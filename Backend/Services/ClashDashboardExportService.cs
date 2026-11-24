using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;
using NavPro.Backend.Core;

namespace NavPro.Backend.Services
{
    /// <summary>Collects the currently saved Clash Detective tests/results on Navisworks' UI thread.</summary>
    public sealed class ClashDashboardExportService
    {
        private static readonly string[] StatusNames = { "New", "Active", "Reviewed", "Approved", "Resolved" };
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        public string ImageSummary { get; private set; } = "";

        public ClashDashboardExportService(Document document) { this.document = document ?? throw new ArgumentNullException(nameof(document)); }

        public int Export(string destination, ClashDashboardOptions options, Action<string> reportProgress = null)
        {
            CheckHost();
            ImageSummary = "";
            if (string.IsNullOrWhiteSpace(destination) || !string.Equals(Path.GetExtension(destination), ".html", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose an HTML dashboard destination.", nameof(destination));
            if (options == null) throw new ArgumentNullException(nameof(options));
            var clash = document.Clash as DocumentClash;
            if (clash == null) throw new InvalidOperationException("Clash Detective is unavailable. Clash Dashboard requires Navisworks Manage.");

            var snapshot = new ClashDashboardSnapshot {
                ReportId = Guid.NewGuid().ToString("N"), ProjectName = string.IsNullOrWhiteSpace(options.ProjectName) ? Path.GetFileNameWithoutExtension(document.FileName) : options.ProjectName.Trim(),
                ExportedAt = DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture), ExportedAtIso = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture), DisplayUnits = options.DisplayUnits,
                SourceUnits = document.Units.ToString(), Statuses = StatusNames
            };
            foreach (Model model in document.Models)
            {
                CheckHost();
                string name = Path.GetFileName(model.FileName ?? string.Empty);
                if (string.IsNullOrWhiteSpace(name)) continue;
                string modified = "";
                try { if (!string.IsNullOrWhiteSpace(model.FileName) && File.Exists(model.FileName)) modified = File.GetLastWriteTime(model.FileName).ToString("dd MMM yyyy", CultureInfo.InvariantCulture); } catch { }
                snapshot.Files.Add(new ClashDashboardModelFile { Name = name, ModifiedDate = modified });
            }
            double scale = options.UnitScale;
            var testsData = clash.TestsData;
            int sequence = 0;
            int imagesGenerated = 0;
            using (var imageCapture = new ClashImageCapture())
            {
                foreach (var saved in FlattenTests(testsData.Tests))
                {
                    CheckHost(); var test = saved as ClashTest; if (test == null) continue;
                    var nameParts = ClashTestNameParser.Parse(test.DisplayName);
                    var dashboardTest = new ClashDashboardTest { Name = test.DisplayName, Type = test.TestType.ToString(), Tolerance = test.Tolerance * scale, Status = test.Status.ToString(), LastRun = test.LastRun == null ? "" : Convert.ToString(test.LastRun, CultureInfo.InvariantCulture), Priority = nameParts.Priority, DisciplineA = nameParts.DisciplineA, DisciplineB = nameParts.DisciplineB, NameValidation = nameParts.ValidationMessage, IsNameValid = nameParts.IsValid };
                    snapshot.Tests.Add(dashboardTest);
                    foreach (var result in LeafResults(test.Children))
                    {
                        CheckHost(); string resultStatus = result.Status.ToString();
                        snapshot.AllResultCount++;
                        snapshot.AllStatusCounts[resultStatus] = snapshot.AllStatusCounts.TryGetValue(resultStatus, out int allStatusCount) ? allStatusCount + 1 : 1;
                        if (!options.IncludeAllResults && !options.IncludedStatuses.Contains(resultStatus)) continue;
                        var representative = result as ClashResult ?? result.RepresentativeResult;
                        var item1 = representative == null ? null : representative.Item1;
                        var item2 = representative == null ? null : representative.Item2;
                        string model1 = ModelName(item1), model2 = ModelName(item2);
                        var center = result.Center;
                        var row = new ClashDashboardResult {
                            ReportId = snapshot.ReportId, Id = (++sequence).ToString(CultureInfo.InvariantCulture), TestName = test.DisplayName, Priority = nameParts.Priority, NameValidation = nameParts.ValidationMessage, IsNameValid = nameParts.IsValid, TestType = test.TestType.ToString(), TestTolerance = test.Tolerance * scale, TestStatus = test.Status.ToString(),
                            ClashName = result.DisplayName, Status = resultStatus, Description = result.Description, ItemA = item1 == null ? "" : item1.DisplayName, ItemB = item2 == null ? "" : item2.DisplayName,
                            ModelA = model1, ModelB = model2, DisciplineA = nameParts.IsValid ? nameParts.DisciplineA : "Unclassified", DisciplineB = nameParts.IsValid ? nameParts.DisciplineB : "Unclassified", Viewpoint = result.HasSavedViewpoint ? "Saved viewpoint" : "No saved viewpoint", DisplayUnits = options.DisplayUnits,
                            Distance = result.Distance * scale, X = center.X * scale, Y = center.Y * scale, Z = center.Z * scale,
    #if NAVISWORKS2026
                            AssignedTo = result.AssignedTo == null ? "" : result.AssignedTo.DisplayName, ApprovedBy = result.ApprovedBy == null ? "" : result.ApprovedBy.DisplayName,
    #else
                            AssignedTo = result.AssignedTo, ApprovedBy = result.ApprovedBy,
    #endif
                            CreatedAt = result.CreatedTime.HasValue ? result.CreatedTime.Value.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) : "", ApprovedAt = result.ApprovedTime.HasValue ? result.ApprovedTime.Value.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) : ""
                        };
                        if (options.IncludeImages)
                        {
                            row.ImageData = imageCapture.Capture(() => { CheckHost(); return testsData.TestsImageForResult(result, ImageGenerationStyle.ScenePlusOverlay, 420, 240); }, out string imageNote);
                            row.ImageNote = imageNote;
                            if (!string.IsNullOrEmpty(row.ImageData))
                            {
                                imagesGenerated++;
                                row.Viewpoint = result.HasSavedViewpoint ? "Saved viewpoint" : "Generated clash view";
                            }
                            if (sequence == 1 || sequence % 100 == 0)
                                reportProgress?.Invoke("Processed " + sequence.ToString("N0") + " clashes; " + imagesGenerated.ToString("N0") + " images generated. Rendering the current clash cannot be interrupted.");
                        }
                        snapshot.Results.Add(row);
                        dashboardTest.ClashCount++;
                        dashboardTest.ClashStatusCounts[resultStatus] = dashboardTest.ClashStatusCounts.TryGetValue(resultStatus, out int statusCount) ? statusCount + 1 : 1;
                    }
                }
                CheckHost();
                if (options.IncludeImages) ImageSummary = imagesGenerated + " of " + snapshot.Results.Count + " native clash images generated; " + (snapshot.Results.Count - imagesGenerated) + " unavailable (see per-row image notes). " + imageCapture.RecoveredImages + " recovered after resource cleanup and retry.";
                else ImageSummary = "Clash images were not requested.";
            }
            reportProgress?.Invoke("Writing the dashboard. " + ImageSummary);
            ClashDashboardWriter.Write(destination, snapshot);
            if (options.IncludeTabularHtml) ClashDashboardWriter.WriteTabular(Path.ChangeExtension(destination, null) + "_tabular.html", snapshot);
            if (options.IncludeExcel) ClashExcelReportWriter.Write(Path.ChangeExtension(destination, ".xlsx"), snapshot);
            if (options.IncludeXml) ClashDashboardWriter.WriteXml(Path.ChangeExtension(destination, null) + ".xml", snapshot);
            return snapshot.Results.Count;
        }

        private static string ModelName(ModelItem item)
        {
            try { return item == null || item.Model == null ? "" : Path.GetFileName(item.Model.FileName ?? ""); } catch { return ""; }
        }
        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks API calls must stay on the host UI thread.");
            if (Autodesk.Navisworks.Api.Application.ActiveDocument != document) throw new InvalidOperationException("The active document changed. Reopen Clash Dashboard for the current document.");
        }
        private static IEnumerable<SavedItem> FlattenTests(IEnumerable<SavedItem> items)
        {
            foreach (var item in items) { if (item is ClashTest) { yield return item; continue; } var group = item as GroupItem; if (group != null) foreach (var child in FlattenTests(group.Children)) yield return child; }
        }
        private static IEnumerable<IClashResult> LeafResults(IEnumerable<SavedItem> items)
        {
            foreach (var item in items) { var group = item as ClashResultGroup; if (group != null) { foreach (var child in LeafResults(group.Children)) yield return child; continue; } var result = item as IClashResult; if (result != null) yield return result; }
        }
    }
}
