using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Reads dashboard XML/CSV exports and safely previews/applies status-only updates.</summary>
    public sealed class ClashStatusImportService
    {
        private static readonly string[] StandardStatuses = { "New", "Active", "Reviewed", "Approved", "Resolved" };
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;

        public ClashStatusImportService(Document document) { this.document = document ?? throw new ArgumentNullException(nameof(document)); }

        public ClashStatusImportReport ReadReport(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("Choose an exported Clash Dashboard XML or CSV file.", path);
            var report = new ClashStatusImportReport { FilePath = Path.GetFullPath(path), FileType = Path.GetExtension(path).TrimStart('.').ToUpperInvariant() };
            if (string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase)) ReadXml(report);
            else if (string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)) ReadCsv(report);
            else throw new InvalidOperationException("Choose a .xml or .csv file exported from the NavPro Clash Dashboard.");
            if (report.Records.Count == 0) throw new InvalidDataException("The report contains no Clash result rows.");
            foreach (string status in StandardStatuses)
                if (report.Records.Any(r => string.Equals(CanonicalStatus(r.Status), status, StringComparison.Ordinal))) report.AvailableStatuses.Add(status);
            return report;
        }

        public ClashStatusImportPreview Preview(ClashStatusImportReport report, IEnumerable<string> selectedStatuses)
        {
            CheckHost();
            if (report == null) throw new ArgumentNullException(nameof(report));
            var selected = new HashSet<string>((selectedStatuses ?? Enumerable.Empty<string>()).Select(CanonicalStatus).Where(s => s != null), StringComparer.OrdinalIgnoreCase);
            if (selected.Count == 0) throw new InvalidOperationException("Select at least one status to import.");
            var entries = ReadNativeEntries();
            var matchIndex = IndexEntries(entries);
            var identityIndex = entries.ToLookup(e => e.Identity, StringComparer.Ordinal);
            var preview = new ClashStatusImportPreview { Report = report };
            preview.SelectedStatuses.AddRange(StandardStatuses.Where(selected.Contains));
            var pending = new Dictionary<string, List<ClashStatusImportPreviewRow>>(StringComparer.Ordinal);

            foreach (var source in report.Records)
            {
                var row = new ClashStatusImportPreviewRow { Source = source };
                string targetStatus = CanonicalStatus(source.Status);
                if (targetStatus == null) { row.Disposition = ClashStatusImportDisposition.InvalidStatus; row.Details = "Report status is not a standard Navisworks status."; preview.InvalidStatusCount++; preview.Rows.Add(row); continue; }
                if (!selected.Contains(targetStatus)) { row.Disposition = ClashStatusImportDisposition.StatusNotSelected; row.Details = "Status excluded by selection."; preview.FilteredOutCount++; preview.Rows.Add(row); continue; }
                bool hasItemA = !string.IsNullOrWhiteSpace(source.ItemA), hasItemB = !string.IsNullOrWhiteSpace(source.ItemB);
                if (string.IsNullOrWhiteSpace(source.TestName) || string.IsNullOrWhiteSpace(source.ClashName) || hasItemA != hasItemB)
                { row.Disposition = ClashStatusImportDisposition.IncompleteIdentity; row.Details = "Test and clash names are required; item names must be supplied as a pair."; preview.IncompleteIdentityCount++; preview.Rows.Add(row); continue; }

                var matches = FindMatches(source, matchIndex);
                if (matches.Count == 0) { row.Disposition = ClashStatusImportDisposition.NotFound; row.Details = "No result matched this report row."; preview.NotFoundCount++; preview.Rows.Add(row); continue; }
                if (matches.Count > 1) { row.Disposition = ClashStatusImportDisposition.Ambiguous; row.Details = matches.Count.ToString("N0", CultureInfo.CurrentCulture) + " results matched; left unchanged."; preview.AmbiguousCount++; preview.Rows.Add(row); continue; }
                var match = matches[0];
                row.CurrentStatus = match.Result.Status.ToString(); row.TargetIdentity = match.Identity;
                if (!pending.TryGetValue(match.Identity, out var duplicates)) pending[match.Identity] = duplicates = new List<ClashStatusImportPreviewRow>();
                duplicates.Add(row);
                if (string.Equals(row.CurrentStatus, targetStatus, StringComparison.OrdinalIgnoreCase))
                { row.Disposition = ClashStatusImportDisposition.AlreadyCurrent; row.Details = "Already has the imported status."; preview.AlreadyCurrentCount++; preview.Rows.Add(row); continue; }
                row.Disposition = ClashStatusImportDisposition.Ready; row.Details = "Will change to " + targetStatus + ".";
                preview.Rows.Add(row);
            }

            foreach (var pair in pending)
            {
                var rows = pair.Value;
                var distinctStatuses = rows.Select(r => CanonicalStatus(r.Source.Status)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (distinctStatuses.Count > 1)
                {
                    foreach (var row in rows)
                    {
                        if (row.Disposition == ClashStatusImportDisposition.AlreadyCurrent) preview.AlreadyCurrentCount--;
                        row.Disposition = ClashStatusImportDisposition.ConflictingRows; row.Details = "Report has different target statuses for the same clash; left unchanged."; preview.ConflictingRowsCount++;
                    }
                    continue;
                }
                var readyRows = rows.Where(r => r.Disposition == ClashStatusImportDisposition.Ready).ToList();
                if (readyRows.Count == 0) continue;
                var first = rows[0];
                var entry = identityIndex[pair.Key].First();
                preview.Actions.Add(new ClashStatusImportAction { Source = first.Source, TestId = entry.TestId, TargetIdentity = entry.Identity, CurrentStatus = first.CurrentStatus, TargetStatus = distinctStatuses[0] });
                for (int i = 1; i < readyRows.Count; i++) { readyRows[i].Disposition = ClashStatusImportDisposition.Duplicate; readyRows[i].Details = "Duplicate report row; the clash will be updated once."; preview.DuplicateCount++; }
            }
            preview.ReadyCount = preview.Actions.Count;
            return preview;
        }

        public ClashStatusImportResult Apply(ClashStatusImportPreview preview, string updatedBy)
        {
            CheckHost();
            if (preview == null || preview.Report == null) throw new ArgumentNullException(nameof(preview));
            if (preview.Actions.Count == 0) throw new InvalidOperationException("The preview contains no status changes to apply.");
            string actor = string.IsNullOrWhiteSpace(updatedBy) ? Environment.UserName : updatedBy.Trim();
            var entries = ReadNativeEntries();
            var matchIndex = IndexEntries(entries);
            var resolved = new List<Tuple<ClashStatusImportAction, NativeEntry, ClashResultStatus>>();
            foreach (var action in preview.Actions)
            {
                var source = action.Source;
                var candidates = FindMatches(source, matchIndex).Where(e => string.Equals(e.TestId, action.TestId, StringComparison.OrdinalIgnoreCase) && string.Equals(e.Identity, action.TargetIdentity, StringComparison.Ordinal)).ToList();
                if (candidates.Count != 1 || !string.Equals(candidates[0].Result.Status.ToString(), action.CurrentStatus, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The Clash results changed after preview. Refresh the report and preview again; no statuses were changed.");
                if (!Enum.TryParse(action.TargetStatus, true, out ClashResultStatus target)) throw new InvalidOperationException("The preview contains an unsupported clash status.");
                resolved.Add(Tuple.Create(action, candidates[0], target));
            }

            var changedTests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new ClashStatusImportResult();
            var testsData = ((DocumentClash)document.Clash).TestsData;
            using (var transaction = document.BeginTransaction("NavPro Import Clash Status"))
            {
                foreach (var item in resolved)
                {
                    CheckHost();
                    var action = item.Item1; var native = item.Item2; var target = item.Item3;
                    if (native.Result.Status == target) { result.Skipped++; continue; }
#if NAVISWORKS2026
                    testsData.TestsEditResultStatus(native.Result, target, new Assignee(actor));
#else
                    testsData.TestsEditResultStatus(native.Result, target);
#endif
                    if (target == ClashResultStatus.Approved)
                    {
#if NAVISWORKS2026
                        testsData.TestsEditResultApprovedBy(native.Result, new Assignee(actor));
#else
                        testsData.TestsEditResultApprovedBy(native.Result, actor);
#endif
                        testsData.TestsEditResultApprovedTime(native.Result, DateTime.Now);
                    }
                    if (!Equals(native.Result.Status, target)) throw new InvalidOperationException("Navisworks did not confirm the requested status for " + action.Source.ClashName + ". Review the model before continuing.");
                    result.Updated++; changedTests.Add(action.TestId);
                }
                transaction.Commit();
            }
            result.TestsChanged = changedTests.Count;
            foreach (var item in resolved) CoordinationChangeLog.Record(document, "Import Clash Status", item.Item1.Source.TestName + " / " + item.Item1.Source.ClashName,
                item.Item1.CurrentStatus, item.Item3.ToString());
            return result;
        }

        private List<NativeEntry> ReadNativeEntries()
        {
            CheckHost();
            var clash = document.Clash as DocumentClash;
            if (clash == null) throw new InvalidOperationException("Clash Detective is unavailable. Import Clash Status requires Navisworks Manage.");
            var entries = new List<NativeEntry>();
            foreach (var saved in FlattenTests(clash.TestsData.Tests))
            {
                CheckHost();
                var test = saved as ClashTest;
                if (test == null) continue;
                foreach (var result in LeafResults(test.Children))
                {
                    var representative = result as ClashResult ?? result.RepresentativeResult;
                    var itemA = representative == null ? null : representative.Item1;
                    var itemB = representative == null ? null : representative.Item2;
                    entries.Add(new NativeEntry
                    {
                        TestId = test.Guid.ToString("D"), TestName = test.DisplayName ?? "", ClashName = result.DisplayName ?? "",
                        ItemA = itemA == null ? "" : itemA.DisplayName ?? "", ItemB = itemB == null ? "" : itemB.DisplayName ?? "",
                        ModelA = ModelName(itemA), ModelB = ModelName(itemB), Result = result
                    });
                }
            }
            return entries;
        }

        private static string MatchKey(string test, string clash)
        {
            string normalizedTest = Normalize(test);
            return normalizedTest.Length.ToString(CultureInfo.InvariantCulture) + ":" + normalizedTest + Normalize(clash);
        }

        private static ILookup<string, NativeEntry> IndexEntries(IEnumerable<NativeEntry> entries) =>
            entries.ToLookup(e => MatchKey(e.TestName, e.ClashName), StringComparer.OrdinalIgnoreCase);

        private static List<NativeEntry> FindMatches(ClashStatusImportRecord source, ILookup<string, NativeEntry> entries)
        {
            return entries[MatchKey(source.TestName, source.ClashName)].Where(e => EqualText(e.TestName, source.TestName) && EqualText(e.ClashName, source.ClashName)
                && (string.IsNullOrWhiteSpace(source.ItemA) || EqualText(e.ItemA, source.ItemA))
                && (string.IsNullOrWhiteSpace(source.ItemB) || EqualText(e.ItemB, source.ItemB))
                && (string.IsNullOrWhiteSpace(source.ModelA) || EqualFile(e.ModelA, source.ModelA))
                && (string.IsNullOrWhiteSpace(source.ModelB) || EqualFile(e.ModelB, source.ModelB))).ToList();
        }

        private static string Identity(NativeEntry entry)
        {
            return entry.TestId + "\u001f" + Normalize(entry.TestName) + "\u001f" + Normalize(entry.ClashName) + "\u001f" + Normalize(entry.ItemA) + "\u001f" + Normalize(entry.ItemB) + "\u001f" + Normalize(entry.ModelA) + "\u001f" + Normalize(entry.ModelB);
        }

        private static bool EqualFile(string left, string right) => string.Equals(Path.GetFileName(left ?? "").Trim(), Path.GetFileName(right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        private static bool EqualText(string left, string right) => string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        private static string Normalize(string value) => string.Join(" ", (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        private static string ModelName(ModelItem item) { try { return item == null || item.Model == null ? "" : Path.GetFileName(item.Model.FileName ?? ""); } catch { return ""; } }

        private static string CanonicalStatus(string value)
        {
            return StandardStatuses.FirstOrDefault(s => string.Equals(s, (value ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private void ReadXml(ClashStatusImportReport report)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var documentXml = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(report.FilePath, settings)) documentXml.Load(reader);
            var nodes = documentXml.SelectNodes("//*[local-name()='Results']/*[local-name()='Clash']");
            int line = 1;
            if (nodes != null && nodes.Count > 0)
            {
                foreach (XmlElement node in nodes)
                {
                    report.Records.Add(new ClashStatusImportRecord
                    {
                        LineNumber = ++line, TestName = Attribute(node, "test"), ClashName = Attribute(node, "name"), Status = FirstNonEmpty(ChildText(node, "resultstatus"), Attribute(node, "status")),
                        ItemA = ChildText(node, "ItemA"), ItemB = ChildText(node, "ItemB"), ApprovedBy = Attribute(node, "approvedBy"), ApprovedAt = Attribute(node, "approvedTime")
                    });
                }
                return;
            }

            // Navisworks Manage's standard Clash Detective XML export uses exchange/batchtest/clashtests/clashtest/clashresults/clashresult.
            var nativeNodes = documentXml.SelectNodes("//*[local-name()='clashtest']/*[local-name()='clashresults']/*[local-name()='clashresult']");
            if (nativeNodes == null || nativeNodes.Count == 0) throw new InvalidDataException("The XML contains no Navisworks clashresult rows. Export detailed Clash results to XML or use a NavPro Dashboard XML/CSV report.");
            foreach (XmlElement node in nativeNodes)
            {
                var testNode = node.ParentNode == null ? null : node.ParentNode.ParentNode as XmlElement;
                report.Records.Add(new ClashStatusImportRecord
                {
                    LineNumber = ++line,
                    TestName = testNode == null ? "" : Attribute(testNode, "name"),
                    ClashName = Attribute(node, "name"),
                    Status = FirstNonEmpty(ChildText(node, "resultstatus"), Attribute(node, "status")),
                    ApprovedBy = ChildText(node, "approvedby"),
                    ApprovedAt = ChildText(node, "approvedtime")
                });
            }
        }

        private static string FirstNonEmpty(string first, string second) => string.IsNullOrWhiteSpace(first) ? second : first.Trim();

        private static string Attribute(XmlElement element, string name) => element.HasAttribute(name) ? element.GetAttribute(name) : "";
        private static string ChildText(XmlElement element, string name)
        {
            foreach (XmlNode child in element.ChildNodes) if (child is XmlElement && string.Equals(child.LocalName, name, StringComparison.OrdinalIgnoreCase)) return child.InnerText;
            return "";
        }

        private void ReadCsv(ClashStatusImportReport report)
        {
            string text = File.ReadAllText(report.FilePath, Encoding.UTF8).TrimStart('\uFEFF');
            var parsed = ParseCsv(text);
            if (parsed.Count < 2) throw new InvalidDataException("The CSV contains no clash result rows.");
            var headers = parsed[0].Values.Select(NormalizeHeader).ToList();
            int test = Column(headers, "test", "testname"); int clash = Column(headers, "clash", "clashname", "testclash"); int status = Column(headers, "status", "clashstatus");
            int itemA = Column(headers, "itema", "item1"); int itemB = Column(headers, "itemb", "item2"); int modelA = Column(headers, "modela", "model1"); int modelB = Column(headers, "modelb", "model2");
            if (test < 0 || clash < 0 || status < 0 || itemA < 0 || itemB < 0) throw new InvalidDataException("CSV headers must include Test, Clash, Status, Item A and Item B from a NavPro Dashboard export.");
            int approvedBy = Column(headers, "approvedby"); int approvedAt = Column(headers, "approved", "approvedat");
            for (int i = 1; i < parsed.Count; i++)
            {
                var row = parsed[i]; if (row.Values.All(string.IsNullOrWhiteSpace)) continue;
                report.Records.Add(new ClashStatusImportRecord
                {
                    LineNumber = row.Line, TestName = Value(row, test), ClashName = Value(row, clash), Status = Value(row, status),
                    ItemA = Value(row, itemA), ItemB = Value(row, itemB), ModelA = Value(row, modelA), ModelB = Value(row, modelB),
                    ApprovedBy = Value(row, approvedBy), ApprovedAt = Value(row, approvedAt)
                });
            }
        }

        private static int Column(List<string> headers, params string[] names)
        {
            foreach (string name in names) { int index = headers.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase)); if (index >= 0) return index; }
            return -1;
        }
        private static string NormalizeHeader(string value) => new string((value ?? "").Where(char.IsLetterOrDigit).ToArray());
        private static string Value(CsvRow row, int index) => index < 0 || index >= row.Values.Count ? "" : row.Values[index].Trim();

        private static List<CsvRow> ParseCsv(string text)
        {
            var rows = new List<CsvRow>(); var fields = new List<string>(); var field = new StringBuilder(); bool quoted = false; int line = 1; int rowLine = 1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else { field.Append(c); if (c == '\n') line++; }
                    continue;
                }
                if (c == '"' && field.Length == 0) { quoted = true; continue; }
                if (c == ',') { fields.Add(field.ToString()); field.Clear(); continue; }
                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    fields.Add(field.ToString()); field.Clear(); rows.Add(new CsvRow(rowLine, fields)); fields = new List<string>(); line++; rowLine = line; continue;
                }
                field.Append(c);
            }
            if (quoted) throw new InvalidDataException("The CSV has an unterminated quoted field.");
            if (field.Length > 0 || fields.Count > 0) { fields.Add(field.ToString()); rows.Add(new CsvRow(rowLine, fields)); }
            return rows;
        }

        private static IEnumerable<SavedItem> FlattenTests(IEnumerable<SavedItem> items)
        {
            foreach (var item in items) { if (item is ClashTest) { yield return item; continue; } var group = item as GroupItem; if (group != null) foreach (var child in FlattenTests(group.Children)) yield return child; }
        }
        private static IEnumerable<IClashResult> LeafResults(IEnumerable<SavedItem> items)
        {
            foreach (var item in items) { var group = item as ClashResultGroup; if (group != null) { foreach (var child in LeafResults(group.Children)) yield return child; continue; } var result = item as IClashResult; if (result != null) yield return result; }
        }
        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks Clash API calls must run on the host UI thread.");
            if (document == null) throw new InvalidOperationException("The source document is no longer available.");
            if (Application.ActiveDocument != document) throw new InvalidOperationException("The active document changed. Reopen Import Clash Status for the current document.");
        }

        private sealed class NativeEntry
        {
            public string TestId; public string TestName; public string ClashName; public string ItemA; public string ItemB; public string ModelA; public string ModelB; public IClashResult Result;
            public string Identity => TestId + "\u001f" + Normalize(TestName) + "\u001f" + Normalize(ClashName) + "\u001f" + Normalize(ItemA) + "\u001f" + Normalize(ItemB) + "\u001f" + Normalize(ModelA) + "\u001f" + Normalize(ModelB);
        }
        private sealed class CsvRow { public int Line { get; } public List<string> Values { get; } public CsvRow(int line, List<string> values) { Line = line; Values = values; } }
    }
}
