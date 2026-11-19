using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
using Color = System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    internal sealed class ClashDashboardSettingsDialog : Form
    {
        private readonly TextBox project = new TextBox { Width = 360 };
        private readonly ComboBox units = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private readonly CheckedListBox statuses = new CheckedListBox { Width = 390, Height = 112, CheckOnClick = true };
        private readonly RadioButton all = new RadioButton { Text = "All kept results", AutoSize = true, Checked = true };
        private readonly RadioButton filtered = new RadioButton { Text = "Only results in selected statuses", AutoSize = true };
        private readonly CheckBox images = new CheckBox { Text = "Include saved viewpoint images in the HTML (slow; one render per clash)", AutoSize = true, Checked = true };
        private readonly CheckBox tabular = new CheckBox { Text = "Also create HTML (tabular) companion", AutoSize = true };
        private readonly CheckBox excel = new CheckBox { Text = "Also create Excel workbook (.xlsx)", AutoSize = true };
        private readonly CheckBox xml = new CheckBox { Text = "Also create XML companion", AutoSize = true };
        private readonly Units sourceUnits;

        public ClashDashboardOptions Options { get; private set; }

        public ClashDashboardSettingsDialog(Document document)
        {
            sourceUnits = document.Units;
            Text = "Clash Dashboard setup"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Font = new Font("Segoe UI", 9.5F); BackColor = Color.FromArgb(241, 244, 248); Padding = new Padding(14);
            project.Text = string.IsNullOrWhiteSpace(document.FileName) ? "Navisworks Clash Report" : System.IO.Path.GetFileNameWithoutExtension(document.FileName);
            units.Items.AddRange(Enum.GetValues(typeof(Units)).Cast<object>().ToArray()); units.SelectedItem = document.Units;
            foreach (var status in new[] { "New", "Active", "Reviewed", "Approved", "Resolved" }) statuses.Items.Add(status, true);

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 8, Dock = DockStyle.Fill, Padding = new Padding(6) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); Controls.Add(layout);
            AddRow(layout, 0, "Project", project); AddRow(layout, 1, "Formats", MakeStack(new Label { Text = "Dashboard (HTML) · Interactive", AutoSize = true, ForeColor = Color.FromArgb(0, 111, 139) }, tabular, excel, xml));
            AddRow(layout, 2, "Units", units); AddRow(layout, 3, "Statuses", statuses); AddRow(layout, 4, "Images", images);
            AddRow(layout, 5, "Results", MakeStack(all, filtered));
            var note = new Label { AutoSize = true, MaximumSize = new Size(590, 0), ForeColor = Color.FromArgb(90, 102, 114), Text = "The dashboard summarizes saved results from the current Clash Detective tests. The Excel workbook includes summary, test and clash-result sheets with coordinates. HTML/XML files are optional companions. Image rendering is synchronous and can take time." };
            layout.Controls.Add(note, 0, 6); layout.SetColumnSpan(note, 2);
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            var ok = new Button { Text = "Continue", DialogResult = DialogResult.OK, AutoSize = true, BackColor = Color.FromArgb(0, 111, 139), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 7); layout.SetColumnSpan(buttons, 2);
            AcceptButton = ok; CancelButton = cancel;
            FormClosing += (s, e) => { if (DialogResult == DialogResult.OK) BuildOptions(); };
        }

        private static Control MakeStack(params Control[] controls)
        {
            var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            foreach (var c in controls) panel.Controls.Add(c); return panel;
        }
        private static void AddRow(TableLayoutPanel panel, int row, string label, Control control)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.FromArgb(20, 39, 58), Padding = new Padding(0, 6, 14, 0) }, 0, row); panel.Controls.Add(control, 1, row);
        }
        private void BuildOptions()
        {
            Units target = units.SelectedItem is Units ? (Units)units.SelectedItem : sourceUnits;
            Options = new ClashDashboardOptions { ProjectName = project.Text, DisplayUnits = target.ToString(), UnitScale = UnitConversion.ScaleFactor(sourceUnits, target), IncludeImages = images.Checked, IncludeAllResults = all.Checked, IncludeTabularHtml = tabular.Checked, IncludeExcel = excel.Checked, IncludeXml = xml.Checked };
            foreach (var item in statuses.CheckedItems) Options.IncludedStatuses.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
        }
    }
}
