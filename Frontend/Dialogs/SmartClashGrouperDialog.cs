using System;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;

namespace NavPro.Frontend.Dialogs
{
    public sealed class SmartClashGrouperDialog : ReviewToolForm
    {
        private readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        private readonly ComboBox property = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        private readonly NumericUpDown size = new NumericUpDown { Minimum = .001M, Maximum = 1000000, DecimalPlaces = 3, Value = 5, Width = 90 };
        private readonly TextBox prefix = new TextBox { Text = "NavPro", Width = 100, MaxLength = 60 };
        private readonly CheckBox resolved = new CheckBox { Text = "Include resolved", AutoSize = true };
        private readonly Button apply;
        private ReviewSnapshot snapshot;
        private ClashGroupingPlan plan;
        public SmartClashGrouperDialog(Document document) : this(new ClashReviewSession(document)) { }
        internal SmartClashGrouperDialog(ClashReviewSession session) : base("Smart Clash Grouper", session)
        {
            mode.Items.AddRange(new object[] { "Shared composite element A", "Shared composite element B", "Model pair", "Property on A", "Property on B", "3D spatial grid", "Distance clusters (3D)", "Level", "Zone" }); mode.SelectedIndex = 0;
            Option("Group by", mode); Option("Property", property); Option("Grid / cluster distance", size); Option("Prefix", prefix); Options.Controls.Add(resolved);
            Help.Text = "Existing groups and singletons remain unchanged. Missing keys are skipped. Grid uses XYZ cells anchored at document origin; neighbouring cells remain separate. Distances use document units. Distance clusters connect neighbours within the radius; cluster diameter may exceed it. Level/zone conflicts and missing data are skipped.";
            AddButton(Actions, "Load results", () => Guard(LoadResults)); AddButton(Actions, "Preview groups", () => Guard(Preview));
            AddButton(Actions, "Check all groups", () => { foreach (DataGridViewRow row in Results.Rows) row.Cells[0].Value = true; });
            AddButton(Actions, "Check none", () => { foreach (DataGridViewRow row in Results.Rows) row.Cells[0].Value = false; });
            apply = AddButton(Actions, "Apply checked groups", () => Guard(Apply)); apply.Enabled = false;
            AddButton(Actions, "Show first clash", () => Guard(() => { if (plan != null && Results.CurrentRow?.Tag is ReviewGroup group) { Session.Validate(snapshot); Session.ShowClash(snapshot.Clashes.First(row => row.TestId == group.TestId && row.Id == group.ResultIds[0])); } }));
            AddButton(Actions, "Explain selected group", () => { if (Results.CurrentRow?.Tag is ReviewGroup group) MessageBox.Show(this, string.Join("\r\n", group.ResultIds.Select(id => snapshot.Clashes.First(r => r.TestId == group.TestId && r.Id == id).Name + ": " + group.Reasons[id])), "Group membership reasons"); });
            CheckColumn(); Column("Test", 230); Column("Proposed group", 350); Column("Clashes", 80);
            mode.SelectedIndexChanged += (s, e) => { RefreshProperties(); InvalidatePlan(); }; property.SelectedIndexChanged += (s, e) => InvalidatePlan();
            prefix.TextChanged += (s, e) => InvalidatePlan(); size.ValueChanged += (s, e) => InvalidatePlan(); resolved.CheckedChanged += (s, e) => InvalidatePlan(); FinishLayout(apply);
        }
        protected override void InvalidateWork(string message) { snapshot = null; InvalidatePlan(); base.InvalidateWork(message); }
        private void InvalidatePlan() { plan = null; if (apply != null) apply.Enabled = false; Results.Rows.Clear(); }
        private void LoadResults() { snapshot = Session.ReadSnapshot(SelectedTestIds); RefreshProperties(); InvalidatePlan(); Status.Text = snapshot.Clashes.Count + " leaf clashes loaded; units: " + snapshot.Units + ". Preview groups."; }
        private void RefreshProperties()
        {
            property.Items.Clear(); property.Enabled = mode.SelectedIndex == 3 || mode.SelectedIndex == 4;
            if (snapshot == null || !property.Enabled) return;
            var keys = snapshot.Clashes.SelectMany(row => mode.SelectedIndex == 3 ? row.PropertiesA.Keys : row.PropertiesB.Keys).Distinct().OrderBy(key => key).ToArray();
            property.Items.AddRange(keys); if (keys.Length > 0) property.SelectedIndex = 0;
        }
        private void Preview()
        {
            if (snapshot == null) throw new InvalidOperationException("Load selected test results first.");
            Session.Validate(snapshot); InvalidatePlan();
            plan = SmartGroupingPlanner.Build(snapshot, (ClashGroupingMode)mode.SelectedIndex, property.SelectedItem as string, (double)size.Value, prefix.Text, resolved.Checked);
            foreach (var group in plan.Groups) { int index = Results.Rows.Add(true, group.TestName, group.Name, group.ResultIds.Count); Results.Rows[index].Tag = group; }
            apply.Enabled = plan.Groups.Count > 0; Status.Text = plan.Groups.Count + " groups / " + plan.GroupedResults + " results proposed; " + plan.Skipped + " results left unchanged.";
        }
        private void Apply()
        {
            Results.EndEdit(); if (plan == null) return;
            var groups = Results.Rows.Cast<DataGridViewRow>().Where(row => Convert.ToBoolean(row.Cells[0].Value ?? false)).Select(row => (ReviewGroup)row.Tag).ToList();
            if (groups.Count == 0) throw new InvalidOperationException("Check at least one proposed group.");
            Session.Validate(snapshot);
            if (!Confirm("Move " + groups.Sum(group => group.ResultIds.Count) + " ungrouped results into " + groups.Count + " new groups? Existing groups and result review data are preserved. Tests will not run or save automatically.")) return;
            SetBusy(true);
            try { int count = Session.ApplyGroups(plan, groups); CoordinationChangeLog.Record(Session.Document, "Smart Clash Grouper", string.Join(", ", groups.Select(g => g.TestName).Distinct()), "Ungrouped: " + count, "Created " + groups.Count + " groups: " + string.Join("; ", groups.Select(g => g.Name))); InvalidateWork("Grouped " + count + " clashes. Review Clash Detective and save when ready."); }
            finally { InvalidatePlan(); AcknowledgeOwnChange(); SetBusy(false); }
        }
    }
}
