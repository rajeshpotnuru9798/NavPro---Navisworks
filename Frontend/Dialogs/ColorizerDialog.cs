using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using Color = System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ColorizerDialog : Form
    {
        private readonly ColorizerService service;
        private readonly RadioButton modelsMode = new RadioButton { Text = "Models / Files", AutoSize = true, Checked = true };
        private readonly RadioButton setsMode = new RadioButton { Text = "Selection / Search Sets", AutoSize = true };
        private readonly CheckedListBox targets = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(0, 105, 125) };
        private readonly CheckBox pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = Color.White, BackColor = Color.FromArgb(20, 39, 58), FlatStyle = FlatStyle.Flat };
        private List<ColorizerTarget> currentTargets = new List<ColorizerTarget>();
        private List<ColorizerColorRow> lastAssignments = new List<ColorizerColorRow>();

        public ColorizerDialog(Document document)
        {
            service = new ColorizerService(document ?? throw new ArgumentNullException(nameof(document)));
            Text = "NavPro | Colorizer"; Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(900, 680); MinimumSize = new Size(700, 520); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(241, 244, 248); MinimizeBox = false;
            BuildLayout();
            modelsMode.CheckedChanged += (s, e) => { if (modelsMode.Checked) RefreshTargets(); };
            setsMode.CheckedChanged += (s, e) => { if (setsMode.Checked) RefreshTargets(); };
            pin.CheckedChanged += (s, e) => TopMost = pin.Checked;
            RefreshTargets();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(160, 0, 170), Padding = new Padding(16) };
            header.Controls.Add(new Label { Text = "Colorizer", Font = new Font("Segoe UI Semibold", 21F), ForeColor = Color.White, AutoSize = true, Location = new Point(14, 8) });
            header.Controls.Add(new Label { Text = "Color loaded models or saved Selection / Search Sets with random colors or an Excel color map.", Font = new Font("Segoe UI", 9.5F), ForeColor = Color.White, AutoSize = true, Location = new Point(16, 46) });
            pin.Location = new Point(16, 70); header.Controls.Add(pin); root.Controls.Add(header, 0, 0);

            var instructions = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2, Padding = new Padding(10), BackColor = Color.White };
            instructions.RowStyles.Add(new RowStyle(SizeType.AutoSize)); instructions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var mode = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            mode.Controls.Add(modelsMode); mode.Controls.Add(setsMode);
            instructions.Controls.Add(mode, 0, 0);
            instructions.Controls.Add(new Label { Dock = DockStyle.Fill, AutoSize = true, Text =
                "Instructions\r\n1. Choose Models / Files or Selection / Search Sets.\r\n2. Check specific targets to limit the operation; leave all unchecked to include every target in the chosen mode.\r\n3. Random Colors applies a generated color to each target. Export Random Colors to Excel, edit Color Hex (or Red / Green / Blue), and save as .xlsx.\r\n4. Import Colors from Excel matches Target Name and, when present, Target Key. Names must match Navisworks; duplicate names need the exported key.\r\nColors are permanent Navisworks appearance overrides. Save the NWF/NWD to retain changes. Overlapping sets can color the same geometry; the last applied color wins. Review appearance in Shaded or Wireframe if render settings hide overrides.",
                ForeColor = Color.FromArgb(35, 49, 67), Font = new Font("Segoe UI", 9F), Padding = new Padding(2, 8, 2, 2) }, 0, 1);
            var instructionScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            instructionScroll.Controls.Add(instructions); root.Controls.Add(instructionScroll, 0, 1);

            var listPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(6), BackColor = Color.White };
            listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoSize = true };
            var refresh = Button("Refresh list"); refresh.Click += (s, e) => RefreshTargets();
            var all = Button("Select all"); all.Click += (s, e) => SetChecked(true);
            var none = Button("Select none"); none.Click += (s, e) => SetChecked(false);
            var random = Button("Random Colors"); random.Click += (s, e) => ApplyRandom();
            var export = Button("Export Random Colors to Excel"); export.Click += (s, e) => ExportColors();
            var import = Button("Import Colors from Excel"); import.Click += (s, e) => ImportColors();
            actions.Controls.AddRange(new Control[] { refresh, all, none, random, export, import });
            var palettes = Button("Saved discipline palettes"); palettes.Click += (s, e) => LoadPalette(); actions.Controls.Add(palettes);
            listPanel.Controls.Add(actions, 0, 0); listPanel.Controls.Add(targets, 0, 1); root.Controls.Add(listPanel, 0, 2);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(4, 8, 4, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(status, 0, 0);
            var close = Button("Close"); close.Click += (s, e) => Close(); footer.Controls.Add(close, 2, 0);
            root.Controls.Add(footer, 0, 3); Controls.Add(root);
            targets.HorizontalScrollbar = true;
            NavPro.Frontend.Ui.ResizableLayout.Rows(root, 1, 2, 1, 0.3, "Colorizer instructions and loaded targets");
        }

        private static Button Button(string text) => new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(2, 3, 4, 3) };

        private void RefreshTargets()
        {
            try
            {
                currentTargets = service.ReadTargets(modelsMode.Checked);
                targets.Items.Clear(); foreach (var target in currentTargets) targets.Items.Add(target, false);
                status.Text = currentTargets.Count.ToString("N0") + (modelsMode.Checked ? " model/file target(s)" : " saved Selection / Search Set(s)") + " loaded. Check specific targets, or leave all unchecked to include all.";
                lastAssignments.Clear();
            }
            catch (Exception ex) { status.Text = "Could not read targets: " + ex.Message; }
        }

        private void SetChecked(bool value) { for (int i = 0; i < targets.Items.Count; i++) targets.SetItemChecked(i, value); }

        private List<ColorizerTarget> SelectedTargets()
        {
            var selected = targets.CheckedItems.Cast<ColorizerTarget>().ToList();
            return selected.Count == 0 ? currentTargets.ToList() : selected;
        }

        private void ApplyRandom()
        {
            var chosen = SelectedTargets(); if (chosen.Count == 0) { status.Text = "There are no targets in the selected mode."; return; }
            try
            {
                lastAssignments = chosen.Select((target, index) => new ColorizerColorRow { TargetType = target.Type, TargetName = target.Name, TargetKey = target.Key, Red = 255, Green = 0, Blue = 255 }).ToList();
                // Evenly-spaced hues reduce adjacent color collisions across long lists.
                for (int i = 0; i < lastAssignments.Count; i++)
                {
                    var rgb = HslToRgb((i * 137.508) % 360.0, 0.72, 0.52);
                    lastAssignments[i].Red = rgb.R; lastAssignments[i].Green = rgb.G; lastAssignments[i].Blue = rgb.B;
                }
                if (!PreviewPalette(lastAssignments)) return;
                int count = service.Apply(lastAssignments);
                int empty = lastAssignments.Count - count;
                status.Text = "Applied random permanent colors to " + count.ToString("N0") + " target(s)" + (empty == 0 ? ". " : "; " + empty.ToString("N0") + " target(s) had no geometry to color. ") + "Export to Excel to edit or reuse the assignments, then save the NWF/NWD.";
            }
            catch (Exception ex) { status.Text = "Coloring stopped: " + ex.Message + " Review the model because native appearance changes may have partially applied."; }
        }

        private void ExportColors()
        {
            var rows = lastAssignments.Count > 0 ? lastAssignments : SelectedTargets().Select((target, i) => {
                var c = HslToRgb((i * 137.508) % 360.0, 0.72, 0.52);
                return new ColorizerColorRow { TargetType = target.Type, TargetName = target.Name, TargetKey = target.Key, Red = c.R, Green = c.G, Blue = c.B };
            }).ToList();
            if (rows.Count == 0) { status.Text = "There are no targets to export."; return; }
            using (var save = new SaveFileDialog { Title = "Export Colorizer workbook", Filter = "Excel workbook (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true, FileName = "NavPro-Colorizer-Colors.xlsx" })
                if (save.ShowDialog(this) == DialogResult.OK)
                    try { ColorizerWorkbook.Write(save.FileName, rows); lastAssignments = rows; status.Text = "Exported " + rows.Count.ToString("N0") + " target color(s) to " + save.FileName; }
                    catch (Exception ex) { status.Text = "Could not export workbook: " + ex.Message; }
        }

        private void ImportColors()
        {
            using (var open = new OpenFileDialog { Title = "Import Colorizer workbook", Filter = "Excel workbook (*.xlsx)|*.xlsx", CheckFileExists = true, Multiselect = false })
            {
                if (open.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var workbookRows = ColorizerWorkbook.Read(open.FileName);
                    var chosen = SelectedTargets();
                    var matched = MatchRows(workbookRows, chosen);
                    if (matched.Count != chosen.Count) throw new InvalidOperationException("Workbook contains colors for " + matched.Count + " of " + chosen.Count + " target(s) in the current scope. Add the missing rows or select only the targets you want to color.");
                    if (!PreviewPalette(matched)) return;
                    int count = service.Apply(matched); lastAssignments = matched;
                    int empty = matched.Count - count;
                    status.Text = "Imported colors for " + count.ToString("N0") + " target(s)" + (empty == 0 ? ". " : "; " + empty.ToString("N0") + " target(s) had no geometry to color. ") + "Save the NWF/NWD to retain the appearance overrides.";
                }
                catch (Exception ex) { status.Text = "Could not import colors: " + ex.Message; }
            }
        }

        private List<ColorizerColorRow> MatchRows(List<ColorizerColorRow> rows, List<ColorizerTarget> chosen)
        {
            var result = new List<ColorizerColorRow>();
            foreach (var target in chosen)
            {
                var matches = rows.Where(row => string.Equals(row.TargetType, target.Type, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(row.TargetName?.Trim(), target.Name, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(row.TargetKey) || string.Equals(row.TargetKey, target.Key, StringComparison.OrdinalIgnoreCase))).ToList();
                if (matches.Count == 0 && chosen.Count == 1)
                    matches = rows.Where(row => string.Equals(row.TargetName?.Trim(), target.Name, StringComparison.OrdinalIgnoreCase)
                        && (string.IsNullOrWhiteSpace(row.TargetType) || string.Equals(row.TargetType, target.Type, StringComparison.OrdinalIgnoreCase))).ToList();
                if (matches.Count != 1) throw new InvalidOperationException(matches.Count == 0
                    ? "No exact workbook row matches target '" + target.Name + "'. Names must match Navisworks; export a fresh workbook to obtain its Target Key."
                    : "Workbook contains duplicate rows for target '" + target.Name + "'. Keep one matching row.");
                result.Add(matches[0]);
            }
            return result;
        }

        private bool ConfirmApply(string message)
        {
            return MessageBox.Show(this, message + "\r\n\r\nThis changes permanent Navisworks appearance overrides. Review the result and save the model if you want to retain it.", "NavPro - Colorizer", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void LoadPalette()
        {
            try
            {
                var saved = ReviewPreferences.Read<Dictionary<string, List<ColorizerColorRow>>>("discipline-palettes");
                if (saved.Count == 0) { MessageBox.Show(this, "No saved palettes yet. Preview Random Colors or an imported colour map, enter a palette name and choose Save palette.", "Saved discipline palettes"); return; }
                using (var dialog = new Form { Name = "SavedDisciplinePalettes", Text = "Saved discipline palettes", Width = 550, Height = 350, StartPosition = FormStartPosition.CenterParent })
                {
                    var list = new ListBox { Dock = DockStyle.Fill }; list.Items.AddRange(saved.Keys.OrderBy(k => k).Cast<object>().ToArray()); dialog.Controls.Add(list);
                    var open = new System.Windows.Forms.Button { Text = "Preview selected palette", Dock = DockStyle.Bottom, Height = 40 };
                    open.Click += (s, e) => { if (list.SelectedItem == null) return; try { var rows = MatchRows(saved[list.Text], SelectedTargets()); if (PreviewPalette(rows)) { service.Apply(rows); lastAssignments = rows; status.Text = "Applied saved palette: " + list.Text; dialog.Close(); } } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } };
                    dialog.Controls.Add(open); NavPro.Frontend.Ui.DialogTheme.Apply(dialog, open); dialog.ShowDialog(this);
                }
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        private bool PreviewPalette(List<ColorizerColorRow> rows)
        {
            using (var dialog = new Form { Name = "ColourLegendPreview", Text = "Colorizer | Preview and discipline legend", Width = 850, Height = 620, MinimumSize = new Size(650, 420), StartPosition = FormStartPosition.CenterParent })
            {
                var legend = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
                legend.Columns.Add("Target", "Target / discipline"); legend.Columns.Add("Color", "Colour (double-click to edit)");
                foreach (var row in rows) { int i = legend.Rows.Add(row.TargetName, row.Hex); legend.Rows[i].Cells[1].Style.BackColor = Color.FromArgb(row.Red, row.Green, row.Blue); }
                legend.CellDoubleClick += (s, e) => { if (e.RowIndex < 0) return; var row = rows[e.RowIndex]; using (var pick = new ColorDialog { Color = Color.FromArgb(row.Red, row.Green, row.Blue), FullOpen = true }) if (pick.ShowDialog(dialog) == DialogResult.OK) { row.Red = pick.Color.R; row.Green = pick.Color.G; row.Blue = pick.Color.B; legend.Rows[e.RowIndex].Cells[1].Value = row.Hex; legend.Rows[e.RowIndex].Cells[1].Style.BackColor = pick.Color; } };
                var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 90, WrapContents = true };
                var name = new TextBox { Width = 180, AccessibleName = "Discipline palette name" }; bar.Controls.Add(new Label { Text = "Palette name", AutoSize = true }); bar.Controls.Add(name);
                var save = Button("Save palette"); save.Click += (s, e) => { try { if (string.IsNullOrWhiteSpace(name.Text) || name.Text.Length > 100) throw new InvalidOperationException("Enter a palette name of 1–100 characters."); var saved = ReviewPreferences.Read<Dictionary<string, List<ColorizerColorRow>>>("discipline-palettes"); saved[name.Text.Trim()] = rows; ReviewPreferences.Write("discipline-palettes", saved); MessageBox.Show(dialog, "Palette saved locally."); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }; bar.Controls.Add(save);
                var apply = Button("Apply previewed colours"); apply.Click += (s, e) => { if (ConfirmApply("Apply this reviewed legend to " + rows.Count + " targets? Overlapping targets use the last colour.")) dialog.DialogResult = DialogResult.OK; }; bar.Controls.Add(apply);
                var close = Button("Cancel"); close.DialogResult = DialogResult.Cancel; bar.Controls.Add(close); dialog.CancelButton = close;
                dialog.Controls.Add(legend); dialog.Controls.Add(bar);
                NavPro.Frontend.Ui.DialogTheme.Apply(dialog, apply); return dialog.ShowDialog(this) == DialogResult.OK;
            }
        }

        private static (byte R, byte G, byte B) HslToRgb(double hue, double saturation, double lightness)
        {
            double c = (1 - Math.Abs(2 * lightness - 1)) * saturation, x = c * (1 - Math.Abs((hue / 60) % 2 - 1)), m = lightness - c / 2;
            double r, g, b;
            if (hue < 60) { r = c; g = x; b = 0; } else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; } else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; }
            return ((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }
    }
}
