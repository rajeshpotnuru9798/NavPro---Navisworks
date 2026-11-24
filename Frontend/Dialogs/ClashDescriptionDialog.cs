using System;
using System.Linq;
using System.Collections.Generic;
using NavPro.Backend.Core;
using System.Threading;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ClashDescriptionDialog : ReviewToolForm
    {
        private readonly ComboBox style = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, AccessibleName = "Description style" };
        private readonly Button generate, cancel, apply;
        private ReviewSnapshot snapshot;
        private CancellationTokenSource cancellation;
        private bool ready, writingDraft;
        private readonly HashSet<DataGridViewRow> edited = new HashSet<DataGridViewRow>();
        private readonly TextBox template = new TextBox { Width = 480, MaxLength = 2000, AccessibleName = "Project description template" };
        private readonly ComboBox templates = new ComboBox { Width = 180, AccessibleName = "Project template name" };
        private Dictionary<string, string> savedTemplates = new Dictionary<string, string>();
        public ClashDescriptionDialog(Document document) : this(new ClashReviewSession(document)) { }
        internal ClashDescriptionDialog(ClashReviewSession session) : base("AI Clash Description Assistant", session)
        {
            style.Items.AddRange(new object[] { ClashDescriptionGenerator.ConciseStyle, ClashDescriptionGenerator.DetailedStyle, ClashDescriptionGenerator.ReviewNoteStyle }); style.SelectedIndex = 0;
            Option("Style", style);
            Option("Project template", templates); Option("Text", template);
            try { savedTemplates = ReviewPreferences.Read<Dictionary<string, string>>("description-templates"); } catch (Exception ex) { Status.Text = ex.Message; }
            templates.Items.AddRange(savedTemplates.Keys.OrderBy(v => v).Cast<object>().ToArray());
            templates.SelectedIndexChanged += (s, e) => { if (savedTemplates.TryGetValue(templates.Text, out var text)) template.Text = text; };
            template.Text = ProjectDescriptionTemplate.Default;
            AddButton(Actions, "Save project template", () => Guard(() => {
                if (string.IsNullOrWhiteSpace(templates.Text) || templates.Text.Length > 100) throw new InvalidOperationException("Enter a template name of 1–100 characters.");
                ProjectDescriptionTemplate.Render(new ReviewClash(), template.Text, out _);
                savedTemplates[templates.Text.Trim()] = template.Text; ReviewPreferences.Write("description-templates", savedTemplates);
                if (!templates.Items.Contains(templates.Text.Trim())) templates.Items.Add(templates.Text.Trim()); Status.Text = "Project template saved locally.";
            }));
            AddButton(Actions, "Generate project template", () => Generate(true));
            Help.Text = "Select tests → Load clashes → check rows → Generate → review/edit drafts → Apply checked. Built-in offline drafting uses available clash data; no AI service, key or network connection is used. Review every draft before applying.";
            AddButton(Actions, "Load clashes", () => Guard(LoadClashes));
            generate = AddButton(Actions, "Generate checked drafts", () => Generate());
            cancel = AddButton(Actions, "Cancel generation", () => cancellation?.Cancel()); cancel.Enabled = false;
            apply = AddButton(Actions, "Apply checked descriptions", () => Guard(Apply)); apply.Enabled = false;
            AddButton(Actions, "Check all", () => { foreach (DataGridViewRow row in Results.Rows) row.Cells[0].Value = true; });
            AddButton(Actions, "Check none", () => { foreach (DataGridViewRow row in Results.Rows) row.Cells[0].Value = false; });
            AddButton(Actions, "View available data", () => { if (Results.CurrentRow?.Tag is ReviewClash row) MessageBox.Show(this, ClashDescriptionGenerator.Context(row), "Metadata used for drafting", MessageBoxButtons.OK, MessageBoxIcon.Information); });
            AddButton(Actions, "Show clash", () => Guard(() => { Session.Validate(snapshot); if (Results.CurrentRow?.Tag is ReviewClash row) Session.ShowClash(row); }));
            AddButton(Actions, "Export drafts CSV", () => Guard(() => { Session.Validate(snapshot); ExportGrid(); }));
            CheckColumn(); Column("Test", 170); Column("Clash", 150); Column("Original description", 240); Column("Proposed description — editable", 480, true);
            Column("Missing data / draft state", 240);
            Results.CellValueChanged += (s, e) => { if (!writingDraft && e.RowIndex >= 0 && e.ColumnIndex == 4) { var row = Results.Rows[e.RowIndex]; edited.Add(row); row.Cells[5].Value = "Manually edited — protected"; } };
            style.SelectedIndexChanged += (s, e) => Status.Text = "Style changed. Existing drafts retained; manually edited drafts are protected from generation.";
            FormClosing += (s, e) => cancellation?.Cancel();
            FinishLayout(generate, apply);
        }
        private void ClearDrafts()
        { ready = false; if (apply != null) apply.Enabled = false; foreach (DataGridViewRow row in Results.Rows) row.Cells[4].Value = ""; }
        protected override void InvalidateWork(string message)
        { cancellation?.Cancel(); snapshot = null; ready = false; if (apply != null) apply.Enabled = false; Results.Rows.Clear(); base.InvalidateWork(message); }
        private void LoadClashes()
        {
            snapshot = Session.ReadSnapshot(SelectedTestIds); Results.Rows.Clear(); ready = false; apply.Enabled = false;
            edited.Clear(); writingDraft = true;
            try { foreach (var row in snapshot.Clashes) { int index = Results.Rows.Add(true, row.TestName, row.Name, row.Description, ""); Results.Rows[index].Tag = row; } }
            finally { writingDraft = false; }
            Status.Text = snapshot.Clashes.Count + " clashes loaded. Check rows and generate drafts. Nothing has been sent or changed.";
        }
        private async void Generate(bool projectTemplate = false)
        {
            if (Busy) return;
            try
            {
                Results.EndEdit(); Session.Validate(snapshot);
                var rows = Results.Rows.Cast<DataGridViewRow>().Where(row => Convert.ToBoolean(row.Cells[0].Value ?? false) && !edited.Contains(row)).ToList();
                if (rows.Count == 0 || rows.Count > 200) throw new InvalidOperationException("Check 1–200 clashes per generation batch.");
                var options = new DescriptionAiOptions { Provider = DescriptionProvider.Template, Style = style.Text };
                ClashDescriptionGenerator.ValidateOptions(options);
                var captured = snapshot; ready = false; apply.Enabled = false; SetBusy(true); Results.Enabled = false;
                foreach (Control control in Actions.Controls) control.Enabled = false; cancel.Enabled = true;
                cancellation = new CancellationTokenSource();
                int completed = 0;
                foreach (var row in rows)
                {
                    Status.Text = "Drafting " + (completed + 1) + " of " + rows.Count + ". You may cancel generation; no model changes occur.";
                    await System.Threading.Tasks.Task.Delay(1, cancellation.Token);
                    string missing = "";
                    string draft = projectTemplate ? ProjectDescriptionTemplate.Render((ReviewClash)row.Tag, template.Text, out missing) : await ClashDescriptionGenerator.GenerateAsync((ReviewClash)row.Tag, options, cancellation.Token);
                    if (IsDisposed) return; cancellation.Token.ThrowIfCancellationRequested(); Session.CheckHost();
                    if (captured.Revision != Session.Revision) throw new InvalidOperationException("Model data changed during generation. Reload clashes and generate again.");
                    writingDraft = true;
                    try { row.Cells[4].Value = draft; row.Cells[5].Value = missing.Length == 0 ? "Generated — review required" : "Missing/ambiguous: " + missing; row.Cells[4].Style.BackColor = missing.Length == 0 ? System.Drawing.Color.White : System.Drawing.Color.MistyRose; }
                    finally { writingDraft = false; } completed++;
                }
                Session.Validate(captured); ready = true; Status.Text = completed + " drafts ready. Review the proposed text, edit it and check only descriptions to apply.";
            }
            catch (OperationCanceledException) { if (!IsDisposed) { ready = HasDrafts(); Status.Text = "Generation cancelled. Existing drafts retained; no descriptions applied."; } }
            catch (Exception ex) { if (!IsDisposed) { ready = HasDrafts(); Status.Text = "Generation stopped; existing drafts retained. " + ex.Message; } }
            finally
            {
                cancellation?.Dispose(); cancellation = null;
                if (!IsDisposed) { SetBusy(false); Results.Enabled = true; foreach (Control control in Actions.Controls) control.Enabled = true; cancel.Enabled = false; apply.Enabled = ready; }
            }
        }
        private bool HasDrafts() => Results.Rows.Cast<DataGridViewRow>().Any(r => !string.IsNullOrWhiteSpace(Convert.ToString(r.Cells[4].Value)));
        private void Apply()
        {
            Results.EndEdit(); if (!ready) throw new InvalidOperationException("Generate and review drafts first."); Session.Validate(snapshot);
            var drafts = Results.Rows.Cast<DataGridViewRow>().Where(row => Convert.ToBoolean(row.Cells[0].Value ?? false))
                .Select(row => new DescriptionDraft { Source = (ReviewClash)row.Tag, Text = Convert.ToString(row.Cells[4].Value) }).ToList();
            if (drafts.Count == 0 || drafts.Any(d => string.IsNullOrWhiteSpace(d.Text) || d.Text.Contains("[MISSING "))) throw new InvalidOperationException("Resolve missing-data markers and review every checked draft before applying.");
            if (!Confirm("Apply " + drafts.Count + " reviewed descriptions? This changes the Description field only; names, statuses and assignments stay unchanged. Save the document when ready.")) return;
            int count = Session.ApplyDescriptions(snapshot, drafts);
            foreach (var draft in drafts.Where(d => d.Source.Description != d.Text)) CoordinationChangeLog.Record(Session.Document, "AI Clash Description Assistant", draft.Source.TestName + " / " + draft.Source.Name, draft.Source.Description, draft.Text);
            InvalidateWork("Updated " + count + " descriptions. Reload to review current values; save your document when ready.");
        }
    }
}
