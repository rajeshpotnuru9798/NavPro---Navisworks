using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Reads and renames Clash Detective tests in Manage 2024.</summary>
    public interface IClashRenamerService
    {
        List<ClashRenameItem> ReadTests();
        ClashRenameResult Apply(IEnumerable<ClashRenameItem> requested);
    }
    public sealed class ClashRenamerService : IClashRenamerService
    {
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;

        public ClashRenamerService(Document document) { this.document = document ?? throw new ArgumentNullException(nameof(document)); }

        public List<ClashRenameItem> ReadTests()
        {
            CheckHost();
            return Flatten(GetClash().TestsData.Tests).OfType<ClashTest>()
                .Select(test => new ClashRenameItem { Id = test.Guid.ToString("D"), CurrentName = test.DisplayName, NewName = test.DisplayName })
                .OrderBy(test => test.CurrentName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public ClashRenameResult Apply(IEnumerable<ClashRenameItem> requested)
        {
            CheckHost();
            var updates = (requested ?? Enumerable.Empty<ClashRenameItem>()).ToList();
            if (updates.Count == 0) throw new InvalidOperationException("Select at least one clash test to update.");
            if (updates.Any(item => item == null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.NewName)))
                throw new InvalidOperationException("Every selected clash test needs a non-empty name.");
            if (updates.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != updates.Count)
                throw new InvalidOperationException("The update list contains a clash test more than once.");

            var current = Flatten(GetClash().TestsData.Tests).OfType<ClashTest>().ToList();
            var byId = current.ToDictionary(test => test.Guid.ToString("D"), StringComparer.OrdinalIgnoreCase);
            if (updates.Any(item => !byId.ContainsKey(item.Id)))
                throw new InvalidOperationException("The clash test list changed since it was loaded. Refresh and review the names again.");
            if (updates.Any(item => !string.Equals(byId[item.Id].DisplayName, item.CurrentName, StringComparison.Ordinal)))
                throw new InvalidOperationException("A selected test name changed since it was loaded. Refresh and review the names again.");

            var targetNames = current.ToDictionary(test => test.Guid.ToString("D"), test => test.DisplayName, StringComparer.OrdinalIgnoreCase);
            foreach (var item in updates) targetNames[item.Id] = item.NewName.Trim();
            var duplicate = targetNames.Values.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null) throw new InvalidOperationException("The proposed name '" + duplicate.Key + "' would be used by more than one clash test. Make each test name unique.");

            var changed = updates.Where(item => !string.Equals(byId[item.Id].DisplayName, item.NewName.Trim(), StringComparison.Ordinal)).ToList();
            var result = new ClashRenameResult { Renamed = changed.Count, Unchanged = updates.Count - changed.Count };
            if (changed.Count == 0) return result;

            using (var transaction = document.BeginTransaction("NavPro Clash Renamer"))
            {
                var testsData = GetClash().TestsData;
                foreach (var item in changed)
                {
                    CheckHost();
                    testsData.TestsEditDisplayName(byId[item.Id], item.NewName.Trim());
                }
                transaction.Commit();
            }

            var verified = Flatten(GetClash().TestsData.Tests).OfType<ClashTest>().ToDictionary(test => test.Guid.ToString("D"), StringComparer.OrdinalIgnoreCase);
            if (changed.Any(item => !verified.TryGetValue(item.Id, out var test) || !string.Equals(test.DisplayName, item.NewName.Trim(), StringComparison.Ordinal)))
                throw new InvalidOperationException("Navisworks did not confirm every requested test name. Refresh the list to see the current names.");
            foreach (var item in changed) CoordinationChangeLog.Record(document, "Renamer", item.Id, item.CurrentName, item.NewName.Trim());
            return result;
        }

        private DocumentClash GetClash()
        {
            var clash = document.Clash as DocumentClash;
            if (clash == null) throw new InvalidOperationException("Clash Detective is unavailable. Renamer requires Navisworks Manage.");
            return clash;
        }

        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks API calls must stay on the host UI thread.");
            if (Application.ActiveDocument != document) throw new InvalidOperationException("The active document changed. Reopen Renamer for the current document.");
        }

        private static IEnumerable<SavedItem> Flatten(IEnumerable<SavedItem> items)
        {
            foreach (var item in items)
            {
                if (item is ClashTest) { yield return item; continue; }
                if (item is GroupItem group)
                    foreach (var child in Flatten(group.Children)) yield return child;
            }
        }
    }
}
