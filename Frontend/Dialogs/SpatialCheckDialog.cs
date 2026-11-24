using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;

namespace NavPro.Frontend.Dialogs
{
    /// <summary>Shared rule workflow for native utility clearances and explicitly modeled maintenance envelopes.</summary>
    public sealed class SpatialCheckDialog : Form
    {
        private readonly ClashReviewSession session;
        private readonly SpatialCheckService service;
        private readonly bool maintenance;
        private readonly ComboBox sideA = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
        private readonly ComboBox sideB = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
        private readonly ComboBox units = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 125 };
        private readonly NumericUpDown distance = new NumericUpDown { DecimalPlaces = 6, Maximum = 1000000, Width = 130, Increment = .01M };
        private readonly TextBox ruleName = new TextBox { Width = 300, MaxLength = 130 };
        private readonly TextBox source = new TextBox { Width = 200, MaxLength = 200 }, ruleRevision = new TextBox { Width = 100, MaxLength = 80 };
        private readonly CheckBox acknowledge = new CheckBox { AutoSize = true };
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, MultiSelect = false };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Button create, run;
        private readonly List<SpatialCheckRule> rules = new List<SpatialCheckRule>();
        private List<CreatedSpatialTest> created;
        private readonly Timer watch = new Timer { Interval = 500 };
        private long revision, previewRevision = -1, createdRevision = -1;
        public SpatialCheckDialog(Document document, bool maintenance) : this(new ClashReviewSession(document), maintenance) { }
        internal SpatialCheckDialog(ClashReviewSession session, bool maintenance)
        {
            this.session = session; this.maintenance = maintenance; if (session != null) service = new SpatialCheckService(session, maintenance);
            string title = maintenance ? "Access and Maintenance Clearance Review" : "Utility Separation Checker";
            Name = maintenance ? "MaintenanceClearance" : "UtilitySeparation";
            Text = "NavPro | " + title; Font = DialogTheme.CreateFont(9.5f); AutoScaleMode = AutoScaleMode.Dpi; Size = new Size(1150, 780); MinimumSize = new Size(1060, 720); StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            var header = new Panel { BackColor = DialogTheme.Brand, Dock = DockStyle.Fill };
            header.Controls.Add(new Label { Text = title, AutoSize = true, Font = DialogTheme.CreateFont(19, FontStyle.Bold), ForeColor = System.Drawing.Color.White, Location = new Point(12, 16) });
            var pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = System.Drawing.Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Right, Top = 24 };
            header.Resize += (s, e) => pin.Left = header.ClientSize.Width - pin.Width - 14; pin.CheckedChanged += (s, e) => TopMost = pin.Checked; header.Controls.Add(pin); root.Controls.Add(header, 0, 0);
            var inputs = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(4) };
            AddField(inputs, maintenance ? "Envelope set" : "Utility A", sideA); AddField(inputs, maintenance ? "Surroundings" : "Utility B", sideB);
            AddField(inputs, "Rule name", ruleName);
            foreach (Units value in Enum.GetValues(typeof(Units))) units.Items.Add(value); units.SelectedItem = Units.Meters;
            if (!maintenance)
            {
                var gap = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty };
                gap.Controls.Add(distance); gap.Controls.Add(units); AddField(inputs, "Required 3D gap", gap);
            }
            acknowledge.Text = maintenance ? "Selected A sets are modeled, approved access/maintenance volumes" : "The entered distances come from approved project requirements";
            inputs.Controls.Add(acknowledge);
            if (!maintenance) { AddField(inputs, "Rule source", source); AddField(inputs, "Revision", ruleRevision); }
            var explanation = new Label { AutoSize = true, MaximumSize = new Size(990, 0), Text = maintenance
                ? "Checks Hard intersections between supplied envelope surfaces and surrounding surfaces. Fully contained objects may not intersect envelope surfaces; containment and removal paths need separate review. The tool does not infer envelope dimensions. Exclude owning equipment from B as required."
                : "Creates native Clearance tests using 3D surface-to-surface gaps, including physical clashes. This is not a horizontal-only, vertical-only or centreline-distance check. Use physical utility solids and include insulation/sleeves as required. No universal separation values are supplied." };
            inputs.Controls.Add(explanation); root.Controls.Add(inputs, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            AddButton(actions, "Refresh sets", () => Guard(RefreshSets)); AddButton(actions, "Add rule", () => Guard(AddRule));
            if (!maintenance)
            {
                AddButton(actions, "Import approved Excel / CSV", () => Guard(ImportRules));
                AddButton(actions, "Save rule CSV template", () => Guard(() => { using (var save = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "NavPro-approved-separation-rules.csv" }) if (save.ShowDialog(this) == DialogResult.OK) System.IO.File.WriteAllText(save.FileName, ApprovedRuleTable.Header + "\r\n"); }));
            }
            AddButton(actions, "Remove selected rule", () => { if (grid.CurrentRow != null) { rules.RemoveAt(grid.CurrentRow.Index); InvalidatePreview(); RenderRules(); } });
            AddButton(actions, "Preview checks", () => Guard(Preview)); create = AddButton(actions, "Create previewed checks", () => Guard(Create)); create.Enabled = false;
            run = AddButton(actions, "Run created checks", () => Guard(Run)); run.Enabled = false;
            AddButton(actions, "Export results CSV", () => Guard(Export)); root.Controls.Add(actions, 0, 2);
            foreach (var column in new[] { "Rule / test name", "A set", "B set", "Type", "Gap / tolerance", "Units", "State / current findings" }) grid.Columns.Add(column, column);
            grid.Columns.Add("Source", "Rule source"); grid.Columns.Add("Revision", "Rule revision");
            grid.Columns[0].Width = 230; grid.Columns[1].Width = grid.Columns[2].Width = 210; grid.Columns[6].Width = 280;
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            root.Controls.Add(grid, 0, 3);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(status, 0, 0); var close = AddButton(footer, "Close", Close); footer.SetCellPosition(close, new TableLayoutPanelCellPosition(1, 0)); CancelButton = close; root.Controls.Add(footer, 0, 4); Controls.Add(root);
            acknowledge.CheckedChanged += (s, e) => InvalidatePreview();
            watch.Tick += (s, e) => { try { session.CheckHost(); if (session.Revision != revision) { revision = session.Revision; InvalidatePreview(); status.Text = "Model, tests or sets changed. Refresh sets and preview again."; } } catch (Exception ex) { InvalidatePreview(); status.Text = ex.Message; } };
            Disposed += (s, e) => { watch.Stop(); watch.Dispose(); session?.Dispose(); };
            DialogTheme.Apply(this, create, run); if (session != null) { RefreshSets(); revision = session.Revision; watch.Start(); }
        }
        private static void AddLabel(Control parent, string text) => parent.Controls.Add(new Label { Text = text, AutoSize = true, Padding = new Padding(4, 6, 2, 0) });
        private static void AddField(Control parent, string text, Control input)
        {
            var field = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 0, 8, 0) };
            AddLabel(field, text); field.Controls.Add(input); parent.Controls.Add(field);
        }
        private static Button AddButton(Control parent, string text, System.Action action) { var button = new Button { Text = text, AutoSize = true }; button.Click += (s, e) => action(); parent.Controls.Add(button); return button; }
        private void Guard(System.Action action) { try { action(); } catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        private void InvalidatePreview()
        {
            previewRevision = -1; createdRevision = -1; if (create != null) create.Enabled = false; if (run != null) run.Enabled = false;
            foreach (DataGridViewRow row in grid.Rows) row.Cells[6].Value = "STALE / not checked — preview or run again";
        }
        private void RefreshSets()
        {
            var sets = service.ReadSets(); sideA.Items.Clear(); sideB.Items.Clear(); sideA.Items.AddRange(sets.Cast<object>().ToArray()); sideB.Items.AddRange(sets.Cast<object>().ToArray());
            if (sets.Count > 0) sideA.SelectedIndex = 0; if (sets.Count > 1) sideB.SelectedIndex = 1;
            rules.Clear(); created = null; InvalidatePreview(); RenderRules(); status.Text = sets.Count + " saved sets. Define explicit check pairs and preview.";
        }
        private void AddRule()
        {
            session.CheckHost(); if (!(sideA.SelectedItem is ClashSetChoice a) || !(sideB.SelectedItem is ClashSetChoice b)) throw new InvalidOperationException("Choose both saved sets.");
            if (!acknowledge.Checked) throw new InvalidOperationException("Confirm the source of the clearance/envelope requirements.");
            if (!maintenance && (string.IsNullOrWhiteSpace(source.Text) || string.IsNullOrWhiteSpace(ruleRevision.Text))) throw new InvalidOperationException("Enter the approved rule source and revision.");
            if (a.Set.Guid == b.Set.Guid || (!maintenance && distance.Value <= 0)) throw new InvalidOperationException("Choose different sets and a positive utility separation.");
            string name = (maintenance ? "Maintenance - " : "Separation - ") + (string.IsNullOrWhiteSpace(ruleName.Text) ? a.Name + " vs " + b.Name : ruleName.Text.Trim());
            rules.Add(new SpatialCheckRule { Name = name, SetAId = a.Set.Guid.ToString("D"), SetBId = b.Set.Guid.ToString("D"), Distance = maintenance ? 0 : (double)distance.Value, InputUnits = (Units)units.SelectedItem, Source = source.Text.Trim(), Revision = ruleRevision.Text.Trim() });
            InvalidatePreview(); RenderRules();
        }
        private void RenderRules()
        {
            grid.Rows.Clear(); foreach (var rule in rules) grid.Rows.Add(rule.Name, SetName(rule.SetAId), SetName(rule.SetBId), maintenance ? "Hard (envelope)" : "Clearance", rule.Distance, rule.InputUnits, "Not previewed / not run", rule.Source, rule.Revision);
        }
        private void ImportRules()
        {
            session.CheckHost();
            using (var open = new OpenFileDialog { Filter = "Approved rules (*.csv;*.xlsx)|*.csv;*.xlsx" })
            {
                if (open.ShowDialog(this) != DialogResult.OK) return;
                var imported = ApprovedRuleTable.Read(open.FileName); var prepared = new List<SpatialCheckRule>();
                var sets = sideA.Items.Cast<ClashSetChoice>().ToList();
                Func<string, ClashSetChoice> resolve = text => { var matches = sets.Where(s => s.Path == text || s.Name == text || s.Set.Guid.ToString("D") == text).ToList(); if (matches.Count != 1) throw new InvalidOperationException("Set is missing or ambiguous: " + text + ". Use the full set path or GUID."); return matches[0]; };
                foreach (var row in imported)
                {
                    if (!Enum.TryParse(row.Units, true, out Units inputUnits) || !Enum.IsDefined(typeof(Units), inputUnits)) throw new InvalidOperationException("Unsupported Navisworks unit name: " + row.Units);
                    prepared.Add(new SpatialCheckRule { Name = "Separation - " + row.Name, SetAId = resolve(row.SetA).Set.Guid.ToString("D"), SetBId = resolve(row.SetB).Set.Guid.ToString("D"), Distance = row.Gap, InputUnits = inputUnits, Source = row.Source, Revision = row.Revision });
                }
                if (prepared.Any(p => rules.Any(r => r.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))) throw new InvalidOperationException("An imported rule name is already in the queue. No rules were imported.");
                rules.AddRange(prepared); InvalidatePreview(); RenderRules(); status.Text = "Imported " + prepared.Count + " approved rules. Review source/revision, confirm requirements, then preview.";
            }
        }
        private string SetName(string id) => sideA.Items.Cast<ClashSetChoice>().FirstOrDefault(set => set.Set.Guid.ToString("D") == id)?.Path ?? "Removed set";
        private void Preview()
        {
            if (!acknowledge.Checked) throw new InvalidOperationException("Confirm the project requirement/envelope basis."); InvalidatePreview();
            var plan = service.Preview(rules); RenderRules();
            for (int i = 0; i < plan.Count; i++) { grid.Rows[i].Cells[4].Value = plan[i].Tolerance; grid.Rows[i].Cells[5].Value = session.UnitsLabel; grid.Rows[i].Cells[6].Value = "Ready to create; not run"; }
            previewRevision = session.Revision; create.Enabled = plan.Count > 0; status.Text = plan.Count + " checks ready. Preview tolerances are in document units. Creating definitions does not run checks.";
        }
        private void Create()
        {
            if (previewRevision < 0) throw new InvalidOperationException("Preview the rules first.");
            if (MessageBox.Show(this, "Create " + rules.Count + " native clash test definitions? Existing tests remain. Run the new checks separately; no file is saved automatically.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            created = service.Create(rules, previewRevision); revision = createdRevision = session.Revision; create.Enabled = false; run.Enabled = created.Count > 0;
            foreach (DataGridViewRow row in grid.Rows) row.Cells[6].Value = "Created; NOT RUN"; status.Text = "Created " + created.Count + " checks. Run created checks to inspect findings.";
        }
        private void Run()
        {
            if (createdRevision < 0) throw new InvalidOperationException("Create previewed checks first; changed scopes must be reviewed in Clash Detective.");
            if (MessageBox.Show(this, "Run only these " + created.Count + " newly created checks? Native tests run synchronously; the window may pause and cannot safely cancel a running test.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            watch.Stop(); UseWaitCursor = true; run.Enabled = false;
            try
            {
                var counts = service.Run(created, createdRevision, (done, total) => status.Text = "Completed " + done + " of " + total + " native tests.");
                for (int i = 0; i < created.Count; i++) grid.Rows[i].Cells[6].Value = counts[created[i].Id] == 0
                    ? (maintenance ? "No surface findings; containment not checked" : "No current findings in checked scope")
                    : counts[created[i].Id] + " finding(s) — review Clash Detective";
                revision = createdRevision = session.Revision; status.Text = "Checks completed. Review native results and coverage; this is not a compliance certification. Save your document when ready.";
            }
            catch (Exception ex) { CoordinationChangeLog.Record(session.Document, maintenance ? "Maintenance checks" : "Utility checks", "Run created checks", "Native batch requested", ex.Message + "; inspect Clash Detective for completed tests", "Incomplete / possible partial results"); foreach (DataGridViewRow row in grid.Rows) row.Cells[6].Value = "Run incomplete — inspect Clash Detective"; createdRevision = -1; throw; }
            finally { UseWaitCursor = false; watch.Start(); }
        }
        private void Export()
        {
            session.CheckHost(); if (grid.Rows.Count == 0) throw new InvalidOperationException("Add rules first.");
            using (var save = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = maintenance ? "NavPro-maintenance-review.csv" : "NavPro-utility-separation.csv" })
                if (save.ShowDialog(this) == DialogResult.OK)
                {
                    var rows = new List<IEnumerable<string>> { grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText).ToArray() };
                    rows.AddRange(grid.Rows.Cast<DataGridViewRow>().Select(r => r.Cells.Cast<DataGridViewCell>().Select(c => Convert.ToString(c.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray()));
                    Backend.Core.ReviewCsv.Write(save.FileName, rows); status.Text = "Exported current rule/result states.";
                }
        }
    }
}
