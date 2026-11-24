using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Core;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Manage 2024 Clash Detective matrix discovery and additive test creation.</summary>
    public sealed class ClashMatrixService
    {
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;

        public ClashMatrixService(Document document) { this.document = document ?? throw new ArgumentNullException(nameof(document)); }

        public ClashMatrixSnapshot ReadSnapshot()
        {
            CheckHost();
            var snapshot = new ClashMatrixSnapshot();
            foreach (Model model in document.Models)
                snapshot.Models.Add(string.IsNullOrWhiteSpace(model.FileName) ? "Unnamed model" : Path.GetFileName(model.FileName));

            int index = 0;
            foreach (var item in Flatten(document.SelectionSets.RootItem.Children))
            {
                if (!(item is SelectionSet set)) continue;
                CheckHost();
                snapshot.Sets.Add(new ClashSetChoice
                {
                    Index = index++, Set = set, Name = set.DisplayName, Path = ItemPath(set), SourceKind = "Set",
                    IsSearchSet = set.HasSearch, ItemCount = -1
                });
            }

            var clash = document.Clash as DocumentClash;
            if (clash == null) throw new InvalidOperationException("Clash Detective data is unavailable. Confirm this is Navisworks Manage and a document is open.");
            foreach (var item in Flatten(clash.TestsData.Tests))
                if (item is ClashTest test)
                {
                    snapshot.ExistingTests.Add(test);
                    if (test.LastRun != null) snapshot.TestsRun++;
                    CountResults(test.Children, snapshot.ResultStatuses);
                }
            return snapshot;
        }

        public int CountItems(ClashSetChoice choice)
        {
            CheckHost();
            if (choice.ExplicitItems != null) return choice.ExplicitItems.Count;
            // Evaluate Search Sets through their stored Search directly. GetSelectedItems
            // can report an empty collection for a saved Search Set in some Manage 2024
            // federated documents even though Clash Detective can retain it as a dynamic
            // selection source. Preview must count the actual search result so it doesn't
            // incorrectly mark every proposed matrix row empty and disable Create.
            if (choice.Set.HasSearch)
            {
                using (var search = new Search(choice.Set.Search))
                    return search.FindAll(document, false).Count;
            }
            using (var selected = choice.Set.GetSelectedItems(document)) return selected.Count;
        }

        public List<ClashSetChoice> ReadModelTreeChoices(bool compact)
        {
            CheckHost();
            var choices = new List<ClashSetChoice>();
            int index = 0;
            foreach (Model model in document.Models)
            {
                if (!compact)
                {
                    string fileName = string.IsNullOrWhiteSpace(model.FileName) ? "Unnamed model" : Path.GetFileName(model.FileName);
                    var modelItems = new ModelItemCollection();
                    modelItems.Add(model.RootItem);
                    choices.Add(new ClashSetChoice
                    {
                        Index = index++, ExplicitItems = modelItems, SourceKind = "TreeStandard", Name = fileName,
                        Path = fileName, StablePath = string.IsNullOrWhiteSpace(model.FileName) ? fileName : model.FileName,
                        IsSearchSet = false, ItemCount = 1
                    });
                    continue;
                }

                IEnumerable<ModelItem> roots = model.RootItem.Children;
                foreach (ModelItem item in roots)
                {
                    CheckHost();
                    var items = new ModelItemCollection();
                    items.Add(item);
                    choices.Add(new ClashSetChoice
                    {
                        Index = index++, ExplicitItems = items, SourceKind = "TreeCompact", Name = item.DisplayName,
                        Path = ModelItemPath(item), IsSearchSet = false, ItemCount = 1
                    });
                }
            }
            return choices;
        }

        public List<ClashMatrixPair> BuildPreview(IList<ClashSetChoice> sideA, IList<ClashSetChoice> sideB,
            ClashTestType type, double tolerance)
        {
            CheckHost();
            if (sideA == null || sideB == null) throw new ArgumentNullException();
            var pairs = new List<ClashMatrixPair>();
            foreach (var a in sideA)
                foreach (var b in sideB)
                    pairs.Add(new ClashMatrixPair
                    {
                        A = a, B = b, TestName = a.Name + " vs " + b.Name,
                        Type = type, Tolerance = tolerance, Validation = "Ready"
                    });

            var existingNames = new HashSet<string>(ReadSnapshot().ExistingTests.Select(t => t.DisplayName), StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenPairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in pairs)
            {
                if (pair.A.StableKey == pair.B.StableKey) pair.Validation = "Invalid: same set on A and B";
                else if (!ClashPairPolicy.TryClaimPair(pair.A.StableKey, pair.B.StableKey, seenPairs)) pair.Validation = "Duplicate A/B pair (reverse pair skipped)";
                else if (existingNames.Contains(pair.TestName)) pair.Validation = "Existing test name";
                else if (!names.Add(pair.TestName)) pair.Validation = "Duplicate test name";
                else pair.Validation = ClashPairPolicy.EmptySourceValidation(pair.A.ItemCount, pair.A.IsSearchSet, pair.B.ItemCount, pair.B.IsSearchSet) ?? "Ready";
            }
            return pairs;
        }

        public int CreateTests(IList<ClashMatrixPair> pairs, bool recordChangeLog = true)
        {
            CheckHost();
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            var ready = pairs.Where(p => p.Ready).ToList();
            if (ready.Count == 0) throw new InvalidOperationException("There are no valid preview rows to create.");
            if (ready.GroupBy(p => p.TestName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Preview contains duplicate clash test names. Refresh and revise the selected Search Sets.");
            var clash = document.Clash as DocumentClash;
            if (clash == null) throw new InvalidOperationException("Clash Detective data is unavailable.");
            var tests = clash.TestsData;
            var existing = new HashSet<string>(Flatten(tests.Tests).Select(item => item.DisplayName), StringComparer.OrdinalIgnoreCase);
            if (ready.Any(p => existing.Contains(p.TestName)))
                throw new InvalidOperationException("A clash test name was added since preview. Refresh and preview the matrix again.");

            // ClashTest items belong in Value.Tests. Navisworks rejects them when
            // passed to the generic TestsAddCopy(GroupItem, SavedItem) folder overload.
            using (var transaction = document.BeginTransaction("NavPro Clash Matrix"))
            {
                foreach (var pair in ready)
                {
                    CheckHost();
                    using (var test = new ClashTest
                    {
                        DisplayName = pair.TestName,
                        TestType = pair.Type,
                        Tolerance = pair.Tolerance
                    })
                    {
                        AddChoice(test.SelectionA.Selection, pair.A);
                        AddChoice(test.SelectionB.Selection, pair.B);
                        test.SelectionA.PrimitiveTypes = pair.PrimitiveTypesA;
                        test.SelectionB.PrimitiveTypes = pair.PrimitiveTypesB;
                        test.SelectionA.SelfIntersect = pair.SelfIntersect;
                        test.SelectionB.SelfIntersect = pair.SelfIntersect;
                        test.MergeComposites = pair.CompositeObjectClashing;
                        tests.TestsAddCopy(test);
                    }
                }
                transaction.Commit();
            }

            // Do not report success based only on the number of submitted API calls.
            // Confirm every eligible preview name is now present in Clash Detective.
            CheckHost();
            var savedNames = new HashSet<string>(Flatten(tests.Tests).OfType<ClashTest>().Select(test => test.DisplayName), StringComparer.OrdinalIgnoreCase);
            var missingNames = ready.Where(pair => !savedNames.Contains(pair.TestName)).Select(pair => pair.TestName).ToList();
            if (missingNames.Count > 0)
                throw new InvalidOperationException("Navisworks did not retain " + missingNames.Count + " of the " + ready.Count + " requested Clash Detective tests: " + string.Join(", ", missingNames) + ". Inspect the Tests tree before retrying.");
            if (recordChangeLog) foreach (var pair in ready) CoordinationChangeLog.Record(document, "Clash Matrix", pair.TestName, "No test", pair.Type + "; tolerance " + pair.Tolerance + " " + document.Units + "; A: " + pair.A.Path + "; B: " + pair.B.Path);
            return ready.Count;
        }

        /// <summary>Runs only the named tests created by this dialog's preview action.</summary>
        public int RunTests(IList<string> testNames, Action<string, int, int> afterEach = null)
        {
            CheckHost();
            if (testNames == null) throw new ArgumentNullException(nameof(testNames));
            var names = testNames.Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 0) throw new InvalidOperationException("There are no newly created Clash Matrix tests to run.");
            var clash = document.Clash as DocumentClash;
            if (clash == null) throw new InvalidOperationException("Clash Detective data is unavailable.");
            var tests = clash.TestsData;
            int completed = 0;
            foreach (string name in names)
            {
                CheckHost();
                // Resolve a fresh live test for each run because running a test updates
                // Clash Detective state and may invalidate previously read handles.
                var test = Flatten(tests.Tests).OfType<ClashTest>()
                    .FirstOrDefault(candidate => string.Equals(candidate.DisplayName, name, StringComparison.OrdinalIgnoreCase));
                if (test == null)
                    throw new InvalidOperationException("Could not find newly created test '" + name + "' in the Clash Detective Tests list. Completed " + completed + " of " + names.Count + " requested runs.");
                try
                {
                    tests.TestsRunTest(test);
                    completed++;
                    afterEach?.Invoke(name, completed, names.Count);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Clash Detective stopped while running '" + name + "'. Completed " + completed + " of " + names.Count + " requested runs. " + ex.Message, ex);
                }
            }
            CheckHost();
            return completed;
        }

        public static string PairKey(int a, int b) => a < b ? a + ":" + b : b + ":" + a;
        public static string PairKey(string a, string b) => ClashPairPolicy.PairKey(a, b);

        private void AddChoice(Selection selection, ClashSetChoice choice)
        {
            if (choice.ExplicitItems != null) selection.CopyFrom(choice.ExplicitItems);
            else selection.SelectionSources.Add(document.SelectionSets.CreateSelectionSource(choice.Set));
        }

        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread)
                throw new InvalidOperationException("Navisworks API calls must stay on the host UI thread.");
            if (NavisworksApplication.ActiveDocument != document)
                throw new InvalidOperationException("The active document changed. Refresh the Clash Matrix before continuing.");
        }

        private static IEnumerable<SavedItem> Flatten(IEnumerable<SavedItem> items)
        {
            foreach (var item in items)
            {
                yield return item;
                if (item is GroupItem group && !(item is ClashTest))
                    foreach (var child in Flatten(group.Children)) yield return child;
            }
        }

        private static void CountResults(IEnumerable<SavedItem> items, IDictionary<string, int> counts)
        {
            foreach (var item in items)
            {
                if (item is ClashResultGroup group)
                {
                    CountResults(group.Children, counts);
                    continue;
                }
                if (item is IClashResult result)
                {
                    string status = result.Status.ToString();
                    counts[status] = counts.TryGetValue(status, out int count) ? count + 1 : 1;
                }
            }
        }

        private static string ItemPath(SavedItem item)
        {
            var names = new Stack<string>();
            var current = item;
            while (current != null) { names.Push(current.DisplayName); current = current.Parent; }
            return string.Join(" / ", names);
        }

        private static string ModelItemPath(ModelItem item)
        {
            var names = new Stack<string>();
            var current = item;
            while (current != null) { names.Push(current.DisplayName ?? current.ClassDisplayName ?? "Item"); current = current.Parent; }
            return string.Join(" / ", names);
        }

        private static class NavisworksApplication
        {
            public static Document ActiveDocument => Autodesk.Navisworks.Api.Application.ActiveDocument;
        }
    }
}
