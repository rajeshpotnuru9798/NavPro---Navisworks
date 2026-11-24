using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Document-bound UI-thread access shared by the review tools. Events only advance a revision.</summary>
    public sealed class ClashReviewSession : IDisposable
    {
        private readonly Document document;
        private readonly DocumentClash clash;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        private long revision;
        private bool disposed;
        public long Revision => Interlocked.Read(ref revision);
        public string UnitsLabel { get { CheckHost(); return document.Units.ToString(); } }
        public Document Document { get { CheckHost(); return document; } }
        public ClashReviewSession(Document document)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            CheckHost(); clash = document.Clash as DocumentClash ?? throw new InvalidOperationException("Navisworks Manage Clash Detective is required.");
            clash.TestsData.Changed += Changed;
            document.Models.CollectionChanged += Changed; document.Models.ModelTransformChanged += Changed;
            document.Models.ModelItemPropertiesChanged += Changed; document.UnitsChanged += Changed;
            document.SelectionSets.Changed += Changed;
        }
        private void Changed(object sender, EventArgs args) => Interlocked.Increment(ref revision);
        public void CheckHost()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ClashReviewSession));
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks operations must run on the host UI thread.");
            if (Autodesk.Navisworks.Api.Application.ActiveDocument != document || document.IsDisposed)
                throw new InvalidOperationException("The active model changed or closed. Close this tool and reopen it for the current document.");
        }
        public List<ReviewTest> ReadTests()
        {
            CheckHost(); return Tests().Select(test => new ReviewTest { Id = test.Guid.ToString("D"), Name = test.DisplayName,
                Count = Leaves(test.Children).Count() }).OrderBy(test => test.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public ReviewSnapshot ReadSnapshot(IEnumerable<string> testIds, bool includeItemMetadata = true)
        {
            CheckHost(); var ids = new HashSet<string>(testIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (ids.Count == 0) throw new InvalidOperationException("Check at least one clash test.");
            var tests = Tests().Where(test => ids.Contains(test.Guid.ToString("D"))).ToList();
            if (tests.Count != ids.Count) throw new InvalidOperationException("The test list changed. Refresh tests and select again.");
            var snapshot = new ReviewSnapshot { Revision = Revision, Units = document.Units.ToString() };
            snapshot.TestIds.AddRange(ids.OrderBy(id => id, StringComparer.Ordinal));
            foreach (var test in tests)
                ReadRows(test, test, "", snapshot.Clashes, includeItemMetadata);
            snapshot.Signature = Signature(snapshot); return snapshot;
        }
        private void ReadRows(ClashTest test, GroupItem parent, string parentGroup, List<ReviewClash> rows, bool includeItemMetadata)
        {
            foreach (var item in parent.Children)
            {
                if (item is ClashResultGroup group) { ReadRows(test, group, group.Guid.ToString("D"), rows, includeItemMetadata); continue; }
                if (!(item is ClashResult result)) continue;
                var a = includeItemMetadata ? result.CompositeItem1 ?? result.Item1 : null; var b = includeItemMetadata ? result.CompositeItem2 ?? result.Item2 : null;
                var point = result.Center;
                var row = new ReviewClash { TestId = test.Guid.ToString("D"), TestName = test.DisplayName,
                    Id = result.Guid.ToString("D"), Name = result.DisplayName, Description = result.Description ?? "", Status = result.Status.ToString(),
                    TestType = test.TestType.ToString(), ParentGroup = parentGroup, X = point.X, Y = point.Y, Z = point.Z,
                    Distance = result.Distance, Units = document.Units.ToString(), ItemA = a?.DisplayName ?? "Unavailable", ItemB = b?.DisplayName ?? "Unavailable",
                    KeyA = ItemKey(a), KeyB = ItemKey(b), ModelA = a?.Model?.FileName ?? "", ModelB = b?.Model?.FileName ?? "" };
                ReadProperties(a, row.PropertiesA); ReadProperties(b, row.PropertiesB); rows.Add(row);
            }
        }
        private string ItemKey(ModelItem item)
        {
            if (item == null) return null;
            var path = document.Models.CreatePathId(item);
            return path.ModelIndex.ToString(CultureInfo.InvariantCulture) + ":" + path.PathId;
        }
        private static void ReadProperties(ModelItem item, Dictionary<string, string> properties)
        {
            if (item == null) return;
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            foreach (var category in item.PropertyCategories)
                foreach (var property in category.Properties)
                {
                    if (property.Value.IsNone) continue;
                    string key = category.DisplayName + " / " + property.DisplayName;
                    // Same display label can occur twice; ambiguous properties are not merged silently.
                    if (ambiguous.Contains(key)) continue;
                    if (properties.ContainsKey(key)) { properties.Remove(key); ambiguous.Add(key); continue; }
                    properties.Add(key, property.Value.IsDisplayString ? property.Value.ToDisplayString() : property.Value.ToString());
                }
        }
        public void Validate(ReviewSnapshot snapshot)
        {
            CheckHost();
            if (snapshot == null || snapshot.Revision != Revision || snapshot.Units != document.Units.ToString())
                throw new InvalidOperationException("The model, tests or settings changed. Load results and preview again.");
            var live = ReadSnapshot(snapshot.TestIds);
            if (live.Signature != snapshot.Signature) throw new InvalidOperationException("Clash results changed since preview. Load results again.");
        }
        public void ShowClash(ReviewClash source)
        {
            CheckHost(); var test = FindTest(source.TestId);
            var result = Leaves(test.Children).SingleOrDefault(item => item.Guid.ToString("D") == source.Id)
                ?? throw new InvalidOperationException("The clash no longer exists. Reload results.");
            using (var view = clash.TestsData.TestsViewpointForResult(result)) document.CurrentViewpoint.CopyFrom(view);
            using (var selected = new ModelItemCollection())
            {
                if (result.Item1 != null) selected.Add(result.Item1); if (result.Item2 != null) selected.Add(result.Item2);
                document.CurrentSelection.CopyFrom(selected);
            }
        }
        public int ApplyGroups(ClashGroupingPlan plan, IEnumerable<ReviewGroup> requested)
        {
            Validate(plan?.Snapshot);
            var groups = requested?.ToList() ?? new List<ReviewGroup>();
            if (groups.Count == 0) throw new InvalidOperationException("Check at least one proposed group.");
            if (groups.Any(group => !plan.Groups.Contains(group)) || groups.SelectMany(g => g.ResultIds.Select(id => g.TestId + "/" + id)).Distinct().Count() != groups.Sum(g => g.ResultIds.Count))
                throw new InvalidOperationException("The group plan is invalid. Preview again.");
            foreach (var testGroups in groups.GroupBy(group => group.TestId))
            {
                var test = FindTest(testGroups.Key);
                var names = new HashSet<string>(test.Children.Select(item => item.DisplayName), StringComparer.OrdinalIgnoreCase);
                if (testGroups.Any(group => group.ResultIds.Count < 2 || !names.Add(group.Name)))
                    throw new InvalidOperationException("A proposed group name already exists or is duplicated. Change the prefix and preview again.");
            }
            using (var transaction = document.BeginTransaction("NavPro Smart Clash Grouper"))
            {
                foreach (var group in groups)
                {
                    var test = FindTest(group.TestId);
                    using (var fresh = new ClashResultGroup { DisplayName = group.Name }) clash.TestsData.TestsAddCopy(test, fresh);
                    foreach (var resultId in group.ResultIds)
                    {
                        test = FindTest(group.TestId);
                        var target = test.Children.OfType<ClashResultGroup>().Single(item => item.DisplayName == group.Name);
                        int index = test.Children.Select((item, n) => new { item, n }).Where(pair => pair.item is ClashResult && pair.item.Guid.ToString("D") == resultId).Select(pair => pair.n).DefaultIfEmpty(-1).Single();
                        if (index < 0) throw new InvalidOperationException("A result moved since preview. Reload before retrying.");
                        clash.TestsData.TestsMove(test, index, target, target.Children.Count);
                    }
                }
                // Inspect the full selected result set before commit: identities, review data and locations must survive moves.
                var after = ReadSnapshot(plan.Snapshot.TestIds);
                if (Signature(after, false) != Signature(plan.Snapshot, false)) throw new InvalidOperationException("Navisworks did not preserve all result data while grouping. Inspect the document before retrying.");
                foreach (var group in groups)
                {
                    var saved = FindTest(group.TestId).Children.OfType<ClashResultGroup>().Single(item => item.DisplayName == group.Name);
                    if (!new HashSet<string>(saved.Children.Select(item => item.Guid.ToString("D"))).SetEquals(group.ResultIds))
                        throw new InvalidOperationException("The proposed group membership was not retained.");
                }
                transaction.Commit();
            }
            return groups.Sum(group => group.ResultIds.Count);
        }
        public int ApplyDescriptions(ReviewSnapshot snapshot, IEnumerable<DescriptionDraft> drafts)
        {
            Validate(snapshot); var rows = drafts?.ToList() ?? new List<DescriptionDraft>();
            if (rows.Count == 0) throw new InvalidOperationException("Check at least one draft to apply.");
            if (rows.Any(row => row?.Source == null || string.IsNullOrWhiteSpace(row.Text) || row.Text.Length > 2000
                || !snapshot.Clashes.Any(source => source.Identity == row.Source.Identity)) || rows.Select(row => row.Source.Identity).Distinct().Count() != rows.Count)
                throw new InvalidOperationException("Every checked draft must be unique and contain 1–2000 characters.");
            var changed = rows.Where(row => row.Text != row.Source.Description).ToList();
            using (var transaction = document.BeginTransaction("NavPro Clash Descriptions"))
            {
                foreach (var row in changed)
                {
                    var live = Leaves(FindTest(row.Source.TestId).Children).Single(item => item.Guid.ToString("D") == row.Source.Id);
                    clash.TestsData.TestsEditResultDescription(live, row.Text);
                }
                var after = ReadSnapshot(snapshot.TestIds).Clashes.ToDictionary(row => row.Identity);
                if (changed.Any(row => !after.TryGetValue(row.Source.Identity, out var saved) || saved.Description != row.Text || saved.Status != row.Source.Status))
                    throw new InvalidOperationException("Navisworks did not confirm every description. Reload to inspect the results.");
                transaction.Commit();
            }
            return changed.Count;
        }
        private ClashTest FindTest(string id) => Tests().SingleOrDefault(test => test.Guid.ToString("D") == id)
            ?? throw new InvalidOperationException("A clash test was removed. Refresh the list.");
        private IEnumerable<ClashTest> Tests() => FlattenTests(clash.TestsData.Tests);
        private static IEnumerable<ClashTest> FlattenTests(IEnumerable<SavedItem> items)
        { foreach (var item in items) { if (item is ClashTest test) yield return test; else if (item is GroupItem group) foreach (var child in FlattenTests(group.Children)) yield return child; } }
        private static IEnumerable<ClashResult> Leaves(IEnumerable<SavedItem> items)
        { foreach (var item in items) { if (item is ClashResult result) yield return result; else if (item is ClashResultGroup group) foreach (var child in Leaves(group.Children)) yield return child; } }
        private static string Signature(ReviewSnapshot snapshot, bool includeParent = true)
        {
            var builder = new StringBuilder();
            foreach (var row in snapshot.Clashes.OrderBy(row => row.Identity, StringComparer.Ordinal))
            {
                foreach (var value in new[] { row.Identity, row.TestName, row.Name, row.Description, row.Status, row.TestType, row.ItemA, row.ItemB,
                    row.KeyA, row.KeyB, row.ModelA, row.ModelB, includeParent ? row.ParentGroup : "", row.X.ToString("R", CultureInfo.InvariantCulture),
                    row.Y.ToString("R", CultureInfo.InvariantCulture), row.Z.ToString("R", CultureInfo.InvariantCulture), row.Distance.ToString("R", CultureInfo.InvariantCulture) })
                    builder.Append((value ?? "").Length).Append(':').Append(value);
            }
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }
        public void Dispose()
        {
            if (disposed) return;
            if (document.IsDisposed) { disposed = true; return; }
            clash.TestsData.Changed -= Changed; document.Models.CollectionChanged -= Changed;
            document.Models.ModelTransformChanged -= Changed; document.Models.ModelItemPropertiesChanged -= Changed;
            document.UnitsChanged -= Changed; document.SelectionSets.Changed -= Changed; disposed = true;
        }
    }
}
