using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Manage 2024 Clash Detective result status reads and explicit bulk status updates.</summary>
    public sealed class ClashApproverService
    {
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;

        public ClashApproverService(Document document) { this.document = document ?? throw new ArgumentNullException(nameof(document)); }

        public List<ClashApprovalTest> ReadTests()
        {
            CheckHost();
            var clash = GetClash();
            var tests = new List<ClashApprovalTest>();
            foreach (var item in FlattenTests(clash.TestsData.Tests))
            {
                var test = item as ClashTest;
                if (test == null) continue;
                var choice = new ClashApprovalTest { Id = test.Guid.ToString("D"), Name = test.DisplayName };
                CountLeaves(test.Children, choice.Statuses);
                choice.Total = choice.Statuses.Values.Sum();
                tests.Add(choice);
            }
            return tests.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public ClashStatusUpdatePreview Preview(IEnumerable<string> selectedIds, ClashResultStatus targetStatus)
        {
            CheckHost();
            EnsureStandardStatus(targetStatus);
            var ids = new HashSet<string>(selectedIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var snapshot = ReadTests().Where(t => ids.Contains(t.Id)).ToList();
            var targetName = targetStatus.ToString();
            var preview = new ClashStatusUpdatePreview { SelectedTests = snapshot.Count, TargetStatus = targetName };
            foreach (var test in snapshot)
                foreach (var status in test.Statuses)
                {
                    preview.CurrentStatuses[status.Key] = preview.CurrentStatuses.TryGetValue(status.Key, out int count) ? count + status.Value : status.Value;
                    if (string.Equals(status.Key, targetName, StringComparison.OrdinalIgnoreCase)) preview.AlreadyAtStatus += status.Value;
                    else if (Enum.GetNames(typeof(ClashResultStatus)).Any(name => string.Equals(name, status.Key, StringComparison.OrdinalIgnoreCase))) preview.ToChange += status.Value;
                    else preview.OtherStatuses += status.Value;
                }
            return preview;
        }

        public ClashStatusUpdateResult UpdateStatus(IEnumerable<string> selectedIds, ClashResultStatus targetStatus, string updatedBy)
        {
            CheckHost();
            EnsureStandardStatus(targetStatus);
            if (targetStatus == ClashResultStatus.Approved && string.IsNullOrWhiteSpace(updatedBy)) throw new InvalidOperationException("Enter the approver name before setting status to Approved.");
            var ids = new HashSet<string>(selectedIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (ids.Count == 0) throw new InvalidOperationException("Select at least one clash test.");

            // Re-resolve all test/result handles immediately before changing the document.
            var selected = FlattenTests(GetClash().TestsData.Tests).OfType<ClashTest>()
                .Where(t => ids.Contains(t.Guid.ToString("D"))).ToList();
            if (selected.Count != ids.Count) throw new InvalidOperationException("The clash test list changed since preview. Refresh and preview again.");

            var result = new ClashStatusUpdateResult();
            using (var transaction = document.BeginTransaction("NavPro Status Updater"))
            {
                foreach (var test in selected)
                {
                    CheckHost();
                    int testUpdated = 0;
                    foreach (var clashResult in LeafResults(test.Children))
                    {
                        var status = clashResult.Status;
                        if (status == targetStatus) { result.Skipped++; continue; }
                        if (!Enum.IsDefined(typeof(ClashResultStatus), status))
                        { result.Skipped++; continue; }

                        var testsData = GetClash().TestsData;
#if NAVISWORKS2026
                        string currentUserName = string.IsNullOrWhiteSpace(updatedBy) ? Environment.UserName : updatedBy.Trim();
                        var currentUser = new Assignee(currentUserName);
                        testsData.TestsEditResultStatus(clashResult, targetStatus, currentUser);
#else
                        testsData.TestsEditResultStatus(clashResult, targetStatus);
#endif
                        if (targetStatus == ClashResultStatus.Approved)
                        {
#if NAVISWORKS2026
                            testsData.TestsEditResultApprovedBy(clashResult, new Assignee(updatedBy.Trim()));
#else
                            testsData.TestsEditResultApprovedBy(clashResult, updatedBy.Trim());
#endif
                            testsData.TestsEditResultApprovedTime(clashResult, DateTime.Now);
                        }
                        result.Updated++;
                        testUpdated++;
                    }
                    if (testUpdated > 0) result.TestsChanged++;
                }
                transaction.Commit();
            }
            return result;
        }

        private static void EnsureStandardStatus(ClashResultStatus status)
        {
            if (!Enum.IsDefined(typeof(ClashResultStatus), status)) throw new InvalidOperationException("Choose one of the standard Navisworks clash statuses.");
        }

        private DocumentClash GetClash()
        {
            var clash = document.Clash as DocumentClash;
            if (clash == null) throw new InvalidOperationException("Clash Detective is unavailable. This feature requires Navisworks Manage.");
            return clash;
        }

        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks API calls must stay on the host UI thread.");
            if (Autodesk.Navisworks.Api.Application.ActiveDocument != document) throw new InvalidOperationException("The active document changed. Reopen Clash Approver for the current document.");
        }

        private static IEnumerable<SavedItem> FlattenTests(IEnumerable<SavedItem> items)
        {
            foreach (var item in items)
            {
                if (item is ClashTest) { yield return item; continue; }
                if (item is GroupItem group)
                    foreach (var child in FlattenTests(group.Children)) yield return child;
            }
        }

        private static IEnumerable<IClashResult> LeafResults(IEnumerable<SavedItem> items)
        {
            foreach (var item in items)
            {
                var group = item as ClashResultGroup;
                if (group != null)
                {
                    foreach (var child in LeafResults(group.Children)) yield return child;
                    continue;
                }
                var result = item as IClashResult;
                if (result != null) yield return result;
            }
        }

        private static void CountLeaves(IEnumerable<SavedItem> items, IDictionary<string, int> counts)
        {
            foreach (var result in LeafResults(items))
            {
                var key = result.Status.ToString();
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }
        }
    }
}
