using System;
using System.Threading;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace NavPro.Backend.Services
{
    /// <summary>Refreshes changed model files already referenced by the active Navisworks document.</summary>
    public sealed class ClashModelRefreshService
    {
        private readonly Document document;
        private readonly int hostThreadId;

        public ClashModelRefreshService(Document document)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            hostThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public bool RefreshChangedFiles()
        {
            CheckHost();
            if (document.Models.Count == 0)
                throw new InvalidOperationException("Open or append at least one model before refreshing files.");

            // Navisworks refreshes existing references whose source files changed on disk.
            // This does not discover or append new files, or remap changed file paths.
            return document.UpdateFiles();
        }

        public string Inspect(bool refreshed = false)
        {
            CheckHost();
            bool published = string.Equals(Path.GetExtension(document.FileName), ".nwd", StringComparison.OrdinalIgnoreCase);
            var previous = ReviewPreferences.Read<Dictionary<string, string>>("reference-observations");
            var report = new StringBuilder(published ? "Published NWD: model source paths can be embedded provenance. An unavailable source does not mean published geometry is missing.\r\n" : "Referenced model paths (availability at inspection time):\r\n");
            foreach (Model model in document.Models)
            {
                string path = model.FileName ?? "";
                try
                {
                    var info = new FileInfo(path);
                    string stamp = info.Exists ? info.LastWriteTimeUtc.ToString("O") + " | " + info.Length + " bytes" : "Unavailable / inaccessible";
                    report.AppendLine(path + "\r\n  " + stamp);
                    if (previous.TryGetValue(path, out var old) && old != stamp) report.AppendLine("  Changed since last NavPro inspection: " + old);
                    previous[path] = stamp;
                }
                catch (Exception ex) { report.AppendLine(path + " — inspection unavailable: " + ex.Message); }
            }
            ReviewPreferences.Write("reference-observations", previous);
            report.AppendLine("\r\nDisk timestamps are current observations, not proof of the version loaded into Navisworks.");
            if (document.Clash is DocumentClash clash)
                foreach (var test in Tests(clash.TestsData.Tests))
                    report.AppendLine("Clash test: " + test.DisplayName + " | last run: " + Convert.ToString(test.LastRun, System.Globalization.CultureInfo.InvariantCulture) + (refreshed ? " | POTENTIALLY OUTDATED — models refreshed; rerun to verify" : " | compare source revisions before relying on results"));
            return report.ToString();
        }
        private static IEnumerable<ClashTest> Tests(IEnumerable<SavedItem> items)
        { foreach (var item in items) { if (item is ClashTest test) yield return test; else if (item is GroupItem group) foreach (var child in Tests(group.Children)) yield return child; } }

        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != hostThreadId)
                throw new InvalidOperationException("Navisworks model refresh must run on the host UI thread.");
            if (Autodesk.Navisworks.Api.Application.ActiveDocument != document) throw new InvalidOperationException("Active document changed. Reopen Refresh Models.");
        }
    }
}
