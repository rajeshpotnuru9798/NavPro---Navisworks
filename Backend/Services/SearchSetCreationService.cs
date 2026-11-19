using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    public sealed class SearchSetCreationService
    {
        private readonly Document document;
        private readonly ModelInspectionService guard;
        public SearchSetCreationService(Document document, ModelInspectionService guard) { this.document = document; this.guard = guard; }
        public HashSet<string> ExistingNames()
        {
            guard.Check();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<SavedItem>(document.SelectionSets.Value);
            while (pending.Count > 0)
            {
                var item = pending.Pop(); names.Add(item.DisplayName);
                if (item is GroupItem group) foreach (var child in group.Children) pending.Push(child);
            }
            return names;
        }
        public async Task Count(InspectionResult result, CancellationToken cancel, Action<long, long?> progress)
        {
            for (int i = 0; i < result.Values.Count; i++)
            {
                guard.Check(); cancel.ThrowIfCancellationRequested(); progress(i, result.Values.Count);
                await Task.Delay(15, cancel); guard.Check();
                // Count the exact typed property values already collected during the UI-thread model scan.
                // Re-running Search.FindAll for every value can fail inside Navisworks' native search engine
                // on some federated NWF/NWC models, although the dynamic Search itself remains valid to save.
                result.Values[i].Matches = (int)Math.Min(int.MaxValue, result.Values[i].Occurrences);
                progress(i + 1, result.Values.Count);
            }
        }
        public async Task<int> Create(InspectionResult result, IList<NameDecision> plan, CancellationToken cancel, Action<long, long?> progress)
        {
            guard.Check();
            if (plan.Count != result.Values.Count || plan.Any(p => p.Error != null)) throw new InvalidOperationException("Resolve all preview name errors first.");
            var names = ExistingNames();
            string folderName = "NavPro - SetCreator";
            int n = 2;
            while (names.Contains(folderName) || plan.Any(p => string.Equals(p.Name, folderName, StringComparison.OrdinalIgnoreCase)))
                folderName = "NavPro - SetCreator (" + n++ + ")";

            // Prepare the sets first. Adding children to a detached FolderItem and then
            // copying that folder made creation dependent on how Navisworks copied its
            // nested SavedItemCollection. API 21 provides AddCopy(parent, item) for a
            // parent already in DocumentSelectionSets; use that explicit host path.
            var prepared = new List<SelectionSet>();
            try
            {
                for (int i = 0; i < result.Values.Count; i++)
                {
                    guard.Check(); cancel.ThrowIfCancellationRequested();
                    if (!plan[i].Exists && result.Values[i].Matches > 0)
                    {
                        using (var search = SearchCriteriaFactory.Create(result.Values[i]))
                            prepared.Add(new SelectionSet(search) { DisplayName = plan[i].Name });
                    }
                    progress(i + 1, result.Values.Count);
                    if (i % 20 == 0) await Task.Delay(15, cancel);
                }
                guard.Check(); cancel.ThrowIfCancellationRequested();
                if (prepared.Count == 0) return 0;

                // Recheck immediately before the only mutation. Never replace/remove an existing saved item.
                var latest = ExistingNames();
                if (latest.Contains(folderName) || prepared.Any(c => latest.Contains(c.DisplayName)))
                    throw new InvalidOperationException("Saved sets changed during preparation. Refresh the preview before creating.");

                // API 21 XML documents this overload as adding a copy under a GroupItem
                // already contained by SelectionSets. Keep this short commit region
                // synchronous on the Navisworks UI thread.
                using (var transaction = document.BeginTransaction("NavPro SetCreator"))
                {
                    using (var folderTemplate = new FolderItem { DisplayName = folderName })
                        document.SelectionSets.AddCopy(folderTemplate);

                    var targetFolder = document.SelectionSets.Value.OfType<FolderItem>()
                        .FirstOrDefault(item => string.Equals(item.DisplayName, folderName, StringComparison.Ordinal));
                    if (targetFolder == null)
                        throw new InvalidOperationException("Navisworks did not add the SetCreator folder to the Sets tree.");

                    foreach (var set in prepared)
                    {
                        guard.Check();
                        document.SelectionSets.AddCopy(targetFolder, set);
                    }
                    transaction.Commit();
                }

                guard.Check();
                var savedFolder = document.SelectionSets.Value.OfType<FolderItem>()
                    .FirstOrDefault(item => string.Equals(item.DisplayName, folderName, StringComparison.Ordinal));
                int created = savedFolder == null ? 0 : savedFolder.Children.OfType<SelectionSet>()
                    .Count(item => prepared.Any(expected => string.Equals(item.DisplayName, expected.DisplayName, StringComparison.Ordinal)));
                if (created != prepared.Count)
                    throw new InvalidOperationException("Navisworks did not retain all previewed sets in the Sets tree. Expected " + prepared.Count + ", found " + created + ". Inspect the Sets tree before retrying.");
                return created;
            }
            finally
            {
                foreach (var set in prepared) set.Dispose();
            }
        }
    }
}
