using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NavPro.Backend.Models;
using NavPro.Backend.Services;

namespace NavPro.Frontend.Ui
{
    /// <summary>Shared modeless review shell. Maintains selection independently of the filtered visual list.</summary>
    public class ReviewToolForm : Form
    {
        protected readonly ClashReviewSession Session;
        protected readonly DataGridView Results = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        protected readonly Label Status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        protected readonly FlowLayoutPanel Options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(4) };
        protected readonly FlowLayoutPanel Actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        protected readonly Panel Extra = new Panel { Dock = DockStyle.Fill, Height = 0, Visible = false };
        protected readonly Label Help = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(3, 6, 3, 6) };
        private readonly CheckedListBox tests = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true, IntegralHeight = false };
        private readonly TextBox filter = new TextBox { Dock = DockStyle.Fill, AccessibleName = "Filter clash tests" };
        private readonly HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
        private List<ReviewTest> all = new List<ReviewTest>();
        private readonly Timer watch = new Timer { Interval = 500 };
        private long revision;
        private bool loading;
        protected bool Busy { get; private set; }
        protected ReviewToolForm(string title, ClashReviewSession session)
        {
            Session = session; Text = "NavPro | " + title; Font = DialogTheme.CreateFont(9.5f); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1200, 800); MinimumSize = new Size(1060, 720); StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            var header = new Panel { Dock = DockStyle.Fill, BackColor = DialogTheme.Brand };
            header.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.White, Font = DialogTheme.CreateFont(20, FontStyle.Bold), Location = new Point(12, 14) });
            var pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Right, Top = 22 };
            header.Resize += (s, e) => pin.Left = header.ClientSize.Width - pin.Width - 16; pin.CheckedChanged += (s, e) => TopMost = pin.Checked;
            header.Controls.Add(pin); root.Controls.Add(header, 0, 0); root.Controls.Add(Options, 0, 1); root.Controls.Add(Help, 0, 2); root.Controls.Add(Actions, 0, 3);
            var picker = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(0, 0, 8, 0) };
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            picker.RowStyles.Add(new RowStyle(SizeType.AutoSize)); picker.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            picker.RowStyles.Add(new RowStyle(SizeType.AutoSize)); picker.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            picker.Controls.Add(new Label { Text = "Clash tests — checks include hidden rows", AutoSize = true }, 0, 0); picker.Controls.Add(filter, 0, 1);
            var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill }; AddButton(tools, "All", () => SelectTests(true)); AddButton(tools, "None", () => SelectTests(false)); AddButton(tools, "Refresh", ReloadTests);
            foreach (Button button in tools.Controls) button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            picker.Controls.Add(tools, 0, 2); picker.Controls.Add(tests, 0, 3);
            root.Controls.Add(ResizableLayout.Split(picker, Results, Orientation.Vertical, 0.25, "Clash tests and review results"), 0, 4);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 6, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(Status, 0, 0); var close = AddButton(footer, "Close", Close); footer.SetCellPosition(close, new TableLayoutPanelCellPosition(1, 0)); CancelButton = close;
            root.Controls.Add(footer, 0, 5); Controls.Add(root);
            filter.TextChanged += (s, e) => RebuildTests();
            tests.ItemCheck += (s, e) => { if (loading) return; var item = (ReviewTest)tests.Items[e.Index]; if (e.NewValue == CheckState.Checked) selected.Add(item.Id); else selected.Remove(item.Id); InvalidateWork("Selection changed. Load/preview again."); };
            watch.Tick += (s, e) =>
            {
                if (Session == null || Busy) return;
                try { Session.CheckHost(); if (Session.Revision != revision) { revision = Session.Revision; OnHostInvalidated("The model, tests or sets changed. Refresh and preview again."); } }
                catch (Exception ex) { OnHostInvalidated(ex.Message); }
            };
            Disposed += (s, e) => { watch.Stop(); watch.Dispose(); Session?.Dispose(); };
        }
        protected void FinishLayout(params Button[] primary)
        { DialogTheme.Apply(this, primary); if (Session != null) { ReloadTests(); revision = Session.Revision; watch.Start(); } }
        protected virtual void InvalidateWork(string message) { Status.Text = message; }
        protected virtual void OnHostInvalidated(string message) => InvalidateWork(message);
        // Call only after our own mutation invalidates its detached work. Keep its
        // completion message while still watching subsequent external changes.
        protected void AcknowledgeOwnChange() { revision = Session.Revision; }
        protected void SetBusy(bool busy)
        {
            Busy = busy; Options.Enabled = !busy; tests.Enabled = !busy; filter.Enabled = !busy; UseWaitCursor = busy;
        }
        protected IReadOnlyCollection<string> SelectedTestIds => selected.ToList().AsReadOnly();
        protected void ReloadTests()
        {
            try { all = Session.ReadTests(); selected.RemoveWhere(id => !all.Any(test => test.Id == id)); if (selected.Count == 0) foreach (var test in all) selected.Add(test.Id); RebuildTests(); InvalidateWork(all.Count + " tests. Check tests and load results."); }
            catch (Exception ex) { InvalidateWork(ex.Message); }
        }
        private void SelectTests(bool value)
        { if (value) foreach (var test in all) selected.Add(test.Id); else selected.Clear(); RebuildTests(); InvalidateWork(selected.Count + " tests checked. Load/preview again."); }
        private void RebuildTests()
        {
            loading = true; tests.BeginUpdate();
            try { tests.Items.Clear(); foreach (var test in all.Where(test => test.Name.IndexOf(filter.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)) tests.Items.Add(test, selected.Contains(test.Id)); tests.HorizontalExtent = all.Count == 0 ? 0 : all.Max(test => TextRenderer.MeasureText(test.ToString(), tests.Font).Width) + 24; }
            finally { tests.EndUpdate(); loading = false; }
        }
        protected static Button AddButton(Control parent, string text, System.Action action)
        { var button = new Button { Text = text, AutoSize = true }; button.Click += (s, e) => action(); parent.Controls.Add(button); return button; }
        protected void Option(string label, Control control)
        {
            var field = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 0, 8, 0) };
            field.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(4, 5, 2, 0) }); field.Controls.Add(control); Options.Controls.Add(field);
        }
        protected void Column(string title, int width = 130, bool editable = false)
        { Results.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, Width = width, ReadOnly = !editable, SortMode = DataGridViewColumnSortMode.NotSortable }); }
        protected void CheckColumn(string title = "Use") => Results.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = title, Width = 48, SortMode = DataGridViewColumnSortMode.NotSortable });
        protected void Guard(System.Action action)
        { try { action(); } catch (Exception ex) { if (Session != null) CoordinationChangeLog.Record(Session.Document, Text, "Stopped operation", "Operation requested", ex.Message + "; inspect native state if mutation had started", "Failed / completion unverified"); Status.Text = ex.Message; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        protected bool Confirm(string message) => MessageBox.Show(this, message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        protected void ExportGrid()
        {
            using (var picker = new SaveFileDialog { Filter = "CSV report (*.csv)|*.csv", FileName = "NavPro-review.csv" })
                if (picker.ShowDialog(this) == DialogResult.OK) Guard(() =>
                {
                    Session?.CheckHost(); Results.EndEdit();
                    var rows = new List<IEnumerable<string>> { Results.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText).ToArray() };
                    rows.AddRange(Results.Rows.Cast<DataGridViewRow>().Select(row => row.Cells.Cast<DataGridViewCell>().Select(cell => Convert.ToString(cell.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray()));
                    Backend.Core.ReviewCsv.Write(picker.FileName, rows); Status.Text = "Report exported.";
                });
        }
    }
}
