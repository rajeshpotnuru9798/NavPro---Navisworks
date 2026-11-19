using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Writes an Excel-compatible workbook with dashboard summary, test, and clash-result sheets.</summary>
    internal static class ClashExcelReportWriter
    {
        private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string ContentTypeNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

        public static void Write(string path, ClashDashboardSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose an Excel workbook destination.", nameof(path));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, false, Encoding.UTF8))
            {
                WriteContentTypes(archive);
                WriteRootRelationships(archive);
                WriteWorkbook(archive);
                WriteWorkbookRelationships(archive);
                WriteSummarySheet(archive, snapshot);
                WriteTestsSheet(archive, snapshot);
                WriteResultsSheet(archive, snapshot);
            }
        }

        private static void WriteContentTypes(ZipArchive archive)
        {
            WriteXml(archive, "[Content_Types].xml", writer =>
            {
                writer.WriteStartElement("Types", ContentTypeNamespace);
                writer.WriteStartElement("Default", ContentTypeNamespace); writer.WriteAttributeString("Extension", "rels"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml"); writer.WriteEndElement();
                writer.WriteStartElement("Default", ContentTypeNamespace); writer.WriteAttributeString("Extension", "xml"); writer.WriteAttributeString("ContentType", "application/xml"); writer.WriteEndElement();
                for (int i = 1; i <= 3; i++) { writer.WriteStartElement("Override", ContentTypeNamespace); writer.WriteAttributeString("PartName", "/xl/worksheets/sheet" + i.ToString(CultureInfo.InvariantCulture) + ".xml"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"); writer.WriteEndElement(); }
                writer.WriteStartElement("Override", ContentTypeNamespace); writer.WriteAttributeString("PartName", "/xl/workbook.xml"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"); writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteRootRelationships(ZipArchive archive)
        {
            WriteXml(archive, "_rels/.rels", writer =>
            {
                writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
                writer.WriteStartElement("Relationship", PackageRelationshipNamespace); writer.WriteAttributeString("Id", "rId1"); writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"); writer.WriteAttributeString("Target", "xl/workbook.xml"); writer.WriteEndElement();
                writer.WriteEndElement();
            });
        }

        private static void WriteWorkbook(ZipArchive archive)
        {
            WriteXml(archive, "xl/workbook.xml", writer =>
            {
                writer.WriteStartElement("workbook", MainNamespace); writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);
                writer.WriteStartElement("sheets", MainNamespace);
                string[] names = { "Dashboard Summary", "Clash Tests", "Clash Results" };
                for (int i = 0; i < names.Length; i++) { writer.WriteStartElement("sheet", MainNamespace); writer.WriteAttributeString("name", names[i]); writer.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("r", "id", RelationshipNamespace, "rId" + (i + 1).ToString(CultureInfo.InvariantCulture)); writer.WriteEndElement(); }
                writer.WriteEndElement(); writer.WriteEndElement();
            });
        }

        private static void WriteWorkbookRelationships(ZipArchive archive)
        {
            WriteXml(archive, "xl/_rels/workbook.xml.rels", writer =>
            {
                writer.WriteStartElement("Relationships", PackageRelationshipNamespace);
                for (int i = 1; i <= 3; i++) { writer.WriteStartElement("Relationship", PackageRelationshipNamespace); writer.WriteAttributeString("Id", "rId" + i.ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"); writer.WriteAttributeString("Target", "worksheets/sheet" + i.ToString(CultureInfo.InvariantCulture) + ".xml"); writer.WriteEndElement(); }
                writer.WriteEndElement();
            });
        }

        private static void WriteSummarySheet(ZipArchive archive, ClashDashboardSnapshot snapshot)
        {
            int completed = GetCount(snapshot, "Reviewed") + GetCount(snapshot, "Approved") + GetCount(snapshot, "Resolved");
            double completion = snapshot.AllResultCount == 0 ? 0 : completed * 100.0 / snapshot.AllResultCount;
            var rows = new List<IList<object>>
            {
                new object[] { "NavPro Clash Dashboard", snapshot.ProjectName ?? "" },
                new object[] { "Dashboard created", snapshot.ExportedAt ?? "" },
                new object[] { "Display units", snapshot.DisplayUnits ?? "" },
                new object[] { "Loaded models", string.Join(", ", (snapshot.Files ?? new List<ClashDashboardModelFile>()).Select(file => file.Name ?? "")) },
                new object[] { "Clash tests", (snapshot.Tests ?? new List<ClashDashboardTest>()).Count },
                new object[] { "Clashes in workbook detail", (snapshot.Results ?? new List<ClashDashboardResult>()).Count },
                new object[] { "Overall clash count", snapshot.AllResultCount },
                new object[] { "Review completion (%)", Math.Round(completion, 2) },
                new object[] { "Clash status", "Count" }
            };
            foreach (var status in snapshot.Statuses ?? new string[0]) rows.Add(new object[] { status, GetCount(snapshot, status) });
            WriteWorksheet(archive, 1, rows, null, false);
        }

        private static void WriteTestsSheet(ZipArchive archive, ClashDashboardSnapshot snapshot)
        {
            var rows = new List<IList<object>>
            {
                new object[] { "Clash Test", "Type", "Test Status", "Tolerance", "Units", "Last Run", "Included Clash Count", "Priority", "Discipline A", "Discipline B", "Name Validation" }
            };
            foreach (var test in snapshot.Tests ?? new List<ClashDashboardTest>())
                rows.Add(new object[] { test.Name ?? "", test.Type ?? "", test.Status ?? "", test.Tolerance, snapshot.DisplayUnits ?? "", test.LastRun ?? "", test.ClashCount, test.Priority ?? "", test.DisciplineA ?? "", test.DisciplineB ?? "", test.NameValidation ?? "" });
            WriteWorksheet(archive, 2, rows, new[] { 38, 18, 18, 15, 14, 24, 20, 14, 28, 28, 64 }, true);
        }

        private static void WriteResultsSheet(ZipArchive archive, ClashDashboardSnapshot snapshot)
        {
            var rows = new List<IList<object>>
            {
                new object[] { "Test", "Test Type", "Test Status", "Clash", "Clash Status", "Priority", "Discipline A", "Discipline B", "Item A", "Model A", "Item B", "Model B", "Distance", "Units", "X", "Y", "Z", "Assigned To", "Approved By", "Created", "Approved", "Viewpoint", "Image Note" }
            };
            foreach (var result in snapshot.Results ?? new List<ClashDashboardResult>())
                rows.Add(new object[] { result.TestName ?? "", result.TestType ?? "", result.TestStatus ?? "", result.ClashName ?? "", result.Status ?? "", result.Priority ?? "", result.DisciplineA ?? "", result.DisciplineB ?? "", result.ItemA ?? "", result.ModelA ?? "", result.ItemB ?? "", result.ModelB ?? "", result.Distance, result.DisplayUnits ?? "", result.X, result.Y, result.Z, result.AssignedTo ?? "", result.ApprovedBy ?? "", result.CreatedAt ?? "", result.ApprovedAt ?? "", result.Viewpoint ?? "", result.ImageNote ?? "" });
            WriteWorksheet(archive, 3, rows, new[] { 38, 18, 18, 32, 16, 12, 25, 25, 36, 28, 36, 28, 14, 14, 16, 16, 16, 24, 24, 22, 22, 22, 40 }, true);
        }

        private static int GetCount(ClashDashboardSnapshot snapshot, string status)
        {
            int count;
            return snapshot.AllStatusCounts != null && snapshot.AllStatusCounts.TryGetValue(status, out count) ? count : 0;
        }

        private static void WriteWorksheet(ZipArchive archive, int sheetNumber, IList<IList<object>> rows, int[] widths, bool filter)
        {
            WriteXml(archive, "xl/worksheets/sheet" + sheetNumber.ToString(CultureInfo.InvariantCulture) + ".xml", writer =>
            {
                writer.WriteStartElement("worksheet", MainNamespace);
                writer.WriteStartElement("sheetViews", MainNamespace); writer.WriteStartElement("sheetView", MainNamespace); writer.WriteAttributeString("workbookViewId", "0");
                if (filter) { writer.WriteStartElement("pane", MainNamespace); writer.WriteAttributeString("ySplit", "1"); writer.WriteAttributeString("topLeftCell", "A2"); writer.WriteAttributeString("activePane", "bottomLeft"); writer.WriteAttributeString("state", "frozen"); writer.WriteEndElement(); }
                writer.WriteEndElement(); writer.WriteEndElement();
                if (widths != null) { writer.WriteStartElement("cols", MainNamespace); for (int i = 0; i < widths.Length; i++) { writer.WriteStartElement("col", MainNamespace); writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("width", widths[i].ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("customWidth", "1"); writer.WriteEndElement(); } writer.WriteEndElement(); }
                writer.WriteStartElement("sheetData", MainNamespace);
                for (int r = 0; r < rows.Count; r++)
                {
                    writer.WriteStartElement("row", MainNamespace); writer.WriteAttributeString("r", (r + 1).ToString(CultureInfo.InvariantCulture));
                    for (int c = 0; c < rows[r].Count; c++) WriteCell(writer, rows[r][c], ColumnName(c + 1) + (r + 1).ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                if (filter && rows.Count > 1) { writer.WriteStartElement("autoFilter", MainNamespace); writer.WriteAttributeString("ref", "A1:" + ColumnName(rows[0].Count) + rows.Count.ToString(CultureInfo.InvariantCulture)); writer.WriteEndElement(); }
                writer.WriteEndElement();
            });
        }

        private static void WriteCell(XmlWriter writer, object value, string reference)
        {
            if (value == null) return;
            writer.WriteStartElement("c", MainNamespace); writer.WriteAttributeString("r", reference);
            if (IsNumber(value))
            {
                writer.WriteStartElement("v", MainNamespace); writer.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture)); writer.WriteEndElement();
            }
            else
            {
                writer.WriteAttributeString("t", "inlineStr"); writer.WriteStartElement("is", MainNamespace); writer.WriteStartElement("t", MainNamespace); writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve"); writer.WriteString(CleanXmlText(Convert.ToString(value, CultureInfo.InvariantCulture))); writer.WriteEndElement(); writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static bool IsNumber(object value) => value is byte || value is short || value is int || value is long || value is float || value is double || value is decimal;
        private static string CleanXmlText(string value) => new string((value ?? "").Where(character => XmlConvert.IsXmlChar(character)).ToArray());

        private static string ColumnName(int number)
        {
            var name = new StringBuilder();
            while (number > 0) { number--; name.Insert(0, (char)('A' + number % 26)); number /= 26; }
            return name.ToString();
        }

        private static void WriteXml(ZipArchive archive, string path, Action<XmlWriter> content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
            {
                writer.WriteStartDocument(); content(writer); writer.WriteEndDocument();
            }
        }
    }
}
