using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ClashToleranceDialog : Form
    {
        private readonly ClashToleranceService service;
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly NumericUpDown tolerance = new NumericUpDown { DecimalPlaces = 3, Maximum = 1000000000, Increment = 0.001M, Width = 140, AccessibleName = "New tolerance" };
        private readonly ComboBox units = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, AccessibleName = "Tolerance units" };
        private readonly ComboBox type = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, AccessibleName = "Clash type" };
        private readonly ComboBox composite = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, AccessibleName = "Composite Object Clashing setting" };
        private readonly TextBox testFilter = new TextBox { Dock = DockStyle.Fill, MinimumSize = new Size(140, 0), AccessibleName = "Filter clash tests", AccessibleDescription = "Show clash tests whose names contain this text." };
        private readonly Button apply = new Button { Text = "Apply preview", AutoSize = true, Enabled = false };
        private readonly Button rerun = new Button { Text = "Re-Run Clashes", AutoSize = true, Enabled = false };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label hint = new Label { Dock = DockStyle.Fill, AutoSize = true };
        private List<ClashToleranceChange> plan;
        private bool inputInitialized;

        public ClashToleranceDialog(Document document) : this(new ClashToleranceService(document), document.Units) { }

        // Detached preview construction does not touch the host; used for dialog layout verification.
        internal ClashToleranceDialog(ClashToleranceService service, Units documentUnits)
        {
            this.service = service;
            Text = "NavPro | Tolerance Updater"; Size = new Size(1020, 720); MinimumSize = new Size(860, 620);
            Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
            type.Items.AddRange(new object[] { "Keep current type", "Hard", "Hard (Conservative)", "Clearance", "Duplicates" }); type.SelectedIndex = 0;
            composite.Items.AddRange(new object[] { "Keep current", "Enabled", "Disabled" }); composite.SelectedIndex = 0;
            foreach (Units value in Enum.GetValues(typeof(Units))) units.Items.Add(value);
            units.SelectedItem = documentUnits;
            BuildLayout();
            tolerance.ValueChanged += (s, e) => InvalidatePreview();
            units.SelectedIndexChanged += (s, e) => InvalidatePreview();
            type.SelectedIndexChanged += (s, e) => { UpdateHint(); InvalidatePreview(); };
            composite.SelectedIndexChanged += (s, e) => { UpdateHint(); InvalidatePreview(); };
            grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += (s, e) => { if (e.ColumnIndex == 0) InvalidatePreview(); };
            testFilter.TextChanged += (s, e) => ApplyTestFilter();
            apply.Click += (s, e) => ApplyPreview();
            rerun.Click += (s, e) => ReRunClashes();
            DialogTheme.Apply(this, apply); UpdateHint();
            if (service != null) RefreshTests();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            var header = new Panel { Dock = DockStyle.Fill, BackColor = DialogTheme.Brand };
            header.Controls.Add(new Label { Text = "Tolerance Updater", AutoSize = true, ForeColor = System.Drawing.Color.White,
                Font = new Font(Font.FontFamily, 22, FontStyle.Bold), Location = new Point(12, 10) });
            var pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = System.Drawing.Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(820, 22) };
            header.Resize += (s, e) => pin.Left = header.ClientSize.Width - pin.Width - 16;
            pin.CheckedChanged += (s, e) => TopMost = pin.Checked; header.Controls.Add(pin); root.Controls.Add(header, 0, 0);
            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 7, RowCount = 1, Padding = new Padding(4, 12, 4, 6) };
            for (int column = 0; column < 6; column++) settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settings.Controls.Add(new Label { Text = "Type", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 0, 0); settings.Controls.Add(type, 1, 0);
            settings.Controls.Add(new Label { Text = "Tolerance", AutoSize = true, Padding = new Padding(12, 5, 0, 0) }, 2, 0); settings.Controls.Add(tolerance, 3, 0);
            settings.Controls.Add(units, 4, 0);
            settings.Controls.Add(new Label { Text = "Filter", AutoSize = true, Padding = new Padding(12, 5, 0, 0) }, 5, 0);
            settings.Controls.Add(testFilter, 6, 0);
            root.Controls.Add(settings, 0, 1); root.Controls.Add(hint, 0, 2);
            var compositeOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 3, 0, 0) };
            compositeOptions.Controls.Add(new Label { Text = "Composite Object Clashing", AutoSize = true, Padding = new Padding(0, 5, 8, 0) });
            compositeOptions.Controls.Add(composite);
            root.Controls.Add(compositeOptions, 0, 3);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 4, 0, 6) };
            AddButton(tools, "Select all", () => SelectTests(true)); AddButton(tools, "Select none", () => SelectTests(false));
            AddButton(tools, "Refresh tests", RefreshTests); AddButton(tools, "Preview changes", Preview); tools.Controls.Add(rerun);
            root.Controls.Add(tools, 0, 4);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Use", Width = 46 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Clash test", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180, ReadOnly = true });
            foreach (var title in new[] { "Current type", "Current tolerance", "Proposed type", "Proposed tolerance" })
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, Width = 145, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Current composite", Width = 130, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Proposed composite", Width = 140, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Change", Width = 100, ReadOnly = true });
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            root.Controls.Add(grid, 0, 5);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 8, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(apply, 1, 0);
            var close = new Button { Text = "Close", AutoSize = true }; close.Click += (s, e) => Close(); CancelButton = close; footer.Controls.Add(close, 2, 0);
            root.Controls.Add(footer, 0, 6); Controls.Add(root);
        }

        private static void AddButton(Control parent, string text, System.Action action)
        { var button = new Button { Text = text, AutoSize = true }; button.Click += (s, e) => action(); parent.Controls.Add(button); }
        private static string TypeLabel(ClashTestType value) => value == ClashTestType.HardConservative ? "Hard (Conservative)" : value == ClashTestType.Duplicate ? "Duplicates" : value.ToString();
        private void UpdateHint()
        {
            var descriptions = new[] { "Only tolerance changes; each test keeps its current type.", "Hard: detect actual geometry intersections; tolerance filters small penetrations.",
                "Hard (Conservative): use conservative intersection detection with the entered tolerance.", "Clearance: detect geometry within the entered distance, including hard clashes.", "Duplicates: detect duplicate geometry within the entered tolerance." };
            var compositeDescription = composite.SelectedIndex == 0 ? "Composite Object Clashing: keep each test's current setting."
                : "Composite Object Clashing: set selected tests to " + (composite.SelectedIndex == 1 ? "Enabled." : "Disabled.");
            hint.Text = descriptions[type.SelectedIndex] + "\n" + compositeDescription + " Results need a rerun after settings change; tests are not run or saved automatically.";
        }
        private void InvalidatePreview()
        {
            plan = null; apply.Enabled = false;
            rerun.Enabled = grid.Rows.Count > 0 && SelectedRows().Any();
            foreach (DataGridViewRow row in grid.Rows)
                foreach (int cell in new[] { 4, 5, 7, 8 }) row.Cells[cell].Value = null;
            status.Text = grid.Rows.Count == 0 ? "No clash tests loaded." : "Checked: " + SelectedRows().Count() + " of " + grid.Rows.Count + ". Preview changes to continue.";
        }
        private IEnumerable<DataGridViewRow> SelectedRows() => grid.Rows.Cast<DataGridViewRow>().Where(row => Convert.ToBoolean(row.Cells[0].Value ?? false));
        private void SelectTests(bool value) { grid.EndEdit(); foreach (DataGridViewRow row in grid.Rows) row.Cells[0].Value = value; InvalidatePreview(); }
        private void ApplyTestFilter()
        {
            var query = testFilter.Text.Trim();
            if (grid.CurrentCell != null) grid.CurrentCell = null;
            foreach (DataGridViewRow row in grid.Rows)
            {
                var name = Convert.ToString(row.Cells[1].Value) ?? string.Empty;
                row.Visible = query.Length == 0 || name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
        private void RefreshTests()
        {
            var hadRows = grid.Rows.Count > 0;
            var selectedIds = new HashSet<string>(SelectedRows().Select(row => ((ClashToleranceItem)row.Tag).Id), StringComparer.OrdinalIgnoreCase);
            plan = null; apply.Enabled = false; rerun.Enabled = false; grid.Rows.Clear();
            try
            {
                var items = service.ReadTests();
                if (!inputInitialized && items.Count > 0)
                {
                    double initial = items[0].Tolerance * UnitConversion.ScaleFactor(items[0].DocumentUnits, (Units)units.SelectedItem);
                    if (initial >= 0 && initial <= (double)tolerance.Maximum) tolerance.Value = (decimal)initial;
                    inputInitialized = true;
                }
                foreach (var item in items)
                { int index = grid.Rows.Add(!hadRows || selectedIds.Contains(item.Id), item.Name, TypeLabel(item.Type), item.Tolerance.ToString("G9") + " " + item.DocumentUnits,
                    null, null, item.CompositeObjectClashing ? "Enabled" : "Disabled", null, null); grid.Rows[index].Tag = item; }
                ApplyTestFilter();
                InvalidatePreview();
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        private void Preview()
        {
            try
            {
                grid.EndEdit(); tolerance.Validate(); InvalidatePreview();
                var types = new[] { ClashTestType.Hard, ClashTestType.HardConservative, ClashTestType.Clearance, ClashTestType.Duplicate };
                plan = service.Preview(SelectedRows().Select(row => (ClashToleranceItem)row.Tag), (double)tolerance.Value,
                    (Units)units.SelectedItem, type.SelectedIndex == 0 ? (ClashTestType?)null : types[type.SelectedIndex - 1],
                    composite.SelectedIndex == 0 ? (bool?)null : composite.SelectedIndex == 1);
                var byId = plan.ToDictionary(item => item.Original.Id);
                foreach (var row in SelectedRows())
                {
                    var item = byId[((ClashToleranceItem)row.Tag).Id]; row.Cells[4].Value = TypeLabel(item.Type);
                    row.Cells[5].Value = item.Tolerance.ToString("G9") + " " + item.Original.DocumentUnits;
                    row.Cells[6].Value = item.Original.CompositeObjectClashing ? "Enabled" : "Disabled";
                    row.Cells[7].Value = item.CompositeObjectClashing ? "Enabled" : "Disabled";
                    row.Cells[8].Value = item.Changed ? "Update" : "Unchanged";
                }
                var changed = plan.Count(item => item.Changed); apply.Enabled = changed > 0;
                status.Text = changed + " test(s) to update; " + (plan.Count - changed) + " unchanged. Review proposed values.";
            }
            catch (Exception ex) { InvalidatePreview(); status.Text = ex.Message; }
        }
        private void ApplyPreview()
        {
            if (plan == null || !apply.Enabled) return;
            var preview = plan;
            if (MessageBox.Show(this, "Update tolerance/settings for " + preview.Count(item => item.Changed) + " clash test(s)?\n\nExisting results may become stale. Rerun the tests in Clash Detective afterward and save your document to persist the settings.",
                "Confirm tolerance update", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                apply.Enabled = false; UseWaitCursor = true;
                int changed = service.Apply(preview); RefreshTests();
                status.Text = "Updated " + changed + " test(s). Rerun in Clash Detective; save the document when ready.";
            }
            catch (Exception ex) { InvalidatePreview(); MessageBox.Show(this, ex.Message, "Tolerance Updater", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { UseWaitCursor = false; }
        }

        private void ReRunClashes()
        {
            grid.EndEdit();
            var selected = SelectedRows().Select(row => (ClashToleranceItem)row.Tag).ToList();
            if (selected.Count == 0) { status.Text = "Select at least one clash test to rerun."; return; }
            if (plan != null && plan.Any(item => item.Changed))
            {
                status.Text = "Apply the previewed tolerance changes before rerunning these tests.";
                return;
            }
            var message = "Rerun " + selected.Count + " selected clash test(s) using their current saved settings?\n\n"
                + "Navisworks runs tests synchronously, so the dialog may not respond until they finish. This operation cannot be safely cancelled. Save the document afterward to retain changes.";
            if (MessageBox.Show(this, message, "Confirm clash rerun", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                rerun.Enabled = false; apply.Enabled = false; UseWaitCursor = true;
                status.Text = "Rerunning " + selected.Count + " selected clash test(s) synchronously…";
                status.Refresh();
                int completed = service.RunTests(selected);
                RefreshTests();
                status.Text = "Reran " + completed + " selected clash test(s). Review their results and save the document when ready.";
            }
            catch (Exception ex)
            {
                RefreshTests();
                status.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Clash rerun incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { UseWaitCursor = false; }
        }
    }
}
