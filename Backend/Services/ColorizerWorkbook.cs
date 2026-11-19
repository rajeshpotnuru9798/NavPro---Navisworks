using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    /// <summary>Small dependency-free XLSX interchange for Colorizer.</summary>
    public static class ColorizerWorkbook
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static void Write(string path, IEnumerable<ColorizerColorRow> rows)
        {
            var data = (rows ?? Enumerable.Empty<ColorizerColorRow>()).ToList();
            if (data.Count == 0) throw new InvalidOperationException("There are no color assignments to export.");
            using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Add(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                Add(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Add(zip, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Colors\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Add(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                var sheet = new XElement(Main + "worksheet", new XAttribute(XNamespace.Xmlns + "r", Rel),
                    new XElement(Main + "sheetData",
                        Row(1, new[] { "Target Type", "Target Name", "Target Key", "Color Hex", "Red", "Green", "Blue" }),
                        data.Select((row, i) => Row(i + 2, new[] { row.TargetType, row.TargetName, row.TargetKey, row.Hex,
                            row.Red.ToString(CultureInfo.InvariantCulture), row.Green.ToString(CultureInfo.InvariantCulture), row.Blue.ToString(CultureInfo.InvariantCulture) }))));
                Add(zip, "xl/worksheets/sheet1.xml", new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), sheet).ToString(SaveOptions.DisableFormatting));
            }
        }

        public static List<ColorizerColorRow> Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The selected workbook could not be found.", path);
            using (var file = File.OpenRead(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                var workbook = XDocument.Load(Open(zip, "xl/workbook.xml"));
                var rels = XDocument.Load(Open(zip, "xl/_rels/workbook.xml.rels"));
                var sheet = workbook.Descendants(Main + "sheet").FirstOrDefault(element => string.Equals((string)element.Attribute("name"), "Colors", StringComparison.OrdinalIgnoreCase))
                    ?? workbook.Descendants(Main + "sheet").FirstOrDefault();
                if (sheet == null) throw new InvalidDataException("The workbook has no worksheet.");
                var relId = (string)sheet.Attribute(Rel + "id");
                var rel = rels.Root.Elements(PkgRel + "Relationship").FirstOrDefault(element => (string)element.Attribute("Id") == relId);
                if (rel == null) throw new InvalidDataException("The Colors worksheet could not be located.");
                var target = (string)rel.Attribute("Target");
                var sheetPath = target.StartsWith("/", StringComparison.Ordinal) ? target.TrimStart('/') : "xl/" + target.Replace("..", "");
                var sheetDoc = XDocument.Load(Open(zip, sheetPath));
                var shared = ReadSharedStrings(zip);
                var grid = new SortedDictionary<int, string[]>();
                foreach (var row in sheetDoc.Descendants(Main + "row"))
                {
                    int rowNumber; if (!int.TryParse((string)row.Attribute("r"), out rowNumber)) continue;
                    var values = new string[7];
                    foreach (var cell in row.Elements(Main + "c"))
                    {
                        int column = ColumnIndex((string)cell.Attribute("r")); if (column < 0 || column >= values.Length) continue;
                        var type = (string)cell.Attribute("t");
                        var value = cell.Element(Main + "v")?.Value ?? cell.Element(Main + "is")?.Value ?? string.Empty;
                        int sharedIndex;
                        if (type == "s" && int.TryParse(value, out sharedIndex) && sharedIndex >= 0 && sharedIndex < shared.Count) value = shared[sharedIndex];
                        values[column] = value;
                    }
                    grid[rowNumber] = values;
                }
                var header = grid.FirstOrDefault(pair => pair.Value.Any(value => string.Equals(value?.Trim(), "Target Name", StringComparison.OrdinalIgnoreCase)));
                if (header.Value == null) throw new InvalidDataException("The workbook must contain a Target Name column.");
                var columns = header.Value.Select((value, index) => new { Name = value?.Trim() ?? string.Empty, Index = index })
                    .Where(item => item.Name.Length > 0).ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);
                int typeCol = Find(columns, "Target Type", -1), nameCol = Find(columns, "Target Name", -1), keyCol = Find(columns, "Target Key", -1);
                int hexCol = Find(columns, "Color Hex", -1), rCol = Find(columns, "Red", -1), gCol = Find(columns, "Green", -1), bCol = Find(columns, "Blue", -1);
                if (hexCol < 0 && (rCol < 0 || gCol < 0 || bCol < 0)) throw new InvalidDataException("Provide a Color Hex column or all three Red, Green and Blue columns.");
                var output = new List<ColorizerColorRow>();
                foreach (var pair in grid.Where(pair => pair.Key > header.Key))
                {
                    var cells = pair.Value;
                    string name = Cell(cells, nameCol).Trim(); if (name.Length == 0) continue;
                    byte red, green, blue;
                    string hex = Cell(cells, hexCol).Trim();
                    if (hex.Length > 0)
                    {
                        if (hex[0] == '#') hex = hex.Substring(1);
                        if (hex.Length != 6 || !byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red)
                            || !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green)
                            || !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue))
                            throw new InvalidDataException("Invalid Color Hex for '" + name + "' at worksheet row " + pair.Key + ". Use #RRGGBB.");
                    }
                    else if (!byte.TryParse(Cell(cells, rCol), out red) || !byte.TryParse(Cell(cells, gCol), out green) || !byte.TryParse(Cell(cells, bCol), out blue))
                        throw new InvalidDataException("Invalid RGB value for '" + name + "' at worksheet row " + pair.Key + ". Values must be from 0 to 255.");
                    output.Add(new ColorizerColorRow { TargetType = Cell(cells, typeCol).Trim(), TargetName = name, TargetKey = Cell(cells, keyCol).Trim(), Red = red, Green = green, Blue = blue });
                }
                if (output.Count == 0) throw new InvalidDataException("No target color rows were found in the workbook.");
                return output;
            }
        }

        private static XElement Row(int index, IEnumerable<string> values) => new XElement(Main + "row", new XAttribute("r", index), values.Select((value, col) => new XElement(Main + "c", new XAttribute("r", ColumnName(col) + index), new XAttribute("t", "inlineStr"), new XElement(Main + "is", new XElement(Main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? string.Empty)))));
        private static void Add(ZipArchive zip, string name, string content) { var entry = zip.CreateEntry(name, CompressionLevel.Optimal); using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(content); }
        private static System.Xml.XmlReader Open(ZipArchive zip, string name) { var entry = zip.GetEntry(name); if (entry == null) throw new InvalidDataException("Workbook component missing: " + name); return System.Xml.XmlReader.Create(entry.Open()); }
        private static List<string> ReadSharedStrings(ZipArchive zip) { var entry = zip.GetEntry("xl/sharedStrings.xml"); if (entry == null) return new List<string>(); using (var reader = System.Xml.XmlReader.Create(entry.Open())) { var doc = XDocument.Load(reader); return doc.Descendants(Main + "si").Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value))).ToList(); } }
        private static int Find(Dictionary<string, int> columns, string name, int fallback) { int index; return columns.TryGetValue(name, out index) ? index : fallback; }
        private static string Cell(string[] values, int index) => index < 0 || index >= values.Length ? string.Empty : values[index] ?? string.Empty;
        private static int ColumnIndex(string reference) { if (string.IsNullOrEmpty(reference)) return -1; int value = 0, i = 0; while (i < reference.Length && char.IsLetter(reference[i])) { value = value * 26 + char.ToUpperInvariant(reference[i]) - 'A' + 1; i++; } return value - 1; }
        private static string ColumnName(int index) { var result = string.Empty; for (int value = index + 1; value > 0; value = (value - 1) / 26) result = (char)('A' + (value - 1) % 26) + result; return result; }
    }
}
