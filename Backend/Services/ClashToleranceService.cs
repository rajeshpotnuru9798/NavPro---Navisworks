using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Edits existing test settings on the Manage UI thread; never runs tests or saves files.</summary>
    public sealed class ClashToleranceService
    {
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        public ClashToleranceService(Document document) { this.document = document ?? throw new ArgumentNullException(nameof(document)); }

        public List<ClashToleranceItem> ReadTests()
        {
            CheckHost();
            return Tests().Select(test => new ClashToleranceItem(test.Guid.ToString("D"), test.DisplayName,
                test.TestType, test.Tolerance, document.Units, test.MergeComposites)).OrderBy(test => test.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public List<ClashToleranceChange> Preview(IEnumerable<ClashToleranceItem> selected, double tolerance, Units inputUnits, ClashTestType? type, bool? compositeObjectClashing = null)
        {
            CheckHost();
            ValidateTolerance(tolerance);
            if (type.HasValue && !IsStandard(type.Value)) throw new InvalidOperationException("Choose a standard clash type or keep the current type.");
            double converted = tolerance * UnitConversion.ScaleFactor(inputUnits, document.Units);
            ValidateTolerance(converted);
            var items = (selected ?? Enumerable.Empty<ClashToleranceItem>()).ToList();
            if (items.Any(item => item == null)) throw new InvalidOperationException("Refresh the clash test list and preview again.");
            var plan = items.Select(item =>
                new ClashToleranceChange(item, type ?? item.Type, converted, compositeObjectClashing ?? item.CompositeObjectClashing)).ToList();
            ValidatePlan(plan);
            return plan;
        }

        public int Apply(IEnumerable<ClashToleranceChange> preview)
        {
            CheckHost();
            var plan = (preview ?? Enumerable.Empty<ClashToleranceChange>()).ToList();
            ValidatePlan(plan); // Re-resolve every GUID and validate the entire batch before mutation.
            var changed = plan.Where(item => item.Changed).ToList();
            if (changed.Count == 0) return 0;
            using (var transaction = document.BeginTransaction("NavPro Tolerance Updater"))
            {
                foreach (var item in changed)
                {
                    CheckHost();
                    var live = Tests().Single(test => test.Guid.ToString("D") == item.Original.Id);
                    // Copy the complete current test so selections, rules and result history remain attached.
                    using (var copy = (ClashTest)live.CreateCopy())
                    {
                        copy.TestType = item.Type;
                        copy.Tolerance = item.Tolerance;
                        copy.MergeComposites = item.CompositeObjectClashing;
                        GetClash().TestsData.TestsEditTestFromCopy(live, copy);
                    }
                }
                Verify(changed);
                transaction.Commit();
            }
            Verify(changed);
            foreach (var item in changed) CoordinationChangeLog.Record(document, "Tolerance Updater", item.Original.Name,
                item.Original.Type + "; " + item.Original.Tolerance + " " + item.Original.DocumentUnits + "; composite " + item.Original.CompositeObjectClashing,
                item.Type + "; " + item.Tolerance + " " + item.Original.DocumentUnits + "; composite " + item.CompositeObjectClashing);
            return changed.Count;
        }

        /// <summary>Reruns only the selected tests, resolving each live ClashTest by GUID immediately before its synchronous run.</summary>
        public int RunTests(IEnumerable<ClashToleranceItem> selected, Action<string, int, int> afterEach = null)
        {
            CheckHost();
            var items = (selected ?? Enumerable.Empty<ClashToleranceItem>()).ToList();
            if (items.Count == 0) throw new InvalidOperationException("Select at least one clash test to rerun.");
            if (items.Any(item => item == null || string.IsNullOrWhiteSpace(item.Id)))
                throw new InvalidOperationException("Refresh the clash test list and select the tests again.");
            if (items.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
                throw new InvalidOperationException("A clash test was selected more than once.");

            var testsData = GetClash().TestsData;
            var ids = new HashSet<string>(Tests().Select(test => test.Guid.ToString("D")), StringComparer.OrdinalIgnoreCase);
            var missing = items.Where(item => !ids.Contains(item.Id)).Select(item => item.Name).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException("Refresh the test list before rerunning. Missing tests: " + string.Join(", ", missing));

            int completed = 0;
            foreach (var item in items)
            {
                CheckHost();
                // A prior run may update the test collection, so fetch a fresh API object for every run.
                var live = Tests().FirstOrDefault(test => string.Equals(test.Guid.ToString("D"), item.Id, StringComparison.OrdinalIgnoreCase));
                if (live == null)
                    throw new InvalidOperationException("Could not find clash test '" + item.Name + "'. Completed " + completed + " of " + items.Count + " runs.");
                try
                {
                    testsData.TestsRunTest(live);
                    completed++;
                    afterEach?.Invoke(item.Name, completed, items.Count);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Clash Detective stopped while rerunning '" + item.Name + "'. Completed " + completed + " of " + items.Count + " requested runs. " + ex.Message, ex);
                }
            }
            CheckHost();
            return completed;
        }

        private void ValidatePlan(List<ClashToleranceChange> plan)
        {
            if (plan.Count == 0) throw new InvalidOperationException("Select at least one clash test.");
            if (plan.Any(item => item?.Original == null || string.IsNullOrEmpty(item.Original.Id)))
                throw new InvalidOperationException("Refresh the clash test list and preview again.");
            if (plan.Select(item => item.Original.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != plan.Count)
                throw new InvalidOperationException("A clash test was selected more than once.");
            var live = Tests().ToDictionary(test => test.Guid.ToString("D"), StringComparer.OrdinalIgnoreCase);
            foreach (var item in plan)
            {
                ValidateTolerance(item.Tolerance);
                if (item.Type != item.Original.Type && !IsStandard(item.Type))
                    throw new InvalidOperationException("Choose a standard clash type.");
                var old = item.Original;
                if (old.DocumentUnits != document.Units || !live.TryGetValue(old.Id, out var test)
                    || test.DisplayName != old.Name || test.TestType != old.Type || test.Tolerance != old.Tolerance
                    || test.MergeComposites != old.CompositeObjectClashing)
                    throw new InvalidOperationException("A selected test or document units changed. Refresh and preview again before applying.");
            }
        }

        private void Verify(List<ClashToleranceChange> plan)
        {
            var live = Tests().ToDictionary(test => test.Guid.ToString("D"), StringComparer.OrdinalIgnoreCase);
            if (plan.Any(item => !live.TryGetValue(item.Original.Id, out var test) || test.TestType != item.Type
                || test.Tolerance != item.Tolerance || test.MergeComposites != item.CompositeObjectClashing))
                throw new InvalidOperationException("Navisworks did not confirm every update. Refresh and inspect the current test settings.");
        }

        private static bool IsStandard(ClashTestType type) => type == ClashTestType.Hard || type == ClashTestType.HardConservative
            || type == ClashTestType.Clearance || type == ClashTestType.Duplicate;
        private static void ValidateTolerance(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new InvalidOperationException("Tolerance must be a finite, non-negative distance.");
        }
        private IEnumerable<ClashTest> Tests() => Flatten(GetClash().TestsData.Tests);
        private static IEnumerable<ClashTest> Flatten(IEnumerable<SavedItem> items)
        {
            foreach (var item in items)
            {
                if (item is ClashTest test) yield return test;
                else if (item is GroupItem group)
                    foreach (var child in Flatten(group.Children)) yield return child;
            }
        }
        private DocumentClash GetClash() => document.Clash as DocumentClash
            ?? throw new InvalidOperationException("Tolerance Updater requires Navisworks Manage Clash Detective.");
        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks API calls must stay on the host UI thread.");
            if (Application.ActiveDocument != document) throw new InvalidOperationException("The active document changed. Reopen Tolerance Updater for the current document.");
        }
    }
}
