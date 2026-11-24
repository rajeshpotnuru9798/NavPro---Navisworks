using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;
using HorizontalAlignment = NavPro.Backend.Core.HorizontalAlignment;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ChainageClashLocatorDialog : ReviewToolForm
    {
        private readonly ComboBox alignments = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270 };
        private readonly ComboBox units = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        private readonly NumericUpDown maximum = new NumericUpDown { DecimalPlaces = 3, Minimum = .001M, Maximum = 1000000, Value = 100, Width = 110 };
        private readonly CheckBox coordinates = new CheckBox { AutoSize = true, Text = "Alignment and model use the same XY origin, axes and coordinate system" };
        private readonly NumericUpDown east = TransformField(0), north = TransformField(0), rotation = TransformField(0), scale = TransformField(1);
        private static NumericUpDown TransformField(decimal value) => new NumericUpDown { Minimum = -1000000000, Maximum = 1000000000, DecimalPlaces = 6, Value = value, Width = 100 };
        private ReviewSnapshot snapshot;
        private readonly Button export;
        public ChainageClashLocatorDialog(Document document) : this(new ClashReviewSession(document)) { }
        internal ChainageClashLocatorDialog(ClashReviewSession session) : base("Chainage-Based Clash Locator", session)
        {
            foreach (Units value in Enum.GetValues(typeof(Units))) units.Items.Add(value); units.SelectedItem = Units.Meters;
            Option("CSV units", units); Option("Alignment", alignments); Option("Max XY search distance", maximum); Options.Controls.Add(coordinates);
            coordinates.Text = "I verified the model → alignment transform below";
            Option("E translation", east); Option("N translation", north); Option("CCW rotation °", rotation); Option("XY scale", scale);
            foreach (var input in new[] { east, north, rotation, scale }) input.ValueChanged += (s, e) => { coordinates.Checked = false; InvalidateWork("Transform changed. Verify coordinates and locate again."); };
            Help.Text = "Import validated LandXML lines/arcs/station equations or CSV. Model XY is converted to alignment units, scaled, rotated counterclockwise, then translated in alignment units. Check known control points before confirming. Offset positive right; Z is unit-converted only. Spirals require separately validated geometry and are rejected.";
            AddButton(Actions, "Visualize alignment", () => Guard(Visualize));
            AddButton(Actions, "Import alignment", () => Guard(Import)); AddButton(Actions, "Save CSV template", SaveTemplate);
            AddButton(Actions, "Locate selected tests", () => Guard(Locate));
            AddButton(Actions, "Show clash", () => Guard(() => { Session.Validate(snapshot); if (Results.CurrentRow?.Tag is ReviewClash row) Session.ShowClash(row); }));
            export = AddButton(Actions, "Export locations CSV", () => Guard(() => { Session.Validate(snapshot); ExportGrid(); })); export.Enabled = false;
            Column("Test", 180); Column("Clash", 150); Column("Alignment", 150); Column("Chainage", 110); Column("Signed offset", 110); Column("XY distance", 110);
            Column("Clash elevation", 110); Column("Units", 100); Column("Check", 340);
            alignments.SelectedIndexChanged += (s, e) => InvalidateWork("Alignment changed. Locate again."); maximum.ValueChanged += (s, e) => InvalidateWork("Search distance changed. Locate again.");
            coordinates.CheckedChanged += (s, e) => InvalidateWork("Coordinate confirmation changed. Locate again.");
            units.SelectedIndexChanged += (s, e) => { alignments.Items.Clear(); InvalidateWork("CSV input units changed. Reimport the alignment."); };
            FinishLayout();
        }
        protected override void InvalidateWork(string message) { snapshot = null; Results.Rows.Clear(); if (export != null) export.Enabled = false; base.InvalidateWork(message); }
        protected override void OnHostInvalidated(string message)
        { coordinates.Checked = false; base.OnHostInvalidated(message); }
        private void Import()
        {
            using (var file = new OpenFileDialog { Filter = "Alignment data (*.xml;*.landxml;*.csv)|*.xml;*.landxml;*.csv" })
            {
                if (file.ShowDialog(this) != DialogResult.OK) return;
                if (new FileInfo(file.FileName).Length > 50000000) throw new InvalidOperationException("Choose an alignment file smaller than 50 MB.");
                // Clear old data before parsing, so a failed import can never leave a previous alignment silently active.
                alignments.Items.Clear(); coordinates.Checked = false; InvalidateWork("Loading alignment data…");
                var data = Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                    ? AlignmentDataReader.ReadCsv(file.FileName, units.SelectedItem.ToString(), UnitConversion.ScaleFactor((Units)units.SelectedItem, Units.Meters))
                    : AlignmentDataReader.ReadLandXml(file.FileName);
                alignments.Items.AddRange(data.Cast<object>().ToArray()); alignments.SelectedIndex = 0;
                Status.Text = "Loaded " + data.Count + " alignment(s). Confirm shared coordinates; maximum distance and output use alignment units.";
            }
        }
        private void Locate()
        {
            if (!(alignments.SelectedItem is HorizontalAlignment alignment)) throw new InvalidOperationException("Import and choose an alignment.");
            if (!coordinates.Checked) throw new InvalidOperationException("Verify known control points and confirm the configured model-to-alignment transform. Model units are converted before XY scaling, rotation and translation.");
            InvalidateWork("Locating…"); snapshot = Session.ReadSnapshot(SelectedTestIds);
            double factor = UnitConversion.ScaleFactor(Session.Document.Units, Units.Meters) / alignment.MetersPerUnit;
            foreach (var row in snapshot.Clashes)
            {
                var point = AlignmentCoordinates.Transform(row.X * factor, row.Y * factor, (double)scale.Value, (double)rotation.Value, (double)east.Value, (double)north.Value);
                var location = alignment.Locate(point.E, point.N, (double)maximum.Value);
                bool ambiguous = location.Note.StartsWith("Ambiguous", StringComparison.Ordinal), outside = location.Note.StartsWith("Outside", StringComparison.Ordinal);
                int index = Results.Rows.Add(row.TestName, row.Name, alignment.Name, ambiguous || outside ? "" : AlignmentCoordinates.Station(alignment.DisplayStation(location.Station)),
                    ambiguous || outside ? "" : Format(location.Offset), Format(location.Distance), Format(row.Z * factor), alignment.Units, location.Note);
                Results.Rows[index].Tag = row;
            }
            export.Enabled = Results.Rows.Count > 0; Status.Text = "Located " + Results.Rows.Count + " clash centres against " + alignment.Name + ". Review ambiguous/out-of-range/endpoint rows; no model values changed.";
        }
        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private void Visualize()
        {
            if (!(alignments.SelectedItem is HorizontalAlignment alignment)) throw new InvalidOperationException("Import an alignment first.");
            alignment.Validate();
            var points = alignment.Segments.SelectMany(s => Enumerable.Range(0, 65).Select(i => s.PointAt(s.Length * i / 64))).ToArray();
            using (var dialog = new Form { Name = "AlignmentPlanPreview", Text = "Alignment plan — " + alignment.Name + " (E right, N up; schematic preview)", Width = 1000, Height = 700 })
            {
                var canvas = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.White };
                canvas.Paint += (s, e) => {
                    double minE = points.Min(p => p.E), minN = points.Min(p => p.N), width = Math.Max(1, points.Max(p => p.E) - minE), height = Math.Max(1, points.Max(p => p.N) - minN);
                    double factor = Math.Min(Math.Max(1, canvas.Width - 60) / width, Math.Max(1, canvas.Height - 60) / height);
                    var screen = points.Select(p => new System.Drawing.PointF((float)(30 + (p.E - minE) * factor), (float)(canvas.Height - 30 - (p.N - minN) * factor))).ToArray();
                    e.Graphics.DrawLines(System.Drawing.Pens.DarkMagenta, screen); e.Graphics.DrawString("Start " + AlignmentCoordinates.Station(alignment.DisplayStation(alignment.Segments[0].StartStation)), Font, System.Drawing.Brushes.Black, screen[0]);
                    e.Graphics.DrawString("End", Font, System.Drawing.Brushes.Black, screen[screen.Length - 1]);
                };
                canvas.Resize += (s, e) => canvas.Invalidate(); dialog.Controls.Add(canvas); DialogTheme.Apply(dialog); dialog.ShowDialog(this);
            }
        }
        private void SaveTemplate()
        {
            using (var save = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "NavPro-alignment-template.csv" })
                if (save.ShowDialog(this) == DialogResult.OK) Guard(() => File.WriteAllText(save.FileName, "Alignment,Station,Easting,Northing\r\nExample,0,1000,2000\r\nExample,100,1100,2000\r\n"));
        }
    }
}
