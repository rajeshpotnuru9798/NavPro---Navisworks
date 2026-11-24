using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;
using Color = System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ClashStatusImportDialog : Form
    {
        private const int MaximumPreviewRows = 500;
        private readonly Document document;
        private readonly ClashStatusImportService service;
        private readonly TextBox filePath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        private readonly Button browse = new Button { Text = "Choose XML/CSV…", AutoSize = true };
        private readonly Button load = new Button { Text = "Load report", AutoSize = true };
        private readonly CheckedListBox statuses = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        private readonly Button selectAll = new Button { Text = "Select all", AutoSize = true };
        private readonly Button selectNone = new Button { Text = "Select none", AutoSize = true };
        private readonly Button previewButton = new Button { Text = "Preview changes", AutoSize = true, Enabled = false };
        private readonly Button importButton = new Button { Text = "Import", AutoSize = true, Enabled = false, BackColor = Color.FromArgb(165, 0, 164), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        private readonly Button close = new Button { Text = "Close", AutoSize = true };
        private readonly CheckBox pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = Color.White, BackColor = DialogTheme.Brand, FlatStyle = FlatStyle.Flat };
        private readonly TextBox updatedBy = new TextBox { Width = 180, Text = Environment.UserName };
        private readonly Label summary = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.FromArgb(48, 67, 85) };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.FromArgb(0, 105, 125) };
        private readonly DataGridView previewGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, MultiSelect = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        private ClashStatusImportReport report;
        private ClashStatusImportPreview lastPreview;
        private bool loadingStatuses;

        public ClashStatusImportDialog(Document document)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            service = new ClashStatusImportService(document);
            Text = "NavPro | Import Clash Status"; Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1040, 720); MinimumSize = new Size(820, 580); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(241, 244, 248); MinimizeBox = false;
            BuildGrid(); BuildLayout();
            browse.Click += (s, e) => BrowseFile();
            load.Click += (s, e) => LoadFile(filePath.Text);
            selectAll.Click += (s, e) => SetAll(true);
            selectNone.Click += (s, e) => SetAll(false);
            previewButton.Click += (s, e) => PreviewChanges();
            importButton.Click += (s, e) => ApplyChanges();
            close.Click += (s, e) => Close();
            pin.CheckedChanged += (s, e) => TopMost = pin.Checked;
            statuses.ItemCheck += (s, e) => { if (!loadingStatuses) InvalidatePreview(); };
            filePath.TextChanged += (s, e) => InvalidatePreview();
            DialogTheme.Apply(this, importButton);
            status.Text = "Choose an XML or CSV exported from the NavPro Clash Dashboard.";
        }

        private void BuildGrid()
        {
            previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Test", DataPropertyName = "TestName", FillWeight = 18 });
            previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Clash", DataPropertyName = "ClashName", FillWeight = 20 });
            previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Report status", DataPropertyName = "ImportedStatus", FillWeight = 12 });
            previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Current status", DataPropertyName = "CurrentStatus", FillWeight = 12 });
            previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Preview result", DataPropertyName = "Disposition", FillWeight = 15 });
            previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Details", DataPropertyName = "Details", FillWeight = 23 });
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10), BackColor = Color.FromArgb(241, 244, 248) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 125)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = DialogTheme.Brand };
            var title = new Label { Text = "Import Clash Status", Font = new Font("Segoe UI Semibold", 18F), ForeColor = Color.White, AutoSize = true, Location = new Point(16, 12) };
            var subtitle = new Label { Text = "Match dashboard XML/CSV rows to this document, review status changes, then import selected statuses.", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(220, 231, 240), AutoSize = true, Location = new Point(18, 50) };
            pin.Anchor = AnchorStyles.Top | AnchorStyles.Right; pin.Location = new Point(Width - 130, 18);
            header.Controls.Add(title); header.Controls.Add(subtitle); header.Controls.Add(pin); root.Controls.Add(header, 0, 0);

            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8), BackColor = Color.White };
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            var fileRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fileRow.Controls.Add(filePath, 0, 0); fileRow.Controls.Add(browse, 1, 0); fileRow.Controls.Add(load, 2, 0); settings.Controls.Add(fileRow, 0, 0);

            var statusArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            statusArea.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); statusArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusArea.Controls.Add(new Label { Text = "Statuses to import", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(2, 2, 10, 0) }, 0, 0);
            var statusList = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            statusList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); statusList.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            statusList.Controls.Add(statuses, 0, 0); statusList.SetRowSpan(statuses, 2); statusList.Controls.Add(selectAll, 1, 0); statusList.Controls.Add(selectNone, 1, 1);
            statusArea.Controls.Add(statusList, 1, 0); settings.Controls.Add(statusArea, 0, 1);
            settings.Controls.Add(new Label { Text = "Approved rows record the name below. Other imported fields, such as assignments, are left unchanged.", AutoSize = true, ForeColor = Color.FromArgb(72, 88, 103), Anchor = AnchorStyles.Left }, 0, 2);
            root.Controls.Add(settings, 0, 1);

            var results = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8), BackColor = Color.White };
            results.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); results.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var resultsHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            resultsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); resultsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); resultsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); resultsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resultsHeader.Controls.Add(summary, 0, 0); resultsHeader.Controls.Add(new Label { Text = "Updated by", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(6, 5, 3, 0) }, 1, 0); resultsHeader.Controls.Add(updatedBy, 2, 0); resultsHeader.Controls.Add(previewButton, 3, 0);
            results.Controls.Add(resultsHeader, 0, 0); results.Controls.Add(previewGrid, 0, 1); root.Controls.Add(results, 0, 2);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(4) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(importButton, 1, 0); footer.Controls.Add(close, 2, 0); root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
            ResizableLayout.Rows(root, 1, 2, 1, 0.3, "Import settings and results");
        }

        private void BrowseFile()
        {
            using (var picker = new OpenFileDialog { Title = "Select NavPro Dashboard report", Filter = "NavPro Dashboard reports (*.xml;*.csv)|*.xml;*.csv|XML reports (*.xml)|*.xml|CSV reports (*.csv)|*.csv", CheckFileExists = true, Multiselect = false })
                if (picker.ShowDialog(this) == DialogResult.OK) { filePath.Text = picker.FileName; LoadFile(picker.FileName); }
        }

        private void LoadFile(string path)
        {
            report = null; lastPreview = null; previewGrid.Rows.Clear(); summary.Text = "";
            loadingStatuses = true; statuses.Items.Clear(); loadingStatuses = false;
            previewButton.Enabled = false; importButton.Enabled = false;
            try
            {
                report = service.ReadReport(path); lastPreview = null; previewGrid.Rows.Clear();
                loadingStatuses = true; statuses.Items.Clear();
                try { foreach (string item in report.AvailableStatuses) statuses.Items.Add(item, true); }
                finally { loadingStatuses = false; }
                previewButton.Enabled = report.AvailableStatuses.Count > 0;
                importButton.Enabled = false;
                status.Text = report.AvailableStatuses.Count == 0 ? "No standard Navisworks statuses were found in this report." : "Loaded " + report.Records.Count.ToString("N0") + " rows from " + report.FileType + "; choose which statuses to import.";
                summary.Text = report.AvailableStatuses.Count == 0 ? "" : "Statuses in report: " + string.Join(", ", report.AvailableStatuses);
            }
            catch (Exception ex) { report = null; lastPreview = null; previewGrid.Rows.Clear(); previewButton.Enabled = false; importButton.Enabled = false; summary.Text = ""; status.Text = "Could not load report: " + ex.Message; }
        }

        private void SetAll(bool value)
        {
            loadingStatuses = true; try { for (int i = 0; i < statuses.Items.Count; i++) statuses.SetItemChecked(i, value); } finally { loadingStatuses = false; }
            InvalidatePreview();
        }

        private void InvalidatePreview()
        {
            lastPreview = null; importButton.Enabled = false; previewGrid.Rows.Clear();
            summary.Text = report == null ? "" : "Statuses in report: " + string.Join(", ", report.AvailableStatuses);
            if (report != null) previewButton.Enabled = statuses.CheckedItems.Count > 0;
        }

        private void PreviewChanges()
        {
            previewButton.Enabled = false; importButton.Enabled = false; Cursor = Cursors.WaitCursor;
            status.Text = "Matching report rows to native results. This synchronous operation may pause the window.";
            status.Refresh();
            try
            {
                if (report == null) throw new InvalidOperationException("Load a dashboard XML or CSV report first.");
                lastPreview = service.Preview(report, statuses.CheckedItems.Cast<string>());
                previewGrid.Rows.Clear();
                foreach (var row in lastPreview.Rows.Take(MaximumPreviewRows))
                    previewGrid.Rows.Add(row.Source.TestName, row.Source.ClashName, row.Source.Status, row.CurrentStatus, row.Disposition.ToString(), row.Details);
                summary.Text = "Ready " + lastPreview.ReadyCount.ToString("N0") + " · Already current " + lastPreview.AlreadyCurrentCount.ToString("N0") + " · Not found " + lastPreview.NotFoundCount.ToString("N0") + " · Ambiguous " + lastPreview.AmbiguousCount.ToString("N0") + " · Conflicts " + lastPreview.ConflictingRowsCount.ToString("N0") + " · Incomplete " + lastPreview.IncompleteIdentityCount.ToString("N0") + " · Invalid " + lastPreview.InvalidStatusCount.ToString("N0") + " · Duplicates " + lastPreview.DuplicateCount.ToString("N0") + " · Filtered " + lastPreview.FilteredOutCount.ToString("N0");
                if (lastPreview.Rows.Count > MaximumPreviewRows) summary.Text += " · Preview lists first " + MaximumPreviewRows.ToString("N0") + " rows";
                importButton.Enabled = lastPreview.ReadyCount > 0;
                status.Text = lastPreview.ReadyCount == 0 ? "No unique, changed status matches are ready to import." : "Review the matching results before importing.";
            }
            catch (Exception ex) { lastPreview = null; importButton.Enabled = false; status.Text = "Preview failed: " + ex.Message; }
            finally { Cursor = Cursors.Default; previewButton.Enabled = report != null && statuses.CheckedItems.Count > 0; }
        }

        private void ApplyChanges()
        {
            if (lastPreview == null || lastPreview.ReadyCount == 0) return;
            string prompt = "Update " + lastPreview.ReadyCount.ToString("N0") + " clash statuses across " + lastPreview.Actions.Select(a => a.TestId).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString("N0") + " tests?\r\n\r\nOnly uniquely matched rows with selected statuses will change. Unmatched, ambiguous, invalid and filtered rows will be left unchanged.";
            if (MessageBox.Show(this, prompt, "Confirm Import Clash Status", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                importButton.Enabled = false; previewButton.Enabled = false; Cursor = Cursors.WaitCursor;
                var result = service.Apply(lastPreview, updatedBy.Text);
                status.Text = "Imported " + result.Updated.ToString("N0") + " status changes across " + result.TestsChanged.ToString("N0") + " tests. " + result.Skipped.ToString("N0") + " rows skipped.";
                lastPreview = null; previewGrid.Rows.Clear(); summary.Text = status.Text; previewButton.Enabled = report != null && statuses.CheckedItems.Count > 0;
                MessageBox.Show(this, status.Text + "\r\nSave the model in Navisworks to keep these native changes.", "Import Clash Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { status.Text = "Import failed: " + ex.Message; MessageBox.Show(this, status.Text, "Import Clash Status", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { Cursor = Cursors.Default; importButton.Enabled = false; if (report != null) previewButton.Enabled = statuses.CheckedItems.Count > 0; }
        }
    }
}
