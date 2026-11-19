using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Document = Autodesk.Navisworks.Api.Document;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ClashDashboardDialog : Form
    {
        private readonly Document document;
        private readonly ClashDashboardExportService service;
        private readonly Button export = new Button { Text = "Export Clash Dashboard", AutoSize = true };
        private readonly Button close = new Button { Text = "Close", AutoSize = true };
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(35, 75, 96) };
        private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Height = 9, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25 };

        public ClashDashboardDialog(Document document)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            service = new ClashDashboardExportService(document);
            Text = "NavPro | Clash Dashboard"; Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(760, 250); MinimumSize = new Size(620, 230); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(242, 245, 248); FormBorderStyle = FormBorderStyle.Sizable; ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(35, 30, 220), Padding = new Padding(16) };
            header.Controls.Add(new Label { Text = "NavPro", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 19F), Location = new Point(14, 9) });
            header.Controls.Add(new Label { Text = "Clash Dashboard · Export", AutoSize = true, ForeColor = Color.White, Location = new Point(17, 47) });
            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(new Label { Text = "Export the saved Clash Detective tests and clash results as an interactive HTML dashboard. Choose report scope, units, statuses and optional images in the next step.", AutoSize = true, MaximumSize = new Size(680, 0), Dock = DockStyle.Top, Padding = new Padding(4, 12, 4, 12), ForeColor = Color.FromArgb(48, 67, 84) }, 0, 1);
            layout.Controls.Add(status, 0, 2); layout.Controls.Add(progress, 0, 3);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
            export.FlatStyle = close.FlatStyle = FlatStyle.Flat; export.Padding = close.Padding = new Padding(12, 8, 12, 8);
            export.BackColor = Color.FromArgb(35, 30, 220); export.ForeColor = Color.White;
            actions.Controls.Add(close); actions.Controls.Add(export); layout.Controls.Add(actions, 0, 4);
            Controls.Add(layout);
            DialogTheme.Apply(this, export);
            export.Click += (s, e) => ExportDashboard(); close.Click += (s, e) => Close();
            progress.Visible = false;
        }

        private void ExportDashboard()
        {
            using (var settings = new ClashDashboardSettingsDialog(document))
            {
                if (settings.ShowDialog(this) != DialogResult.OK || settings.Options == null) return;
                using (var save = new SaveFileDialog
                {
                    Title = "Save NavPro Clash Dashboard", Filter = "Interactive HTML Dashboard (*.html)|*.html",
                    DefaultExt = "html", AddExtension = true, OverwritePrompt = true,
                    FileName = "NavPro-ClashDashboard-" + DateTime.Now.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture) + ".html"
                })
                {
                    if (save.ShowDialog(this) != DialogResult.OK) return;
                    var companions = new List<string>();
                    if (settings.Options.IncludeTabularHtml) companions.Add(System.IO.Path.ChangeExtension(save.FileName, null) + "_tabular.html");
                    if (settings.Options.IncludeExcel) companions.Add(System.IO.Path.ChangeExtension(save.FileName, ".xlsx"));
                    if (settings.Options.IncludeXml) companions.Add(System.IO.Path.ChangeExtension(save.FileName, ".xml"));
                    var existing = companions.Where(System.IO.File.Exists).ToList();
                    if (existing.Count > 0 && MessageBox.Show(this, "These companion files already exist and will be replaced:\r\n" + string.Join("\r\n", existing) + "\r\n\r\nContinue?", "Overwrite companion reports", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    try
                    {
                        export.Enabled = false; progress.Visible = true;
                        status.Text = "Collecting saved Clash Detective tests and results. Optional viewpoint images render synchronously and may take time.";
                        status.Refresh(); progress.Refresh();
                        int count = service.Export(save.FileName, settings.Options);
                        var outputs = new List<string> { "HTML dashboard: " + save.FileName };
                        if (settings.Options.IncludeExcel) outputs.Add("Excel workbook: " + System.IO.Path.ChangeExtension(save.FileName, ".xlsx"));
                        if (settings.Options.IncludeTabularHtml) outputs.Add("Tabular HTML: " + System.IO.Path.ChangeExtension(save.FileName, null) + "_tabular.html");
                        if (settings.Options.IncludeXml) outputs.Add("XML report: " + System.IO.Path.ChangeExtension(save.FileName, ".xml"));
                        status.Text = "Clash Dashboard exported with " + count.ToString("N0") + " clash results and all saved test definitions. " + string.Join(" · ", outputs);
                        var summary = count == 0 ? "The dashboard was created, but no individual clash results matched the selected statuses." : "Exported an interactive dashboard with " + count.ToString("N0") + " results. Use Add Files to combine NavPro dashboards; Print PDF opens the browser print dialog.";
                        MessageBox.Show(this, summary + "\r\n\r\n" + string.Join("\r\n", outputs), "Clash Dashboard complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(save.FileName) { UseShellExecute = true }); } catch { }
                    }
                    catch (Exception ex)
                    {
                        status.Text = "Clash Dashboard export failed: " + ex.Message;
                        MessageBox.Show(this, ex.Message, "Clash Dashboard export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally { export.Enabled = true; progress.Visible = false; }
                }
            }
        }
    }
}
