using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NavPro.Backend.Core;

namespace NavPro.Backend.Services
{
    public sealed class ApprovedSeparationRule
    {
        public string Name, SetA, SetB, Units, Source, Revision;
        public double Gap;
    }
    public static class ApprovedRuleTable
    {
        public const string Header = "Rule,SetA,SetB,Gap,Units,Source,Revision,Approved,CheckType";
        public static List<ApprovedSeparationRule> Read(string path)
        {
            if (new FileInfo(path).Length > 10000000) throw new InvalidDataException("Rule table must be smaller than 10 MB.");
            var rows = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? ReviewCsv.Read(File.ReadAllText(path)) : ReadWorkbook(path);
            if (rows.Count < 2 || rows.Count > 5001) throw new InvalidDataException("Supply a header and 1–5000 approved rules.");
            var required = Header.Split(',');
            var headers = rows[0].Select(v => v.Trim()).ToList();
            if (required.Any(h => headers.Count(v => v.Equals(h, StringComparison.OrdinalIgnoreCase)) != 1)) throw new InvalidDataException("Required unique columns: " + Header);
            var output = new List<ApprovedSeparationRule>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows.Skip(1))
            {
                Func<string, string> cell = name => { int i = headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase)); return i < row.Count ? row[i].Trim() : ""; };
                if (required.Any(h => string.IsNullOrWhiteSpace(cell(h)))) throw new InvalidDataException("Every rule needs all columns, including source, revision and approval.");
                if (!cell("Approved").Equals("Yes", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Only Approved=Yes rows can be imported.");
                if (!cell("CheckType").Equals("3D", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Only verified native 3D surface-gap checks are supported. Horizontal, vertical and crossing geometry require separate validation; they are never treated as 3D equivalents.");
                if (!double.TryParse(cell("Gap"), NumberStyles.Float, CultureInfo.InvariantCulture, out var gap) || double.IsNaN(gap) || double.IsInfinity(gap) || gap <= 0) throw new InvalidDataException("Gap must be a positive finite number.");
                if (!names.Add(cell("Rule"))) throw new InvalidDataException("Duplicate rule name: " + cell("Rule"));
                output.Add(new ApprovedSeparationRule { Name = cell("Rule"), SetA = cell("SetA"), SetB = cell("SetB"), Gap = gap, Units = cell("Units"), Source = cell("Source"), Revision = cell("Revision") });
            }
            return output;
        }
        private static XDocument Xml(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name) ?? throw new InvalidDataException("Missing workbook part: " + name);
            if (entry.Length > 20000000) throw new InvalidDataException("Workbook part exceeds 20 MB.");
            using (var stream = entry.Open()) using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20000000 })) return XDocument.Load(reader);
        }
        internal static List<List<string>> ReadWorkbook(string path)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main", rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            using (var stream = File.OpenRead(path)) using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var sheet = Xml(zip, "xl/workbook.xml").Descendants(ns + "sheet").FirstOrDefault() ?? throw new InvalidDataException("Workbook has no sheet.");
                string id = (string)sheet.Attribute(rel + "id");
                var link = Xml(zip, "xl/_rels/workbook.xml.rels").Root.Elements().SingleOrDefault(e => (string)e.Attribute("Id") == id) ?? throw new InvalidDataException("Missing worksheet relationship.");
                if ((string)link.Attribute("TargetMode") == "External") throw new InvalidDataException("External worksheet references are unsupported.");
                string target = (string)link.Attribute("Target");
                var uri = new Uri(new Uri("https://workbook.invalid/xl/workbook.xml"), target);
                if (uri.Host != "workbook.invalid" || !uri.AbsolutePath.StartsWith("/xl/worksheets/", StringComparison.Ordinal)) throw new InvalidDataException("Unsupported worksheet location.");
                var shared = zip.GetEntry("xl/sharedStrings.xml") == null ? new List<string>() : Xml(zip, "xl/sharedStrings.xml").Descendants(ns + "si").Select(e => string.Concat(e.Descendants(ns + "t").Select(t => t.Value))).ToList();
                var output = new List<List<string>>();
                foreach (var row in Xml(zip, uri.AbsolutePath.TrimStart('/')).Descendants(ns + "row"))
                {
                    var values = Enumerable.Repeat("", 9).ToList();
                    foreach (var cell in row.Elements(ns + "c"))
                    {
                        string reference = (string)cell.Attribute("r") ?? ""; int column = 0;
                        foreach (char c in reference.TakeWhile(char.IsLetter)) { column = column * 26 + char.ToUpperInvariant(c) - 'A' + 1; if (column > 1000) throw new InvalidDataException("Invalid worksheet column."); }
                        if (column < 1 || column > 9) continue;
                        if (cell.Element(ns + "f") != null) throw new InvalidDataException("Rule values must be literal cells. Replace formulas with approved values.");
                        string value = cell.Element(ns + "v")?.Value ?? string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
                        if ((string)cell.Attribute("t") == "s") { if (!int.TryParse(value, out int i) || i < 0 || i >= shared.Count) throw new InvalidDataException("Invalid shared string."); value = shared[i]; }
                        values[column - 1] = value;
                    }
                    if (values.Any(v => v.Length > 0)) output.Add(values);
                }
                return output;
            }
        }
    }
}
