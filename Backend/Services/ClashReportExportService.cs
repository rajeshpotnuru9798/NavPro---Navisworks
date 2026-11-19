using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Exports all current Clash Detective leaf results to an image-enabled XLSX workbook.</summary>
    public sealed class ClashReportExportService
    {
        private const int ImageWidth = 320;
        private const int ImageHeight = 180;
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;

        public ClashReportExportService(Document document) { this.document = document ?? throw new ArgumentNullException(nameof(document)); }

        public int ExportAll(string destinationPath)
        {
            CheckHost();
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Choose an Excel report destination.", nameof(destinationPath));
            if (!string.Equals(Path.GetExtension(destinationPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Clash Report must be saved as an .xlsx workbook.");

            var clash = document.Clash as DocumentClash;
            if (clash == null) throw new InvalidOperationException("Clash Detective is unavailable. Clash Report requires Navisworks Manage.");
            string imageDirectory = Path.Combine(Path.GetTempPath(), "NavPro-ClashReport-" + Guid.NewGuid().ToString("N"));
            string stagedWorkbook = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destinationPath)), ".NavPro-ClashReport-" + Guid.NewGuid().ToString("N") + ".xlsx");
            Directory.CreateDirectory(imageDirectory);
            try
            {
                var rows = new List<ClashReportRow>();
                int imageIndex = 0;
                var testsData = clash.TestsData;
                foreach (var saved in FlattenTests(testsData.Tests))
                {
                    CheckHost();
                    var test = saved as ClashTest;
                    if (test == null) continue;
                    foreach (var result in LeafResults(test.Children))
                    {
                        CheckHost();
                        var representative = result as ClashResult ?? result.RepresentativeResult;
                        var center = result.Center;
                        var row = new ClashReportRow
                        {
                            TestName = test.DisplayName,
                            TestType = test.TestType.ToString(),
                            TestTolerance = test.Tolerance,
                            TestStatus = test.Status.ToString(),
                            ClashName = result.DisplayName,
                            ClashStatus = result.Status.ToString(),
                            Description = result.Description,
                            ItemA = representative == null || representative.Item1 == null ? "" : representative.Item1.DisplayName,
                            ItemB = representative == null || representative.Item2 == null ? "" : representative.Item2.DisplayName,
                            Distance = result.Distance,
                            CenterX = center.X,
                            CenterY = center.Y,
                            CenterZ = center.Z,
#if NAVISWORKS2026
                            ApprovedBy = result.ApprovedBy == null ? "" : result.ApprovedBy.DisplayName,
                            AssignedTo = result.AssignedTo == null ? "" : result.AssignedTo.DisplayName,
#else
                            ApprovedBy = result.ApprovedBy,
                            AssignedTo = result.AssignedTo,
#endif
                            CreatedTime = result.CreatedTime.HasValue ? result.CreatedTime.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "",
                            ApprovedTime = result.ApprovedTime.HasValue ? result.ApprovedTime.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : ""
                        };
                        string imagePath = Path.Combine(imageDirectory, "clash-" + (++imageIndex).ToString("D7", CultureInfo.InvariantCulture) + ".jpg");
                        try
                        {
                            using (var bitmap = testsData.TestsImageForResult(result, ImageGenerationStyle.ScenePlusOverlay, ImageWidth, ImageHeight))
                            {
                                if (bitmap == null) row.ImageNote = "No image returned by Navisworks";
                                else { bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg); row.ImagePath = imagePath; row.ImageNote = "Embedded image"; }
                            }
                        }
                        catch (Exception ex) { row.ImageNote = "Image unavailable: " + ex.Message; }
                        rows.Add(row);
                    }
                }
                CheckHost();
                ClashReportWorkbookWriter.Write(stagedWorkbook, rows);
                CheckHost();
                if (File.Exists(destinationPath)) File.Replace(stagedWorkbook, destinationPath, null);
                else File.Move(stagedWorkbook, destinationPath);
                return rows.Count;
            }
            finally
            {
                if (File.Exists(stagedWorkbook)) File.Delete(stagedWorkbook);
                if (Directory.Exists(imageDirectory)) Directory.Delete(imageDirectory, true);
            }
        }

        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks API calls must stay on the host UI thread.");
            if (Autodesk.Navisworks.Api.Application.ActiveDocument != document) throw new InvalidOperationException("The active document changed. Reopen Clash Matrix for the current document.");
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
    }

    internal static class ClashReportWorkbookWriter
    {
        private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
        private const string DrawingMainNs = "http://schemas.openxmlformats.org/drawingml/2006/main";

        private sealed class ImageAnchor
        {
            public int RowIndex { get; set; }
            public string Path { get; set; }
        }

        private static readonly string[] Headers = {
            "Test Name", "Test Type", "Tolerance", "Test Status", "Clash Name", "Clash Status", "Description",
            "Item A", "Item B", "Distance", "Center X (Navisworks model units)", "Center Y (Navisworks model units)",
            "Center Z (Navisworks model units)", "Approved By", "Assigned To", "Created Time", "Approved Time", "Image", "Image Note"
        };

        public static void Write(string path, IList<ClashReportRow> rows)
        {
            if (rows.Count > 1048573) throw new InvalidOperationException("Excel's worksheet row limit would be exceeded by this report.");
            string fullPath = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            using (var file = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, false, Encoding.UTF8))
            {
                WriteContentTypes(archive, rows.Any(r => !string.IsNullOrEmpty(r.ImagePath)));
                WriteRootRelationships(archive);
                WriteWorkbook(archive);
                WriteWorkbookRelationships(archive);
                WriteStyles(archive);
                WriteSheet(archive, rows);
                var images = rows.Select((row, index) => new { Row = row, Index = index }).Where(x => !string.IsNullOrEmpty(x.Row.ImagePath))
                    .Select(x => new ImageAnchor { RowIndex = x.Index + 2, Path = x.Row.ImagePath }).ToList();
                if (images.Count > 0)
                {
                    WriteWorksheetRelationships(archive);
                    WriteDrawing(archive, images);
                    WriteDrawingRelationships(archive, images);
                    for (int i = 0; i < images.Count; i++)
                    {
                        var entry = archive.CreateEntry("xl/media/image" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".jpg", CompressionLevel.NoCompression);
                        using (var target = entry.Open()) using (var source = File.OpenRead(images[i].Path)) source.CopyTo(target);
                    }
                }
            }
        }

        private static void WriteContentTypes(ZipArchive archive, bool hasDrawing)
        {
            WriteXml(archive, "[Content_Types].xml", w => {
                w.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                Default(w, "rels", "application/vnd.openxmlformats-package.relationships+xml"); Default(w, "xml", "application/xml"); Default(w, "jpg", "image/jpeg");
                Override(w, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                Override(w, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                Override(w, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                if (hasDrawing) Override(w, "/xl/drawings/drawing1.xml", "application/vnd.openxmlformats-officedocument.drawing+xml");
                w.WriteEndElement();
            });
        }

        private static void WriteRootRelationships(ZipArchive archive) => WriteXml(archive, "_rels/.rels", w => {
            w.WriteStartElement("Relationships", PackageRelNs);
            Relationship(w, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml"); w.WriteEndElement();
        });

        private static void WriteWorkbook(ZipArchive archive) => WriteXml(archive, "xl/workbook.xml", w => {
            w.WriteStartElement("workbook", MainNs); w.WriteAttributeString("xmlns", "r", null, RelNs);
            w.WriteStartElement("bookViews", MainNs); w.WriteStartElement("workbookView", MainNs); w.WriteAttributeString("activeTab", "0"); w.WriteEndElement(); w.WriteEndElement();
            w.WriteStartElement("sheets", MainNs); w.WriteStartElement("sheet", MainNs); w.WriteAttributeString("name", "Clash Report"); w.WriteAttributeString("sheetId", "1"); w.WriteAttributeString("r", "id", RelNs, "rId1"); w.WriteEndElement(); w.WriteEndElement();
            w.WriteEndElement();
        });

        private static void WriteWorkbookRelationships(ZipArchive archive) => WriteXml(archive, "xl/_rels/workbook.xml.rels", w => {
            w.WriteStartElement("Relationships", PackageRelNs);
            Relationship(w, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml");
            Relationship(w, "rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml"); w.WriteEndElement();
        });

        private static void WriteStyles(ZipArchive archive) => WriteXml(archive, "xl/styles.xml", w => {
            w.WriteStartElement("styleSheet", MainNs); w.WriteAttributeString("xmlns", MainNs);
            w.WriteStartElement("numFmts", MainNs); w.WriteAttributeString("count", "1"); w.WriteStartElement("numFmt", MainNs); w.WriteAttributeString("numFmtId", "164"); w.WriteAttributeString("formatCode", "#,##0.000"); w.WriteEndElement(); w.WriteEndElement();
            w.WriteStartElement("fonts", MainNs); w.WriteAttributeString("count", "2"); w.WriteStartElement("font", MainNs); w.WriteStartElement("sz", MainNs); w.WriteAttributeString("val", "10"); w.WriteEndElement(); w.WriteStartElement("name", MainNs); w.WriteAttributeString("val", "Segoe UI"); w.WriteEndElement(); w.WriteEndElement(); w.WriteStartElement("font", MainNs); w.WriteElementString("b", MainNs, ""); w.WriteStartElement("color", MainNs); w.WriteAttributeString("rgb", "FFFFFFFF"); w.WriteEndElement(); w.WriteStartElement("sz", MainNs); w.WriteAttributeString("val", "10"); w.WriteEndElement(); w.WriteStartElement("name", MainNs); w.WriteAttributeString("val", "Segoe UI"); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
            w.WriteStartElement("fills", MainNs); w.WriteAttributeString("count", "3"); w.WriteStartElement("fill", MainNs); w.WriteStartElement("patternFill", MainNs); w.WriteAttributeString("patternType", "none"); w.WriteEndElement(); w.WriteEndElement(); w.WriteStartElement("fill", MainNs); w.WriteStartElement("patternFill", MainNs); w.WriteAttributeString("patternType", "gray125"); w.WriteEndElement(); w.WriteEndElement(); w.WriteStartElement("fill", MainNs); w.WriteStartElement("patternFill", MainNs); w.WriteAttributeString("patternType", "solid"); w.WriteStartElement("fgColor", MainNs); w.WriteAttributeString("rgb", "FF14273A"); w.WriteEndElement(); w.WriteStartElement("bgColor", MainNs); w.WriteAttributeString("indexed", "64"); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
            w.WriteStartElement("borders", MainNs); w.WriteAttributeString("count", "1"); w.WriteStartElement("border", MainNs); foreach (var side in new[] { "left", "right", "top", "bottom", "diagonal" }) w.WriteElementString(side, MainNs, ""); w.WriteEndElement(); w.WriteEndElement();
            w.WriteStartElement("cellStyleXfs", MainNs); w.WriteAttributeString("count", "1"); w.WriteStartElement("xf", MainNs); w.WriteAttributeString("numFmtId", "0"); w.WriteAttributeString("fontId", "0"); w.WriteAttributeString("fillId", "0"); w.WriteAttributeString("borderId", "0"); w.WriteEndElement(); w.WriteEndElement();
            w.WriteStartElement("cellXfs", MainNs); w.WriteAttributeString("count", "3");
            Xf(w, "0", "0", "0", "0", false); Xf(w, "0", "1", "2", "0", true); Xf(w, "164", "0", "0", "0", false); w.WriteEndElement();
            w.WriteStartElement("cellStyles", MainNs); w.WriteAttributeString("count", "1"); w.WriteStartElement("cellStyle", MainNs); w.WriteAttributeString("name", "Normal"); w.WriteAttributeString("xfId", "0"); w.WriteAttributeString("builtinId", "0"); w.WriteEndElement(); w.WriteEndElement();
            w.WriteEndElement();
        });

        private static void WriteSheet(ZipArchive archive, IList<ClashReportRow> rows)
        {
            int last = rows.Count + 2;
            var images = rows.Any(r => !string.IsNullOrEmpty(r.ImagePath));
            WriteXml(archive, "xl/worksheets/sheet1.xml", w => {
                w.WriteStartElement("worksheet", MainNs); w.WriteAttributeString("xmlns", "r", null, RelNs);
                w.WriteStartElement("dimension", MainNs); w.WriteAttributeString("ref", "A1:S" + Math.Max(last, 2).ToString(CultureInfo.InvariantCulture)); w.WriteEndElement();
                w.WriteStartElement("sheetViews", MainNs); w.WriteStartElement("sheetView", MainNs); w.WriteAttributeString("workbookViewId", "0"); w.WriteStartElement("pane", MainNs); w.WriteAttributeString("ySplit", "2"); w.WriteAttributeString("topLeftCell", "A3"); w.WriteAttributeString("activePane", "bottomLeft"); w.WriteAttributeString("state", "frozen"); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
                w.WriteStartElement("sheetFormatPr", MainNs); w.WriteAttributeString("defaultRowHeight", "18"); w.WriteEndElement();
                WriteColumns(w);
                w.WriteStartElement("sheetData", MainNs);
                w.WriteStartElement("row", MainNs); w.WriteAttributeString("r", "1"); w.WriteAttributeString("ht", "25"); w.WriteAttributeString("customHeight", "1"); WriteTextCell(w, "A1", "NavPro Clash Report  ·  " + rows.Count.ToString("N0", CultureInfo.InvariantCulture) + " clash results  ·  Coordinates are result center points in Navisworks model units  ·  Exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), 1); w.WriteEndElement();
                w.WriteStartElement("row", MainNs); w.WriteAttributeString("r", "2"); w.WriteAttributeString("ht", "42"); w.WriteAttributeString("customHeight", "1");
                for (int i = 0; i < Headers.Length; i++) WriteTextCell(w, ColumnName(i) + "2", Headers[i], 1);
                w.WriteEndElement();
                for (int index = 0; index < rows.Count; index++) WriteDataRow(w, rows[index], index + 3);
                w.WriteEndElement();
                w.WriteStartElement("autoFilter", MainNs); w.WriteAttributeString("ref", "A2:S" + Math.Max(last, 2).ToString(CultureInfo.InvariantCulture)); w.WriteEndElement();
                w.WriteStartElement("mergeCells", MainNs); w.WriteAttributeString("count", "1"); w.WriteStartElement("mergeCell", MainNs); w.WriteAttributeString("ref", "A1:S1"); w.WriteEndElement(); w.WriteEndElement();
                if (images) { w.WriteStartElement("drawing", MainNs); w.WriteAttributeString("r", "id", RelNs, "rId1"); w.WriteEndElement(); }
                w.WriteEndElement();
            });
        }

        private static void WriteColumns(XmlWriter w)
        {
            double[] widths = { 28, 18, 13, 16, 32, 16, 45, 32, 32, 14, 22, 22, 22, 22, 22, 21, 21, 28, 28 };
            w.WriteStartElement("cols", MainNs);
            for (int i = 0; i < widths.Length; i++) { w.WriteStartElement("col", MainNs); w.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("width", widths[i].ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("customWidth", "1"); w.WriteEndElement(); }
            w.WriteEndElement();
        }

        private static void WriteDataRow(XmlWriter w, ClashReportRow row, int rowNumber)
        {
            w.WriteStartElement("row", MainNs); w.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("ht", "76"); w.WriteAttributeString("customHeight", "1");
            string[] text = { row.TestName, row.TestType, null, row.TestStatus, row.ClashName, row.ClashStatus, row.Description, row.ItemA, row.ItemB, null, null, null, null, row.ApprovedBy, row.AssignedTo, row.CreatedTime, row.ApprovedTime, "", row.ImageNote };
            for (int i = 0; i < text.Length; i++) if (text[i] != null) WriteTextCell(w, ColumnName(i) + rowNumber.ToString(CultureInfo.InvariantCulture), text[i], 0);
            WriteNumberCell(w, "C" + rowNumber.ToString(CultureInfo.InvariantCulture), row.TestTolerance);
            WriteNumberCell(w, "J" + rowNumber.ToString(CultureInfo.InvariantCulture), row.Distance);
            WriteNumberCell(w, "K" + rowNumber.ToString(CultureInfo.InvariantCulture), row.CenterX);
            WriteNumberCell(w, "L" + rowNumber.ToString(CultureInfo.InvariantCulture), row.CenterY);
            WriteNumberCell(w, "M" + rowNumber.ToString(CultureInfo.InvariantCulture), row.CenterZ);
            w.WriteEndElement();
        }

        private static void WriteWorksheetRelationships(ZipArchive archive) => WriteXml(archive, "xl/worksheets/_rels/sheet1.xml.rels", w => {
            w.WriteStartElement("Relationships", PackageRelNs);
            Relationship(w, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing", "../drawings/drawing1.xml"); w.WriteEndElement();
        });

        private static void WriteDrawing(ZipArchive archive, IList<ImageAnchor> images) => WriteXml(archive, "xl/drawings/drawing1.xml", w => {
            w.WriteStartElement("xdr", "wsDr", DrawingNs); w.WriteAttributeString("xmlns", "xdr", null, DrawingNs); w.WriteAttributeString("xmlns", "a", null, DrawingMainNs); w.WriteAttributeString("xmlns", "r", null, RelNs);
            for (int i = 0; i < images.Count; i++)
            {
                w.WriteStartElement("xdr", "twoCellAnchor", DrawingNs); w.WriteAttributeString("editAs", "oneCell");
                WriteMarker(w, "from", 17, images[i].RowIndex); WriteMarker(w, "to", 18, images[i].RowIndex + 1);
                w.WriteStartElement("xdr", "pic", DrawingNs);
                w.WriteStartElement("xdr", "nvPicPr", DrawingNs); w.WriteStartElement("xdr", "cNvPr", DrawingNs); w.WriteAttributeString("id", (i + 1).ToString(CultureInfo.InvariantCulture)); w.WriteAttributeString("name", "Clash image " + (i + 1).ToString(CultureInfo.InvariantCulture)); w.WriteEndElement(); w.WriteStartElement("xdr", "cNvPicPr", DrawingNs); w.WriteStartElement("a", "picLocks", DrawingMainNs); w.WriteAttributeString("noChangeAspect", "1"); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
                w.WriteStartElement("xdr", "blipFill", DrawingNs); w.WriteStartElement("a", "blip", DrawingMainNs); w.WriteAttributeString("r", "embed", RelNs, "rId" + (i + 1).ToString(CultureInfo.InvariantCulture)); w.WriteEndElement(); w.WriteStartElement("a", "stretch", DrawingMainNs); w.WriteStartElement("a", "fillRect", DrawingMainNs); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
                w.WriteStartElement("xdr", "spPr", DrawingNs); w.WriteStartElement("a", "xfrm", DrawingMainNs); w.WriteStartElement("a", "off", DrawingMainNs); w.WriteAttributeString("x", "0"); w.WriteAttributeString("y", "0"); w.WriteEndElement(); w.WriteStartElement("a", "ext", DrawingMainNs); w.WriteAttributeString("cx", "1524000"); w.WriteAttributeString("cy", "857250"); w.WriteEndElement(); w.WriteEndElement(); w.WriteStartElement("a", "prstGeom", DrawingMainNs); w.WriteAttributeString("prst", "rect"); w.WriteStartElement("a", "avLst", DrawingMainNs); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
                w.WriteEndElement(); w.WriteStartElement("xdr", "clientData", DrawingNs); w.WriteEndElement(); w.WriteEndElement();
            }
            w.WriteEndElement();
        });

        private static void WriteDrawingRelationships(ZipArchive archive, IList<ImageAnchor> images) => WriteXml(archive, "xl/drawings/_rels/drawing1.xml.rels", w => {
            w.WriteStartElement("Relationships", PackageRelNs);
            for (int i = 0; i < images.Count; i++) Relationship(w, "rId" + (i + 1).ToString(CultureInfo.InvariantCulture), "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", "../media/image" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".jpg");
            w.WriteEndElement();
        });

        private static void WriteMarker(XmlWriter w, string marker, int column, int row)
        {
            w.WriteStartElement("xdr", marker, DrawingNs);
            w.WriteElementString("xdr", "col", DrawingNs, column.ToString(CultureInfo.InvariantCulture)); w.WriteElementString("xdr", "colOff", DrawingNs, "0");
            w.WriteElementString("xdr", "row", DrawingNs, row.ToString(CultureInfo.InvariantCulture)); w.WriteElementString("xdr", "rowOff", DrawingNs, "0"); w.WriteEndElement();
        }

        private static void WriteTextCell(XmlWriter w, string address, string value, int style)
        {
            w.WriteStartElement("c", MainNs); w.WriteAttributeString("r", address); w.WriteAttributeString("t", "inlineStr"); if (style != 0) w.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));
            w.WriteStartElement("is", MainNs); w.WriteStartElement("t", MainNs); if (value != null && (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]))) || (value ?? "").Contains("\n")) w.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve"); w.WriteString(value ?? ""); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement();
        }

        private static void WriteNumberCell(XmlWriter w, string address, double value)
        {
            w.WriteStartElement("c", MainNs); w.WriteAttributeString("r", address); w.WriteAttributeString("s", "2"); w.WriteStartElement("v", MainNs); w.WriteString(double.IsNaN(value) || double.IsInfinity(value) ? "0" : value.ToString("R", CultureInfo.InvariantCulture)); w.WriteEndElement(); w.WriteEndElement();
        }

        private static void WriteXml(ZipArchive archive, string path, Action<XmlWriter> write)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open()) using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CloseOutput = false })) write(writer);
        }

        private static void Default(XmlWriter w, string extension, string contentType) { w.WriteStartElement("Default", "http://schemas.openxmlformats.org/package/2006/content-types"); w.WriteAttributeString("Extension", extension); w.WriteAttributeString("ContentType", contentType); w.WriteEndElement(); }
        private static void Override(XmlWriter w, string partName, string contentType) { w.WriteStartElement("Override", "http://schemas.openxmlformats.org/package/2006/content-types"); w.WriteAttributeString("PartName", partName); w.WriteAttributeString("ContentType", contentType); w.WriteEndElement(); }
        private static void Relationship(XmlWriter w, string id, string type, string target) { w.WriteStartElement("Relationship", PackageRelNs); w.WriteAttributeString("Id", id); w.WriteAttributeString("Type", type); w.WriteAttributeString("Target", target); w.WriteEndElement(); }
        private static void Xf(XmlWriter w, string numFmt, string font, string fill, string border, bool apply)
        {
            w.WriteStartElement("xf", MainNs); w.WriteAttributeString("numFmtId", numFmt); w.WriteAttributeString("fontId", font); w.WriteAttributeString("fillId", fill); w.WriteAttributeString("borderId", border); w.WriteAttributeString("xfId", "0");
            if (numFmt != "0") w.WriteAttributeString("applyNumberFormat", "1"); if (font != "0") w.WriteAttributeString("applyFont", "1"); if (fill != "0") w.WriteAttributeString("applyFill", "1");
            if (apply) { w.WriteAttributeString("applyAlignment", "1"); w.WriteStartElement("alignment", MainNs); w.WriteAttributeString("vertical", "center"); w.WriteAttributeString("wrapText", "1"); w.WriteEndElement(); }
            w.WriteEndElement();
        }
        private static string ColumnName(int index)
        {
            int n = index + 1; var name = new StringBuilder();
            while (n > 0) { int r = (n - 1) % 26; name.Insert(0, (char)('A' + r)); n = (n - 1) / 26; }
            return name.ToString();
        }
    }
}
