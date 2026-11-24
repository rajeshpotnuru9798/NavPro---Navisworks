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
using Color = System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ClashApproverDialog : Form
    {
        private readonly Document document;
        private readonly ClashApproverService service;
        private readonly CheckedListBox tests = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        private readonly TextBox testFilter = new TextBox { Width = 210, AccessibleName = "Filter clash tests", AccessibleDescription = "Show clash tests whose names contain this text." };
        private readonly List<ClashApprovalTest> allTests = new List<ClashApprovalTest>();
        private readonly HashSet<string> checkedTestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly TextBox updatedBy = new TextBox { Width = 220, Text = Environment.UserName };
        private readonly Label updatedByLabel = new Label { Text = "Updated by / approver", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(4, 7, 8, 0) };
        private readonly ComboBox targetStatus = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = Color.White, BackColor = Color.FromArgb(20, 39, 58), FlatStyle = FlatStyle.Flat };
        private readonly Label summary = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.FromArgb(48, 67, 85) };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.FromArgb(0, 105, 125) };
        private readonly Button refresh = new Button { Text = "Refresh tests", AutoSize = true };
        private readonly Button selectAll = new Button { Text = "Select all", AutoSize = true };
        private readonly Button selectNone = new Button { Text = "Select none", AutoSize = true };
        private readonly Button preview = new Button { Text = "Preview Status", AutoSize = true };
        private readonly Button update = new Button { Text = "Clash Status", AutoSize = true, Enabled = false, BackColor = Color.FromArgb(0, 112, 135), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        private readonly Button close = new Button { Text = "Close", AutoSize = true };
        private bool hasPreview;
        private bool loadingTests;
        private bool previewQueued;

        public ClashApproverDialog(Document document)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            service = new ClashApproverService(document);
            Text = "NavPro | Status Updater"; Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(900, 700); MinimumSize = new Size(700, 560); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(241, 244, 248); MinimizeBox = false;
            foreach (ClashResultStatus clashStatus in Enum.GetValues(typeof(ClashResultStatus))) targetStatus.Items.Add(clashStatus);
            targetStatus.SelectedItem = ClashResultStatus.Approved;
            BuildLayout();
            refresh.Click += (s, e) => RefreshTests();
            selectAll.Click += (s, e) => SetAll(true);
            selectNone.Click += (s, e) => SetAll(false);
            preview.Click += (s, e) => PreviewStatus();
            update.Click += (s, e) => UpdateStatus();
            close.Click += (s, e) => Close();
            testFilter.TextChanged += (s, e) => ApplyTestFilter();
            tests.ItemCheck += (s, e) =>
            {
                if (loadingTests) return;
                if (tests.Items[e.Index] is ClashApprovalTest item)
                {
                    if (e.NewValue == CheckState.Checked) checkedTestIds.Add(item.Id);
                    else checkedTestIds.Remove(item.Id);
                }
                hasPreview = false; update.Enabled = false;
                QueuePreviewAfterSelection();
            };
            targetStatus.SelectedIndexChanged += (s, e) => { hasPreview = false; PreviewStatus(); };
            updatedBy.TextChanged += (s, e) => { hasPreview = false; update.Enabled = false; };
            pin.CheckedChanged += (s, e) => TopMost = pin.Checked;
            Shown += (s, e) => { if (!hasPreview) PreviewStatus(); };
            DialogTheme.Apply(this, update);
            RefreshTests();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 39, 58), Padding = new Padding(18) };
            var title = new Label { Text = "Status Updater", Font = new Font("Segoe UI Semibold", 22F), ForeColor = Color.White, AutoSize = true, Location = new Point(16, 12) };
            var subtitle = new Label { Text = "Select clash tests, choose a standard status, preview the changes, then update results in bulk.", Font = new Font("Segoe UI", 10F), ForeColor = Color.FromArgb(220, 231, 240), AutoSize = true, Location = new Point(18, 58) };
            pin.Location = new Point(18, 83);
            header.Controls.Add(title); header.Controls.Add(subtitle); header.Controls.Add(pin);
            root.Controls.Add(header, 0, 0);

            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(8), BackColor = Color.White };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            settings.Controls.Add(new Label { Text = "Clash Status", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(4, 7, 8, 0) }, 0, 0);
            settings.Controls.Add(targetStatus, 1, 0); settings.Controls.Add(updatedByLabel, 2, 0); settings.Controls.Add(updatedBy, 3, 0);
            var explanation = new Label { Text = "Available statuses come from Navisworks: New, Active, Reviewed, Approved and Resolved. The name is recorded as approval metadata only when the target status is Approved.", AutoSize = true, MaximumSize = new Size(820, 0), ForeColor = Color.FromArgb(72, 88, 103), Anchor = AnchorStyles.Left };
            settings.Controls.Add(explanation, 0, 1); settings.SetColumnSpan(explanation, 4);
            root.Controls.Add(settings, 0, 1);

            var listPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8), BackColor = Color.White };
            listPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); listPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var filterBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
            filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            testFilter.Dock = DockStyle.Fill; testFilter.Margin = new Padding(2, 3, 4, 3);
            filterBar.Controls.Add(testFilter, 0, 0); filterBar.Controls.Add(new Panel(), 1, 0);
            filterBar.Controls.Add(selectAll, 2, 0); filterBar.Controls.Add(selectNone, 3, 0); filterBar.Controls.Add(refresh, 4, 0);
            listPanel.Controls.Add(filterBar, 0, 0);
            listPanel.Controls.Add(new Label { Text = "CLASH TESTS", AutoSize = true, Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.FromArgb(30, 48, 65), Anchor = AnchorStyles.Left, Padding = new Padding(2, 4, 0, 0) }, 0, 1);
            listPanel.Controls.Add(tests, 0, 2);
            root.Controls.Add(listPanel, 0, 2);

            var previewPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12), BackColor = Color.White };
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var previewHeader = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            previewHeader.Controls.Add(preview);
            previewPanel.Controls.Add(previewHeader, 0, 0); previewPanel.Controls.Add(summary, 0, 1);
            root.Controls.Add(previewPanel, 0, 3);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(4) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(update, 1, 0); footer.Controls.Add(close, 2, 0);
            root.Controls.Add(footer, 0, 4);
            ResizableLayout.Rows(root, 2, 3, 2, 0.7, "Clash tests and status preview");
            Controls.Add(root);
        }

        private void QueuePreviewAfterSelection()
        {
            if (!IsHandleCreated || IsDisposed || previewQueued) return;
            previewQueued = true;
            BeginInvoke((Action)(() => { previewQueued = false; if (!IsDisposed) PreviewStatus(); }));
        }

        private void RefreshTests()
        {
            try
            {
                var oldSelection = new HashSet<string>(SelectedIds(), StringComparer.OrdinalIgnoreCase);
                loadingTests = true;
                tests.BeginUpdate();
                try
                {
                    allTests.Clear(); allTests.AddRange(service.ReadTests());
                    checkedTestIds.Clear();
                    foreach (var item in allTests)
                        if (oldSelection.Count == 0 || oldSelection.Contains(item.Id)) checkedTestIds.Add(item.Id);
                    tests.Items.Clear();
                    foreach (var item in FilteredTests()) tests.Items.Add(item, checkedTestIds.Contains(item.Id));
                }
                finally { tests.EndUpdate(); loadingTests = false; }
                hasPreview = false; update.Enabled = false;
                status.Text = allTests.Count == 0 ? "No Clash Detective tests found in this document." : "Loaded " + allTests.Count.ToString("N0") + " clash tests.";
                PreviewStatus();
            }
            catch (Exception ex) { status.Text = ex.Message; summary.Text = "Refresh failed. Confirm the active document and try again."; }
        }

        private void SetAll(bool value)
        {
            if (value) foreach (var item in allTests) checkedTestIds.Add(item.Id);
            else checkedTestIds.Clear();
            ApplyTestFilter();
            hasPreview = false; update.Enabled = false; PreviewStatus();
        }

        private IEnumerable<ClashApprovalTest> FilteredTests()
        {
            var query = testFilter.Text.Trim();
            return query.Length == 0 ? allTests : allTests.Where(item => item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void ApplyTestFilter()
        {
            if (loadingTests) return;
            loadingTests = true; tests.BeginUpdate();
            try
            {
                tests.Items.Clear();
                foreach (var item in FilteredTests()) tests.Items.Add(item, checkedTestIds.Contains(item.Id));
            }
            finally { tests.EndUpdate(); loadingTests = false; }
        }

        private List<string> SelectedIds() => allTests.Where(item => checkedTestIds.Contains(item.Id)).Select(item => item.Id).ToList();

        private ClashResultStatus SelectedStatus() => targetStatus.SelectedItem is ClashResultStatus ? (ClashResultStatus)targetStatus.SelectedItem : ClashResultStatus.Approved;

        private void PreviewStatus()
        {
            try
            {
                var target = SelectedStatus();
                bool approvalMetadataRequired = target == ClashResultStatus.Approved;
                updatedBy.Enabled = approvalMetadataRequired; updatedByLabel.Enabled = approvalMetadataRequired;
                var p = service.Preview(SelectedIds(), target);
                var statusCounts = string.Join("    ", Enum.GetValues(typeof(ClashResultStatus)).Cast<ClashResultStatus>().Select(value => value + ": " + (p.CurrentStatuses.TryGetValue(value.ToString(), out int count) ? count : 0).ToString("N0")));
                summary.Text = "Selected tests: " + p.SelectedTests.ToString("N0") + "    Target status: " + p.TargetStatus + "\r\n" +
                    "Current clash statuses: " + statusCounts + "\r\n" +
                    "Ready to change: " + p.ToChange.ToString("N0") + "    Already at target: " + p.AlreadyAtStatus.ToString("N0") + "    Other statuses: " + p.OtherStatuses.ToString("N0");
                hasPreview = true;
                update.Enabled = p.ToChange > 0 && (!approvalMetadataRequired || !string.IsNullOrWhiteSpace(updatedBy.Text));
                status.Text = p.ToChange == 0 ? "No clash results need this status change." : "Preview ready. Review the counts before updating status.";
            }
            catch (Exception ex) { hasPreview = false; update.Enabled = false; status.Text = ex.Message; }
        }

        private void UpdateStatus()
        {
            if (!hasPreview) { PreviewStatus(); return; }
            var target = SelectedStatus();
            var previewNow = service.Preview(SelectedIds(), target);
            if (previewNow.ToChange == 0) { PreviewStatus(); return; }
            string approvalNote = target == ClashResultStatus.Approved ? "\r\nApproved by: " + updatedBy.Text.Trim() : "";
            var answer = MessageBox.Show(this,
                "Change " + previewNow.ToChange.ToString("N0") + " clash results in " + previewNow.SelectedTests.ToString("N0") +
                " selected tests to " + target + "?" + approvalNote + "\r\n\r\nNavisworks will not rerun the tests.",
                "Confirm bulk status change", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            try
            {
                update.Enabled = false; refresh.Enabled = false; preview.Enabled = false;
                var result = service.UpdateStatus(SelectedIds(), target, updatedBy.Text);
                hasPreview = false; RefreshTests();
                status.Text = "Changed " + result.Updated.ToString("N0") + " clashes across " + result.TestsChanged.ToString("N0") + " tests; skipped " + result.Skipped.ToString("N0") + ".";
            }
            catch (Exception ex) { status.Text = "Status update stopped: " + ex.Message; hasPreview = false; }
            finally { refresh.Enabled = true; preview.Enabled = true; }
        }
    }
}
