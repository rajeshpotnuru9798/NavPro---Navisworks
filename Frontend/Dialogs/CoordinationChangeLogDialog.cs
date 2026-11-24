using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;

namespace NavPro.Frontend.Dialogs
{
    public sealed class CoordinationChangeLogDialog : Form
    {
        private readonly object document;
        private readonly TextBox filter = new TextBox { Width = 300, AccessibleName = "Filter change log" };
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            ReadOnly = true, RowHeadersVisible = false, AutoGenerateColumns = false };
        private readonly Label status = new Label { AutoSize = true };
        private List<CoordinationChange> visible = new List<CoordinationChange>();
        public CoordinationChangeLogDialog(object document)
        {
            this.document = document; Text = "NavPro | Coordination Change Log"; Font = DialogTheme.CreateFont(9.5f); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1150, 720); MinimumSize = new Size(980, 620); StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(1060, 0), Text = "Local history follows the saved document path across sessions. Unsaved documents use session memory. Capture named revisions to compare result fields by GUID. Native manual edits appear only in captured comparisons. This journal is independent of Undo; it cannot reverse changes.", Font = DialogTheme.CreateFont(11, FontStyle.Bold) }, 0, 0);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; tools.Controls.Add(filter);
            var refresh = new Button { Text = "Refresh", AutoSize = true }; refresh.Click += (s, e) => Reload(); tools.Controls.Add(refresh);
            var export = new Button { Text = "Export visible log CSV", AutoSize = true }; export.Click += (s, e) => Export(); tools.Controls.Add(export);
            var pin = new CheckBox { Text = "Pin window", AutoSize = true }; pin.CheckedChanged += (s, e) => TopMost = pin.Checked; tools.Controls.Add(pin); var revisionName = new TextBox { Width = 150, AccessibleName = "Coordination revision label" }; tools.Controls.Add(revisionName);
            var capture = new Button { Text = "Capture revision", AutoSize = true }; capture.Click += (s, e) => {
                try { if (!(document is Autodesk.Navisworks.Api.Document native)) throw new InvalidOperationException("Open a native model first."); using (var session = new ClashReviewSession(native)) {
                    var rows = session.ReadSnapshot(session.ReadTests().Select(t => t.Id), includeItemMetadata: false).Clashes;
                    CoordinationChangeLog.CaptureRevision(native, revisionName.Text, rows.ToDictionary(r => r.Identity, r => new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { r.TestName, r.Name, r.Description, r.Status, r.ParentGroup, r.X, r.Y, r.Z })));
                } status.Text = "Revision captured locally."; }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
            }; tools.Controls.Add(capture);
            var compare = new Button { Text = "Compare latest revisions", AutoSize = true }; compare.Click += (s, e) => {
                try { using (var dialog = new Form { Name = "CoordinationRevisionComparison", Text = "Coordination revision comparison", Width = 1000, Height = 650 }) { dialog.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Text = CoordinationChangeLog.CompareLatest(document) }); DialogTheme.Apply(dialog); dialog.ShowDialog(this); } }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
            }; tools.Controls.Add(compare); root.Controls.Add(tools, 0, 1);
            foreach (var field in new[] { "TimeUtc", "User", "Tool", "Scope", "Before", "After", "Outcome" }) grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = field == "TimeUtc" ? "Time (UTC)" : field, DataPropertyName = field, Width = field == "TimeUtc" ? 215 : 200 });
            root.Controls.Add(grid, 0, 2); var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; footer.Controls.Add(status);
            var close = new Button { Text = "Close", AutoSize = true }; close.Click += (s, e) => Close(); footer.Controls.Add(close); CancelButton = close; root.Controls.Add(footer, 0, 3);
            Controls.Add(root); filter.TextChanged += (s, e) => Reload(); DialogTheme.Apply(this, export); Reload();
        }
        private void Reload()
        {
            var all = CoordinationChangeLog.Read(document); visible = all.Where(row => string.Join(" ", row.TimeUtc, row.User, row.Tool, row.Scope, row.Before, row.After).IndexOf(filter.Text, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
            grid.DataSource = visible; status.Text = (CoordinationChangeLog.PersistenceError == null ? "" : CoordinationChangeLog.PersistenceError + " | ") + visible.Count + " of " + all.Count + " recorded entries. Refresh after other NavPro actions.";
        }
        private void Export()
        {
            using (var save = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "NavPro-coordination-change-log.csv" })
                if (save.ShowDialog(this) == DialogResult.OK)
                    try { CoordinationChangeLog.Export(save.FileName, visible); status.Text = "Exported " + visible.Count + " entries."; }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
