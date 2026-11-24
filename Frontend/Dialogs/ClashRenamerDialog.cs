using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;
using NavPro.Backend.Core;
using Color = System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ClashRenamerDialog : Form
    {
        private readonly IClashRenamerService service;
        private bool loading;
        private readonly ComboBox presets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, AccessibleName = "Naming pattern preset" };
        private readonly TextBox pattern = new TextBox { Width = 300, Text = "{n}_{name}", AccessibleName = "Naming pattern" };
        private readonly NumericUpDown start = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 1, Width = 90, AccessibleName = "Numbering start" };
        private readonly NumericUpDown step = new NumericUpDown { Minimum = 1, Maximum = 1000000, Value = 1, Width = 65, AccessibleName = "Numbering step" };
        private readonly NumericUpDown digits = new NumericUpDown { Minimum = 1, Maximum = 9, Value = 3, Width = 48, AccessibleName = "Number digits" };
        private readonly RichTextBox beforeDiff = new RichTextBox { ReadOnly = true, Dock = DockStyle.Fill, DetectUrls = false, WordWrap = false, AccessibleName = "Before text differences" };
        private readonly RichTextBox afterDiff = new RichTextBox { ReadOnly = true, Dock = DockStyle.Fill, DetectUrls = false, WordWrap = false, AccessibleName = "After text differences" };
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, EditMode = DataGridViewEditMode.EditOnEnter };
        private readonly TextBox findText = new TextBox { Width = 220 };
        private readonly TextBox replaceText = new TextBox { Width = 220 };
        private readonly TextBox prefixText = new TextBox { Width = 180 };
        private readonly CheckBox caseSensitive = new CheckBox { Text = "Case sensitive", AutoSize = true };
        private readonly CheckBox pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = Color.White, BackColor = Color.FromArgb(20, 39, 58), FlatStyle = FlatStyle.Flat };
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(0, 105, 125) };
        private readonly Button apply = new Button { Text = "Apply selected names", AutoSize = true, Enabled = false, BackColor = Color.FromArgb(0, 112, 135), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        private readonly Button refresh = new Button { Text = "Refresh tests", AutoSize = true };

        public ClashRenamerDialog(Document document) : this(new ClashRenamerService(document ?? throw new ArgumentNullException(nameof(document)))) { }
        internal ClashRenamerDialog(IClashRenamerService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            Text = "NavPro | Renamer"; Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1120, 900); MinimumSize = new Size(940, 780); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(241, 244, 248); MinimizeBox = false;
            BuildLayout();
            refresh.Click += (s, e) => RefreshTests();
            apply.Click += (s, e) => ApplySelected();
            pin.CheckedChanged += (s, e) => TopMost = pin.Checked;
            grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += (s, e) => { if (!loading && e.RowIndex >= 0) UpdateStatus(); };
            grid.CellEndEdit += (s, e) => UpdateStatus();
            grid.SelectionChanged += (s, e) => ShowDifference();
            DialogTheme.Apply(this, apply);
            RefreshTests();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 39, 58), Padding = new Padding(18) };
            header.Controls.Add(new Label { Text = "Renamer", Font = new Font("Segoe UI Semibold", 22F), ForeColor = Color.White, AutoSize = true, Location = new Point(16, 10) });
            header.Controls.Add(new Label { Text = "Find and replace text across clash test names, edit proposed names, or add a prefix to checked tests.", Font = new Font("Segoe UI", 10F), ForeColor = Color.FromArgb(220, 231, 240), AutoSize = true, Location = new Point(18, 54) });
            pin.Location = new Point(18, 77); header.Controls.Add(pin); root.Controls.Add(header, 0, 0);

            var tools = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 6, RowCount = 3, Padding = new Padding(8), BackColor = Color.White };
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tools.Controls.Add(new Label { Text = "Find", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); tools.Controls.Add(findText, 1, 0);
            var next = new Button { Text = "Find next", AutoSize = true }; next.Click += (s, e) => FindMatch(1); tools.Controls.Add(next, 2, 0);
            var previous = new Button { Text = "Find previous", AutoSize = true }; previous.Click += (s, e) => FindMatch(-1); tools.Controls.Add(previous, 3, 0);
            tools.Controls.Add(caseSensitive, 4, 0); tools.Controls.Add(refresh, 5, 0);
            tools.Controls.Add(new Label { Text = "Replace with", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); tools.Controls.Add(replaceText, 1, 1);
            var replace = new Button { Text = "Replace in checked", AutoSize = true }; replace.Click += (s, e) => ReplaceInChecked(); tools.Controls.Add(replace, 2, 1); tools.SetColumnSpan(replace, 2);
            tools.Controls.Add(new Label { Text = "Prefix", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); tools.Controls.Add(prefixText, 1, 2);
            var addPrefix = new Button { Text = "Add prefix to checked", AutoSize = true }; addPrefix.Click += (s, e) => AddPrefix(); tools.Controls.Add(addPrefix, 2, 2); tools.SetColumnSpan(addPrefix, 2);
            var selectAll = new Button { Text = "Select all", AutoSize = true }; selectAll.Click += (s, e) => SetSelected(true); tools.Controls.Add(selectAll, 4, 2);
            var selectNone = new Button { Text = "Select none", AutoSize = true }; selectNone.Click += (s, e) => SetSelected(false); tools.Controls.Add(selectNone, 5, 2);
            root.Controls.Add(tools, 0, 1);

            var patternArea = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
            patternArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var patternBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            presets.Items.AddRange(new object[] { "Number + original", "Prefix + numbered original", "Discipline pair + number", "Custom pattern" });
            presets.SelectedIndexChanged += (s, e) => { if (presets.SelectedIndex < 3) pattern.Text = new[] { "{n}_{name}", "{prefix}{n}_{name}", "{priority}_{a}_VS_{b}_{n}" }[presets.SelectedIndex]; };
            presets.SelectedIndex = 0;
            pattern.TextChanged += (s, e) => { if (presets.SelectedIndex < 3 && pattern.Text != new[] { "{n}_{name}", "{prefix}{n}_{name}", "{priority}_{a}_VS_{b}_{n}" }[presets.SelectedIndex]) presets.SelectedIndex = 3; };
            foreach (var field in new[] { Tuple.Create("Preset", (Control)presets), Tuple.Create("Pattern", (Control)pattern), Tuple.Create("Start", (Control)start), Tuple.Create("Step", (Control)step), Tuple.Create("Digits", (Control)digits) })
            {
                var pair = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
                pair.Controls.Add(new Label { Text = field.Item1, AutoSize = true, Padding = new Padding(3, 6, 2, 0) }); pair.Controls.Add(field.Item2); patternBar.Controls.Add(pair);
            }
            var previewPattern = new Button { Text = "Preview pattern", AutoSize = true }; previewPattern.Click += (s, e) => PreviewPattern(); patternBar.Controls.Add(previewPattern);
            patternArea.Controls.Add(patternBar, 0, 0);
            patternArea.Controls.Add(new Label { Text = "Fields: {name}, {prefix}, {n}, {priority}, {a}, {b}. Number checked rows in displayed order, using ORIGINAL names. Preview stages names only. Select a row to inspect highlighted changes below.", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(3, 3, 3, 6) }, 0, 1);
            root.Controls.Add(patternArea, 0, 2);

            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Use", Width = 48, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentName", HeaderText = "Current Clash Test Name", ReadOnly = true, Width = 340, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NewName", HeaderText = "New Clash Test Name (editable)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            root.Controls.Add(grid, 0, 3);
            var differences = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            differences.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); differences.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            differences.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); differences.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            differences.Controls.Add(new Label { Text = "Before — removed/changed text", AutoSize = true }, 0, 0);
            differences.Controls.Add(new Label { Text = "After — added/changed text", AutoSize = true }, 1, 0);
            differences.Controls.Add(beforeDiff, 0, 1); differences.Controls.Add(afterDiff, 1, 1); root.Controls.Add(differences, 0, 4);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(4, 8, 4, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(apply, 1, 0);
            var close = new Button { Text = "Close", AutoSize = true }; close.Click += (s, e) => Close(); footer.Controls.Add(close, 2, 0); root.Controls.Add(footer, 0, 5);
            Controls.Add(root);
        }

        private void RefreshTests()
        {
            try
            {
                loading = true; var items = service.ReadTests(); grid.Rows.Clear();
                foreach (var item in items) grid.Rows.Add(true, item.CurrentName, item.NewName, item.Id);
                loading = false;
                UpdateStatus();
            }
            catch (Exception ex) { status.Text = "Could not load clash tests: " + ex.Message; apply.Enabled = false; }
            finally { loading = false; }
        }

        private void FindMatch(int direction)
        {
            var query = findText.Text.Trim();
            if (query.Length == 0) { status.Text = "Enter text to find in the clash test names."; findText.Focus(); return; }
            if (grid.Rows.Count == 0) { status.Text = "No clash tests are loaded."; return; }
            int current = grid.CurrentCell == null ? (direction > 0 ? -1 : 0) : grid.CurrentCell.RowIndex;
            for (int step = 1; step <= grid.Rows.Count; step++)
            {
                int index = (current + direction * step + grid.Rows.Count * 2) % grid.Rows.Count;
                var row = grid.Rows[index];
                var currentName = Convert.ToString(row.Cells["CurrentName"].Value) ?? "";
                var newName = Convert.ToString(row.Cells["NewName"].Value) ?? "";
                var comparison = caseSensitive.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
                if (currentName.IndexOf(query, comparison) < 0 && newName.IndexOf(query, comparison) < 0) continue;
                grid.ClearSelection(); row.Selected = true; grid.CurrentCell = row.Cells["NewName"]; grid.FirstDisplayedScrollingRowIndex = index;
                status.Text = "Found test " + (index + 1).ToString("N0") + " of " + grid.Rows.Count.ToString("N0") + "."; return;
            }
            status.Text = "No clash test name contains '" + query + "'.";
        }

        private void AddPrefix()
        {
            var prefix = prefixText.Text;
            if (string.IsNullOrWhiteSpace(prefix)) { status.Text = "Enter the prefix to add."; prefixText.Focus(); return; }
            int changed = 0;
            grid.EndEdit(); loading = true;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!Convert.ToBoolean(row.Cells["Selected"].Value ?? false)) continue;
                var name = Convert.ToString(row.Cells["NewName"].Value) ?? "";
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { row.Cells["NewName"].Value = prefix + name; changed++; }
            }
            loading = false; UpdateStatus(changed == 0 ? "No checked names needed this prefix." : "Prefix added to " + changed.ToString("N0") + " checked names. Review before applying.");
        }

        private void ReplaceInChecked()
        {
            var find = findText.Text;
            if (string.IsNullOrEmpty(find)) { status.Text = "Enter the text to find before replacing."; findText.Focus(); return; }
            var replacement = replaceText.Text ?? string.Empty;
            var comparison = caseSensitive.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            int testsChanged = 0, occurrences = 0;
            grid.EndEdit(); loading = true;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!Convert.ToBoolean(row.Cells["Selected"].Value ?? false)) continue;
                var name = Convert.ToString(row.Cells["NewName"].Value) ?? string.Empty;
                int found;
                var updated = ReplaceMatches(name, find, replacement, comparison, out found);
                if (found == 0) continue;
                row.Cells["NewName"].Value = updated;
                testsChanged++;
                occurrences += found;
            }
            loading = false; UpdateStatus(occurrences == 0
                ? "No matches found in checked proposed names. Nothing was changed."
                : "Replaced " + occurrences.ToString("N0") + " match(es) across " + testsChanged.ToString("N0") + " checked test(s). Review the proposed names before applying.");
        }

        private static string ReplaceMatches(string value, string find, string replacement, StringComparison comparison, out int count)
        {
            count = 0;
            var builder = new StringBuilder();
            int cursor = 0, match;
            while ((match = value.IndexOf(find, cursor, comparison)) >= 0)
            {
                builder.Append(value, cursor, match - cursor).Append(replacement);
                cursor = match + find.Length;
                count++;
            }
            return count == 0 ? value : builder.Append(value, cursor, value.Length - cursor).ToString();
        }

        private void SetSelected(bool selected)
        {
            grid.EndEdit(); loading = true;
            foreach (DataGridViewRow row in grid.Rows) row.Cells["Selected"].Value = selected;
            loading = false;
            UpdateStatus(selected ? "All clash tests are checked." : "All clash tests are unchecked.");
        }

        private void ApplySelected()
        {
            try
            {
                grid.EndEdit(); UpdateStatus(); if (!apply.Enabled) return;
                var selected = grid.Rows.Cast<DataGridViewRow>().Where(row => Convert.ToBoolean(row.Cells["Selected"].Value ?? false)).ToList();
                if (selected.Count == 0) { status.Text = "Check at least one clash test to update."; return; }
                var updates = selected.Select(row => new ClashRenameItem { Id = Convert.ToString(row.Cells["Id"].Value), CurrentName = Convert.ToString(row.Cells["CurrentName"].Value), NewName = Convert.ToString(row.Cells["NewName"].Value) }).ToList();
                var changed = updates.Count(item => !string.Equals(item.CurrentName, item.NewName == null ? null : item.NewName.Trim(), StringComparison.Ordinal));
                if (changed == 0) { status.Text = "The checked names have no changes."; return; }
                var answer = MessageBox.Show(this, "Rename " + changed.ToString("N0") + " clash test(s)? Existing tests and clash results will be preserved.", "Confirm bulk rename", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) return;
                var result = service.Apply(updates);
                RefreshTests();
                status.Text = "Renamed " + result.Renamed.ToString("N0") + " clash test(s); " + result.Unchanged.ToString("N0") + " selected names were unchanged.";
            }
            catch (Exception ex) { status.Text = "Rename stopped: " + ex.Message; }
        }

        private void UpdateStatus(string message = null)
        {
            if (loading) return;
            var rows = grid.Rows.Cast<DataGridViewRow>().ToList();
            var names = rows.Select(row => Convert.ToBoolean(row.Cells["Selected"].Value ?? false) ? Convert.ToString(row.Cells["NewName"].Value).Trim() : Convert.ToString(row.Cells["CurrentName"].Value)).ToList();
            var duplicates = new System.Collections.Generic.HashSet<string>(names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            int changed = 0, selected = 0, invalid = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; bool use = Convert.ToBoolean(row.Cells["Selected"].Value ?? false);
                string original = Convert.ToString(row.Cells["CurrentName"].Value), proposed = Convert.ToString(row.Cells["NewName"].Value);
                bool different = original != proposed.Trim(); if (use) { selected++; if (different) changed++; }
                row.ErrorText = names[i].Length == 0 ? "Name is empty" : names[i].Length > 255 || names[i].Any(char.IsControl) ? "Use 1–255 printable characters" : duplicates.Contains(names[i]) ? "Duplicate test name" : "";
                if (row.ErrorText.Length > 0) invalid++;
                row.Cells["NewName"].ToolTipText = row.ErrorText.Length > 0 ? row.ErrorText : different ? "Proposed change — select row to inspect the highlighted differences below." : "Unchanged";
                row.Cells["NewName"].Style.BackColor = row.ErrorText.Length > 0 ? Color.MistyRose : different ? Color.FromArgb(224, 245, 227) : Color.White;
            }
            status.Text = message ?? rows.Count + " tests · " + selected + " checked · " + changed + " changed · " + invalid + " invalid. Review before applying.";
            apply.Enabled = changed > 0 && invalid == 0;
            ShowDifference();
        }

        private void PreviewPattern()
        {
            try
            {
                grid.EndEdit();
                var rows = grid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells["Selected"].Value ?? false)).ToList();
                var names = RenamePatterns.Build(rows.Select(r => new ClashRenameItem { Id = Convert.ToString(r.Cells["Id"].Value), CurrentName = Convert.ToString(r.Cells["CurrentName"].Value) }), pattern.Text, prefixText.Text, (int)start.Value, (int)step.Value, (int)digits.Value);
                loading = true;
                for (int i = 0; i < rows.Count; i++) rows[i].Cells["NewName"].Value = names[i].NewName;
                loading = false; UpdateStatus();
            }
            catch (Exception ex) { loading = false; status.Text = "Pattern preview: " + ex.Message; }
        }
        private void ShowDifference()
        {
            var row = grid.CurrentRow;
            string before = row == null ? "" : Convert.ToString(row.Cells["CurrentName"].Value);
            string after = row == null ? "" : Convert.ToString(row.Cells["NewName"].Value);
            var diff = RenamePatterns.Difference(before, after);
            PaintDifference(beforeDiff, before, diff.Item1, diff.Item2, Color.MistyRose);
            PaintDifference(afterDiff, after, diff.Item1, diff.Item3, Color.FromArgb(199, 239, 206));
        }
        private static void PaintDifference(RichTextBox box, string text, int start, int count, Color color)
        {
            box.Text = text; box.SelectAll(); box.SelectionBackColor = Color.White; box.SelectionColor = Color.Black;
            if (count > 0) { box.Select(start, count); box.SelectionBackColor = color; }
            box.Select(0, 0);
        }
    }
}
