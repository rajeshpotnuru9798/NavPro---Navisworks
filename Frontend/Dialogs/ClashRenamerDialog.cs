using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;
using Color = System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ClashRenamerDialog : Form
    {
        private readonly ClashRenamerService service;
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, EditMode = DataGridViewEditMode.EditOnEnter };
        private readonly TextBox findText = new TextBox { Width = 220 };
        private readonly TextBox replaceText = new TextBox { Width = 220 };
        private readonly TextBox prefixText = new TextBox { Width = 180 };
        private readonly CheckBox caseSensitive = new CheckBox { Text = "Case sensitive", AutoSize = true };
        private readonly CheckBox pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = Color.White, BackColor = Color.FromArgb(20, 39, 58), FlatStyle = FlatStyle.Flat };
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(0, 105, 125) };
        private readonly Button apply = new Button { Text = "Apply selected names", AutoSize = true, Enabled = false, BackColor = Color.FromArgb(0, 112, 135), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        private readonly Button refresh = new Button { Text = "Refresh tests", AutoSize = true };

        public ClashRenamerDialog(Document document)
        {
            service = new ClashRenamerService(document ?? throw new ArgumentNullException(nameof(document)));
            Text = "NavPro | Renamer"; Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(980, 720); MinimumSize = new Size(760, 560); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(241, 244, 248); MinimizeBox = false;
            BuildLayout();
            refresh.Click += (s, e) => RefreshTests();
            apply.Click += (s, e) => ApplySelected();
            pin.CheckedChanged += (s, e) => TopMost = pin.Checked;
            grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += (s, e) => { if (e.RowIndex >= 0) UpdateStatus(); };
            grid.CellEndEdit += (s, e) => UpdateStatus();
            DialogTheme.Apply(this, apply);
            RefreshTests();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 39, 58), Padding = new Padding(18) };
            header.Controls.Add(new Label { Text = "Renamer", Font = new Font("Segoe UI Semibold", 22F), ForeColor = Color.White, AutoSize = true, Location = new Point(16, 10) });
            header.Controls.Add(new Label { Text = "Find and replace text across clash test names, edit proposed names, or add a prefix to checked tests.", Font = new Font("Segoe UI", 10F), ForeColor = Color.FromArgb(220, 231, 240), AutoSize = true, Location = new Point(18, 54) });
            pin.Location = new Point(18, 77); header.Controls.Add(pin); root.Controls.Add(header, 0, 0);

            var tools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 3, Padding = new Padding(8), BackColor = Color.White };
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

            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Use", Width = 48, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentName", HeaderText = "Current Clash Test Name", ReadOnly = true, Width = 340, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NewName", HeaderText = "New Clash Test Name (editable)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            root.Controls.Add(grid, 0, 2);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(4, 8, 4, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(apply, 1, 0);
            var close = new Button { Text = "Close", AutoSize = true }; close.Click += (s, e) => Close(); footer.Controls.Add(close, 2, 0); root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
        }

        private void RefreshTests()
        {
            try
            {
                var items = service.ReadTests(); grid.Rows.Clear();
                foreach (var item in items) grid.Rows.Add(true, item.CurrentName, item.NewName, item.Id);
                UpdateStatus();
            }
            catch (Exception ex) { status.Text = "Could not load clash tests: " + ex.Message; apply.Enabled = false; }
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
                if (currentName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0 && newName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
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
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!Convert.ToBoolean(row.Cells["Selected"].Value ?? false)) continue;
                var name = Convert.ToString(row.Cells["NewName"].Value) ?? "";
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { row.Cells["NewName"].Value = prefix + name; changed++; }
            }
            UpdateStatus(changed == 0 ? "No checked names needed this prefix." : "Prefix added to " + changed.ToString("N0") + " checked names. Review before applying.");
        }

        private void ReplaceInChecked()
        {
            var find = findText.Text;
            if (string.IsNullOrEmpty(find)) { status.Text = "Enter the text to find before replacing."; findText.Focus(); return; }
            var replacement = replaceText.Text ?? string.Empty;
            var comparison = caseSensitive.Checked ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            int testsChanged = 0, occurrences = 0;
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
            UpdateStatus(occurrences == 0
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
            foreach (DataGridViewRow row in grid.Rows) row.Cells["Selected"].Value = selected;
            UpdateStatus(selected ? "All clash tests are checked." : "All clash tests are unchecked.");
        }

        private void ApplySelected()
        {
            try
            {
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
            if (message != null) status.Text = message;
            else status.Text = grid.Rows.Count.ToString("N0") + " clash tests loaded. Checked rows are included when applying names.";
            apply.Enabled = grid.Rows.Count > 0;
        }
    }
}
