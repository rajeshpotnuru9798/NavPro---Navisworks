using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NavPro.Backend.Core;

namespace NavPro.Backend.Services
{
    /// <summary>Exact horizontal lines/circular arcs. Unsupported geometry is rejected, never silently tessellated.</summary>
    public static class AlignmentDataReader
    {
        private static double Number(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("Invalid alignment number: '" + text + "'. Use invariant decimal numbers.");
            return value;
        }
        public static List<HorizontalAlignment> ReadLandXml(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 50000000 }))
            {
                var xml = XDocument.Load(reader); var root = xml.Root;
                if (root == null || root.Name.LocalName != "LandXML") throw new InvalidOperationException("Choose a LandXML alignment file.");
                var units = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Units")?.Elements().SingleOrDefault();
                string unit = (string)units?.Attribute("linearUnit"); double factor = UnitFactor(unit);
                var output = new List<HorizontalAlignment>();
                foreach (var element in root.Descendants().Where(e => e.Name.LocalName == "Alignment"))
                {
                    string name = (string)element.Attribute("name");
                    var alignment = new HorizontalAlignment { Name = name, Units = unit, MetersPerUnit = factor };
                    double station = Number((string)element.Attribute("staStart") ?? "0");
                    var geometry = element.Elements().SingleOrDefault(e => e.Name.LocalName == "CoordGeom")
                        ?? throw new InvalidOperationException("Alignment '" + name + "' has no CoordGeom.");
                    foreach (var segment in geometry.Elements())
                    {
                        string kind = segment.Name.LocalName;
                        if (kind != "Line" && kind != "Curve") throw new InvalidOperationException("Alignment '" + name + "': " + kind + " is unsupported. Supply lines/circular arcs; spirals are not approximated.");
                        if (segment.Attribute("staStart") != null && Math.Abs(Number((string)segment.Attribute("staStart")) - station) > 1e-4)
                            throw new InvalidOperationException("Alignment segment station disagrees with its geometry.");
                        var start = Point(segment, "Start"); var end = Point(segment, "End");
                        HorizontalSegment native;
                        if (kind == "Line") native = HorizontalSegment.Line(start, end, station);
                        else
                        {
                            string rotation = (string)segment.Attribute("rot");
                            if (rotation != "cw" && rotation != "ccw") throw new InvalidOperationException("Curve rotation must be cw or ccw.");
                            native = HorizontalSegment.Arc(start, end, Point(segment, "Center"), station, rotation == "cw");
                            if (segment.Attribute("radius") != null && Math.Abs(Number((string)segment.Attribute("radius")) - native.Radius) > Math.Max(1e-4, native.Radius * 1e-6))
                                throw new InvalidOperationException("Curve radius disagrees with Start/Center/End coordinates.");
                        }
                        if (segment.Attribute("length") != null && Math.Abs(Number((string)segment.Attribute("length")) - native.Length) > Math.Max(1e-4, native.Length * 1e-6))
                            throw new InvalidOperationException("Segment length disagrees with horizontal geometry. Check elevation/arc definition.");
                        alignment.Segments.Add(native); station += native.Length;
                    }
                    if (element.Attribute("length") != null && Math.Abs(Number((string)element.Attribute("length")) - alignment.Segments.Sum(s => s.Length)) > Math.Max(1e-4, alignment.Segments.Sum(s => s.Length) * 1e-6))
                        throw new InvalidOperationException("Alignment declared length disagrees with its geometry.");
                    foreach (var equation in element.Elements().Where(e => e.Name.LocalName == "StaEquation"))
                    {
                        string direction = (string)equation.Attribute("staIncrement");
                        if (!string.IsNullOrEmpty(direction) && direction != "increasing") throw new InvalidOperationException("Decreasing/unknown station progression needs separate geometry validation and is unsupported.");
                        alignment.Equations.Add(new StationEquation { Internal = Number((string)equation.Attribute("staInternal")), Back = Number((string)equation.Attribute("staBack")), Ahead = Number((string)equation.Attribute("staAhead")) });
                    }
                    alignment.Validate(); output.Add(alignment);
                }
                ValidateNames(output); return output;
            }
        }
        private static AlignmentPoint Point(XElement element, string name)
        {
            var child = element.Elements().SingleOrDefault(e => e.Name.LocalName == name);
            if (child == null || child.Attribute("pntRef") != null) throw new InvalidOperationException("Supply explicit " + name + " coordinates, not point references.");
            var values = child.Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < 2 || values.Length > 3) throw new InvalidOperationException(name + " must contain Northing Easting [Elevation].");
            if (values.Length == 3) Number(values[2]);
            return new AlignmentPoint(Number(values[1]), Number(values[0])); // LandXML Y, X, Z order.
        }
        public static List<HorizontalAlignment> ReadCsv(string path, string units, double metersPerUnit)
        {
            var rows = ReviewCsv.Read(File.ReadAllText(path));
            if (rows.Count < 3) throw new InvalidOperationException("CSV needs a header and at least two alignment vertices.");
            var expected = new[] { "Alignment", "Station", "Easting", "Northing" };
            if (rows[0].Count != 4 || !rows[0].SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("CSV header must be Alignment,Station,Easting,Northing. Vertices define straight segments.");
            if (rows.Skip(1).Any(row => row.Count != 4)) throw new InvalidOperationException("Each CSV row must contain four fields.");
            var output = new List<HorizontalAlignment>();
            foreach (var group in rows.Skip(1).GroupBy(row => row[0], StringComparer.OrdinalIgnoreCase))
            {
                var alignment = new HorizontalAlignment { Name = group.Key, Units = units, MetersPerUnit = metersPerUnit };
                var vertices = group.ToList(); // Preserve supplied order; never sort malformed stationing into apparent validity.
                for (int i = 1; i < vertices.Count; i++)
                {
                    var a = vertices[i - 1]; var b = vertices[i];
                    var segment = HorizontalSegment.Line(new AlignmentPoint(Number(a[2]), Number(a[3])), new AlignmentPoint(Number(b[2]), Number(b[3])), Number(a[1]));
                    if (Math.Abs(Number(b[1]) - segment.StartStation - segment.Length) > Math.Max(1e-4, segment.Length * 1e-6))
                        throw new InvalidOperationException("CSV station increments must equal straight-segment plan lengths. Use LandXML for circular curves.");
                    alignment.Segments.Add(segment);
                }
                alignment.Validate(); output.Add(alignment);
            }
            ValidateNames(output); return output;
        }
        private static void ValidateNames(List<HorizontalAlignment> alignments)
        {
            if (alignments.Count == 0 || alignments.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new InvalidOperationException("The file must contain uniquely named alignments.");
        }
        private static double UnitFactor(string unit)
        {
            switch (unit)
            {
                case "meter": return 1; case "millimeter": return .001; case "centimeter": return .01; case "kilometer": return 1000;
                case "foot": return .3048; case "USSurveyFoot": return 1200d / 3937; case "inch": return .0254;
                default: throw new InvalidOperationException("LandXML linear units are missing or unsupported: " + unit);
            }
        }
    }
}
