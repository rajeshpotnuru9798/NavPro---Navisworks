using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Controls;
using NavPro.Frontend.Ui;
using Color = System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    public sealed class SearchSetCreaterDialog : Form
    {
        private readonly ModelInspectionService inspector;
        private readonly SearchSetCreationService creator;
        private readonly ComboBox category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox property = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox condition = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox value = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly Label chosen = new Label { AutoSize = true, Dock = DockStyle.Fill };
        private readonly Label summary = new Label { AutoSize = true, Dock = DockStyle.Fill };
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill };
        private readonly Label progressStage = new Label { AutoSize = true, Text = "READY", Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.White };
        private readonly Label progressEta = new Label { AutoSize = true, Text = "No active operation", ForeColor = Color.FromArgb(202, 215, 229) };
        private readonly RoundedProgressIndicator progress = new RoundedProgressIndicator { Anchor = AnchorStyles.Right | AnchorStyles.Top };
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, ScrollBars = ScrollBars.Both, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None };
        private readonly Button discover = new Button { Text = "1  Scan properties", AutoSize = true };
        private readonly Button preview = new Button { Text = "2  Preview search sets", AutoSize = true, Enabled = false };
        private readonly Button create = new Button { Text = "3  Create search sets", AutoSize = true, Enabled = false };
        private readonly Button oneClickPreview = new Button { Text = "Preview File Name sets", AutoSize = true };
        private readonly Button oneClickCreate = new Button { Text = "Create previewed sets", AutoSize = true, Enabled = false };
        private readonly CheckedListBox oneClickModels = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, Height = 92 };
        private readonly CheckBox pinWindow = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = Color.White, BackColor = Color.FromArgb(25, 42, 59), FlatStyle = FlatStyle.Flat };
        private readonly Button cancel = new Button { Text = "Close", AutoSize = true };
        private readonly Label oneClickProperty = new Label { AutoSize = true, ForeColor = Color.FromArgb(25, 42, 59), Font = new Font("Segoe UI Semibold", 10F) };
        private readonly Label oneClickSummary = new Label { AutoSize = true, ForeColor = Color.FromArgb(75, 91, 105) };
        private readonly DataGridView oneClickGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, ScrollBars = ScrollBars.Both, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None };
        private InspectionResult result;
        private InspectionResult oneClickResult;
        private List<NameDecision> oneClickPlan;
        private PropertyOption oneClickSelectedProperty;
        private List<PropertyOption> discoveredProperties = new List<PropertyOption>();
        private List<NameDecision> plan;
        private readonly Dictionary<string, string> renamedSets = new Dictionary<string, string>(StringComparer.Ordinal);
        private CancellationTokenSource cancellation;
        private bool busy;
        private bool quickOperation;
        private bool updatingSelectors;
        private readonly ProgressReporter reporter = new ProgressReporter();

        private sealed class ValueChoice
        {
            public ValueGroup Group { get; private set; }
            public ValueChoice(ValueGroup group) { Group = group; }
            public override string ToString() => Group == null
                ? "All values (one set per distinct value)"
                : Group.Display + "  ·  " + Group.NativeValue.DataType;
        }

        private sealed class ModelChoice
        {
            public Model Model { get; private set; }
            public string Label { get; private set; }
            public ModelChoice(Model model, string label) { Model = model; Label = label; }
            public override string ToString() => Label;
        }

        public SearchSetCreaterDialog(Document document)
        {
            inspector = new ModelInspectionService(document); creator = new SearchSetCreationService(document, inspector);
            Text = "NavPro | SetCreator"; Font = new Font("Segoe UI", 10F); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1180, 820); MinimumSize = new Size(940, 700); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(241, 244, 248); MinimizeBox = false; MaximizeBox = true;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 5 };
            Controls.Add(layout);
            for (int i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(i == 2 ? SizeType.Percent : SizeType.AutoSize, i == 2 ? 100 : 0));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, BackColor = Color.FromArgb(25, 42, 59), Padding = new Padding(16), Margin = new Padding(0, 0, 0, 8) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.FromArgb(25, 42, 59), Padding = new Padding(0, 8, 0, 0) };
            brand.RowStyles.Add(new RowStyle(SizeType.AutoSize)); brand.RowStyles.Add(new RowStyle(SizeType.AutoSize)); brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            brand.Controls.Add(new Label { Text = "NavPro", Font = new Font("Segoe UI Semibold", 22F), ForeColor = Color.White, AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
            brand.Controls.Add(new Label { Text = "SetCreator  ·  Model property workflow", Font = new Font("Segoe UI", 10F), ForeColor = Color.FromArgb(198, 214, 226), AutoSize = true, Dock = DockStyle.Fill }, 0, 1);
            brand.Controls.Add(pinWindow, 0, 2);
            pinWindow.Margin = new Padding(0, 6, 0, 0);
            var pinTip = new ToolTip();
            pinTip.SetToolTip(pinWindow, "Keep this modeless NavPro dialog above Navisworks while you work in the host window.");
            var progressCard = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.FromArgb(16, 37, 59), Padding = new Padding(12), Margin = new Padding(8, 0, 0, 0) };
            progressCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76)); progressCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            progressCard.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); progressCard.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            progressCard.Controls.Add(progressStage, 0, 0);
            progressCard.Controls.Add(progressEta, 0, 1);
            progressCard.Controls.Add(progress, 1, 0); progressCard.SetRowSpan(progress, 2);
            header.Controls.Add(brand, 0, 0); header.Controls.Add(progressCard, 1, 0); layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 12), ForeColor = Color.FromArgb(52, 70, 86), Text = "Choose the setup that fits this project: configure a specific property and value in Option A, or create sets from Item / File Name values in Option B." }, 0, 1);
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, AutoSize = true, Padding = new Padding(0, 2, 0, 6), BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) };
            fields.Padding = new Padding(14, 12, 14, 10);
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int row = 0; row < 2; row++) fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var fieldLabelColor = Color.FromArgb(25, 42, 59);
            fields.Controls.Add(new Label { Text = "CATEGORY", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = fieldLabelColor, Font = new Font("Segoe UI Semibold", 8.5F) }, 0, 0); fields.Controls.Add(category, 1, 0);
            fields.Controls.Add(new Label { Text = "PROPERTY", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = fieldLabelColor, Font = new Font("Segoe UI Semibold", 8.5F) }, 2, 0); fields.Controls.Add(property, 3, 0);
            fields.Controls.Add(new Label { Text = "CONDITION", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = fieldLabelColor, Font = new Font("Segoe UI Semibold", 8.5F) }, 0, 1); fields.Controls.Add(condition, 1, 1);
            fields.Controls.Add(new Label { Text = "VALUE", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = fieldLabelColor, Font = new Font("Segoe UI Semibold", 8.5F) }, 2, 1); fields.Controls.Add(value, 3, 1);
            var criteriaHint = new Label { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(75, 91, 105), Padding = new Padding(0, 8, 0, 0), Text = "= matches the exact stored value and type. All values keeps batch creation; choose one value to create only that set." };
            fields.Controls.Add(criteriaHint, 0, 2); fields.SetColumnSpan(criteriaHint, 4);

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 10F) };
            var optionA = new TabPage("Option A — Property setup") { BackColor = Color.FromArgb(241, 244, 248), Padding = new Padding(12) };
            var optionB = new TabPage("Option B — Model file names") { BackColor = Color.FromArgb(241, 244, 248), Padding = new Padding(18) };
            tabs.TabPages.Add(optionA); tabs.TabPages.Add(optionB);
            var optionALayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
            for (int i = 0; i < 7; i++) optionALayout.RowStyles.Add(new RowStyle(i == 3 ? SizeType.Percent : SizeType.AutoSize, i == 3 ? 100 : 0));
            optionALayout.Controls.Add(fields, 0, 0);
            optionALayout.Controls.Add(chosen, 0, 1); optionALayout.Controls.Add(summary, 0, 2); optionALayout.Controls.Add(grid, 0, 3);
            optionALayout.Controls.Add(status, 0, 4);
            optionALayout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(75, 91, 105), Padding = new Padding(0, 6, 0, 8), Text = "Counts include directly matching hierarchy items. Missing/blank values are excluded. Existing saved names are skipped. Save the NWF/NWD in Navisworks to retain new sets." }, 0, 5);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
            actions.Controls.AddRange(new Control[] { discover, preview, create });
            optionALayout.Controls.Add(actions, 0, 6);
            optionA.Controls.Add(optionALayout);

            var optionBLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(16), BackColor = Color.White };
            for (int i = 0; i < 6; i++) optionBLayout.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
            optionBLayout.Controls.Add(new Label { Text = "Model file-name setup", Font = new Font("Segoe UI Semibold", 16F), ForeColor = Color.FromArgb(25, 42, 59), AutoSize = true, Margin = new Padding(0, 0, 0, 8) }, 0, 0);
            optionBLayout.Controls.Add(new Label { Text = "All loaded models are selected by default. Uncheck any to exclude it. Preview Category: Item, Property: File Name, then create one search set per distinct value using that value as the set name.", AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = Color.FromArgb(52, 70, 86), Margin = new Padding(0, 0, 0, 10) }, 0, 1);
            var modelRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 10) };
            modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            modelRow.Controls.Add(new Label { Text = "MODELS", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, ForeColor = Color.FromArgb(25, 42, 59), Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(0, 6, 12, 0) }, 0, 0);
            modelRow.Controls.Add(oneClickModels, 1, 0);
            var modelNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Model model in document.Models)
            {
                string name = string.IsNullOrWhiteSpace(model.FileName) ? "Unnamed model" : System.IO.Path.GetFileName(model.FileName);
                modelNames.TryGetValue(name, out int occurrence); modelNames[name] = ++occurrence;
                int index = oneClickModels.Items.Add(new ModelChoice(model, occurrence == 1 ? name : name + " (" + occurrence + ")"));
                oneClickModels.SetItemChecked(index, true);
            }
            optionBLayout.RowCount = 7;
            optionBLayout.RowStyles.Clear();
            for (int i = 0; i < 7; i++) optionBLayout.RowStyles.Add(new RowStyle(i == 5 ? SizeType.Percent : SizeType.AutoSize, i == 5 ? 100 : 0));
            optionBLayout.Controls.Add(modelRow, 0, 2);
            optionBLayout.Controls.Add(oneClickProperty, 0, 3); oneClickProperty.Text = "Property: Item / File Name";
            optionBLayout.Controls.Add(oneClickSummary, 0, 4); oneClickSummary.Text = "Preview reads File Name values from the selected model and counts exact matches. Existing saved set names are skipped; no existing sets are replaced or deleted.";
            optionBLayout.Controls.Add(oneClickGrid, 0, 5);
            var oneClickActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 10, 0, 0) };
            oneClickActions.Controls.Add(oneClickPreview); oneClickActions.Controls.Add(oneClickCreate);
            optionBLayout.Controls.Add(oneClickActions, 0, 6);
            optionB.Controls.Add(optionBLayout);
            layout.Controls.Add(tabs, 0, 2);
            layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(75, 91, 105), Padding = new Padding(0, 8, 0, 4), Text = "Progress ETA is approximate and stage-local; synchronous native searches may pause updates. New sets are added to a folder and are not saved to the NWF/NWD until you save in Navisworks." }, 0, 3);
            var closeActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            closeActions.Controls.Add(cancel); layout.Controls.Add(closeActions, 0, 4);
            foreach (var button in new[] { discover, preview, create, oneClickPreview, oneClickCreate, cancel }) { button.FlatStyle = FlatStyle.Flat; button.Padding = new Padding(9, 7, 9, 7); button.Margin = new Padding(0, 0, 8, 0); }
            create.BackColor = Color.FromArgb(0, 111, 139); create.ForeColor = Color.White;
            oneClickModels.BorderStyle = BorderStyle.FixedSingle;
            oneClickCreate.BackColor = Color.FromArgb(0, 111, 139); oneClickCreate.ForeColor = Color.White; oneClickCreate.Font = new Font("Segoe UI Semibold", 10F); oneClickCreate.Padding = new Padding(14, 10, 14, 10);
            oneClickGrid.Columns.Add("Value", "Model file number"); oneClickGrid.Columns.Add("Name", "Resulting Set Name"); oneClickGrid.Columns.Add("Status", "Action / validation");
            var oneClickRenameColumn = new DataGridViewButtonColumn { Name = "RenameEdit", HeaderText = "Rename / Edit", Text = "Rename", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat };
            oneClickGrid.Columns.Add(oneClickRenameColumn);
            oneClickGrid.EnableHeadersVisualStyles = false; oneClickGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 42, 59); oneClickGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; oneClickGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            oneClickGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 241, 245); oneClickGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 42, 59); oneClickGrid.RowTemplate.Height = 30;
            oneClickGrid.Columns["Value"].Width = 260; oneClickGrid.Columns["Name"].Width = 340; oneClickGrid.Columns["Status"].Width = 400; oneClickGrid.Columns["RenameEdit"].Width = 145;
            oneClickGrid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == oneClickGrid.Columns["RenameEdit"].Index)
                    EditModelFileSetName(e.RowIndex);
            };
            grid.Columns.Add("Value", "Model number"); grid.Columns.Add("Name", "Resulting Set Name"); grid.Columns.Add("Status", "Action / validation");
            grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 42, 59); grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 241, 245); grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 42, 59); grid.RowTemplate.Height = 30;
            var renameColumn = new DataGridViewButtonColumn { Name = "RenameEdit", HeaderText = "Rename / Edit", Text = "Rename", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat };
            grid.Columns.Add(renameColumn);
            renameColumn.ToolTipText = "Enter a custom name for this search set. NavPro rechecks duplicates and naming rules before creation.";
            foreach (DataGridViewColumn column in grid.Columns) column.ReadOnly = column.Name != "RenameEdit";
            grid.Columns["Value"].Width = 260; grid.Columns["Name"].Width = 340;
            grid.Columns["Status"].Width = 400; grid.Columns["RenameEdit"].Width = 145;
            grid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == grid.Columns["RenameEdit"].Index)
                    EditSetName(Convert.ToInt32(grid.Rows[e.RowIndex].Tag));
            };
            discover.Click += async (s, e) => await Run(Discover);
            preview.Click += async (s, e) => await Run(Preview);
            create.Click += async (s, e) => await Run(Create);
            oneClickPreview.Click += async (s, e) => await Run(PreviewModelFileNumberSets, true);
            oneClickCreate.Click += async (s, e) => await Run(CreatePreviewedModelFileNumberSets, true);
            pinWindow.CheckedChanged += (s, e) => TopMost = pinWindow.Checked;
            oneClickModels.ItemCheck += (s, e) =>
            {
                oneClickResult = null; oneClickPlan = null; oneClickSelectedProperty = null; oneClickGrid.Rows.Clear();
                oneClickProperty.Text = "Property: Item / File Name";
                oneClickSummary.Text = "Model selection changed. Preview again to refresh the combined File Name values.";
                oneClickCreate.Enabled = false;
                BeginInvoke(new Action(SetControls));
            };
            cancel.Click += (s, e) => { if (busy) cancellation.Cancel(); else Close(); };
            condition.Items.Add("="); condition.SelectedIndex = 0;
            value.Items.Add(new ValueChoice(null)); value.SelectedIndex = 0;
            category.SelectedIndexChanged += (s, e) => PopulateProperties();
            property.SelectedIndexChanged += (s, e) =>
            {
                if (updatingSelectors) return;
                result = null; plan = null; grid.Rows.Clear(); ResetValueChoices();
                chosen.Text = SelectedPropertyLabel(); summary.Text = "Preview required before creation."; status.Text = "Property changed. Preview again to inspect matching values."; SetControls();
            };
            value.SelectedIndexChanged += (s, e) =>
            {
                if (updatingSelectors) return;
                result = null; plan = null; grid.Rows.Clear(); summary.Text = "Preview required before creation.";
                status.Text = "Value selection changed. Preview again to count and validate the selected value."; SetControls();
            };
            FormClosing += (s, e) => { if (busy) { e.Cancel = true; cancellation.Cancel(); } };
            FormClosed += (s, e) => inspector.Dispose();
            status.Text = "Ready. Start with Scan properties. All Autodesk model access stays on the host UI thread.";
            DialogTheme.Apply(this, create, oneClickCreate);
        }
        private void UpdateProgress(string stage, long done, long? total)
        {
            status.Text = reporter.Describe(stage, done, total);
            progressStage.Text = stage.ToUpperInvariant();
            progressEta.Text = reporter.Remaining(done, total);
            progress.Indeterminate = !total.HasValue;
            if (total.HasValue) progress.Progress = total.Value == 0 ? 100 : (int)Math.Min(100, 100.0 * done / total.Value);
        }
        private async Task Run(Func<CancellationToken, Task> operation, bool quick = false)
        {
            if (busy) return;
            busy = true; quickOperation = quick; cancellation = new CancellationTokenSource(); SetControls();
            try { await operation(cancellation.Token); }
            catch (OperationCanceledException) { result = null; grid.Rows.Clear(); status.Text = "Cancelled before commit. Existing saved sets were not changed. Run preview again."; if (quickOperation) oneClickSummary.Text = "Cancelled before commit. Existing saved sets were not changed."; }
            catch (CanceledOperationException) { result = null; grid.Rows.Clear(); status.Text = "Native search cancelled. Run preview again; no sets were created."; if (quickOperation) oneClickSummary.Text = "Native search cancelled. No new sets were created."; }
            catch (Exception ex)
            {
                result = null; grid.Rows.Clear(); status.Text = "Error: " + ex.Message + " If creation had reached commit, inspect Sets before retrying; rollback is not guaranteed by the SDK.";
                if (quickOperation) oneClickSummary.Text = "Stopped: " + ex.Message;
                MessageBox.Show(this, status.Text, "NavPro - SetCreator", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { progress.Indeterminate = false; busy = false; quickOperation = false; cancellation.Dispose(); cancellation = null; SetControls(); }
        }
        private void SetControls()
        {
            discover.Enabled = !busy; preview.Enabled = !busy && property.SelectedItem != null; oneClickModels.Enabled = !busy; oneClickPreview.Enabled = !busy && oneClickModels.CheckedItems.Count > 0;
            oneClickCreate.Enabled = !busy && oneClickPlan != null && oneClickPlan.All(p => p.Error == null) &&
                oneClickPlan.Any(p => !p.Exists) && oneClickResult != null && oneClickResult.Values.Any(v => v.Matches > 0);
            category.Enabled = property.Enabled = condition.Enabled = value.Enabled = !busy;
            create.Enabled = !busy && result != null && plan != null && plan.All(p => p.Error == null) && plan.Any(p => !p.Exists) && result.Values.Any(v => v.Matches > 0);
            cancel.Text = busy ? "Cancel operation" : "Close";
        }
        private async Task Discover(CancellationToken token)
        {
            result = null; plan = null; discoveredProperties.Clear(); category.Items.Clear(); property.Items.Clear(); ResetValueChoices(); grid.Rows.Clear(); reporter.NewStage();
            var options = await inspector.Discover(token, (done, total) => UpdateProgress("Discovering model properties", done, total));
            if (options.Count == 0) throw new InvalidOperationException("No searchable properties found. Check the model import/export settings and reload the model.");
            discoveredProperties = options;
            var categories = options.GroupBy(p => p.Key.Category, StringComparer.Ordinal)
                .Select(g => new CategoryOption { InternalName = g.Key, DisplayName = g.Select(p => p.CategoryLabel).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key })
                .OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
            updatingSelectors = true;
            category.Items.AddRange(categories.Cast<object>().ToArray());
            var suggested = options[0];
            category.SelectedItem = categories.First(c => c.InternalName == suggested.Key.Category);
            updatingSelectors = false;
            PopulateProperties(suggested.Key.Id);
            status.Text = $"Found {categories.Count:N0} categories and {options.Count:N0} properties. Confirm the model-number property; suggestions are based on names only.";
        }

        private void PopulateProperties(string preferredId = null)
        {
            if (updatingSelectors) return;
            var selectedCategory = category.SelectedItem as CategoryOption;
            updatingSelectors = true;
            property.Items.Clear();
            if (selectedCategory != null)
            {
                var options = discoveredProperties.Where(p => p.Key.Category == selectedCategory.InternalName).ToList();
                property.Items.AddRange(options.Cast<object>().ToArray());
                var selected = options.FirstOrDefault(p => p.Key.Id == preferredId) ?? options.FirstOrDefault();
                if (selected != null) property.SelectedItem = selected;
            }
            updatingSelectors = false;
            result = null; plan = null; grid.Rows.Clear(); ResetValueChoices();
            chosen.Text = SelectedPropertyLabel(); summary.Text = "Preview required before creation."; SetControls();
        }

        private string SelectedPropertyLabel()
        {
            var selected = property.SelectedItem as PropertyOption;
            return selected == null ? "" : selected.CategoryLabel + " / " + selected.PropertyLabel + "  [" + selected.Key.Category + " / " + selected.Key.Property + "]";
        }

        private void ResetValueChoices()
        {
            updatingSelectors = true;
            value.Items.Clear(); value.Items.Add(new ValueChoice(null)); value.SelectedIndex = 0;
            updatingSelectors = false;
        }

        private void PopulateValueChoices(IList<ValueGroup> values)
        {
            var selected = value.SelectedItem as ValueChoice;
            string selectedKey = selected?.Group?.Criteria.ValueKey;
            updatingSelectors = true;
            value.Items.Clear(); value.Items.Add(new ValueChoice(null));
            foreach (var item in values) value.Items.Add(new ValueChoice(item));
            int index = 0;
            if (selectedKey != null)
                for (int i = 1; i < value.Items.Count; i++)
                    if (((ValueChoice)value.Items[i]).Group.Criteria.ValueKey == selectedKey) { index = i; break; }
            value.SelectedIndex = index;
            updatingSelectors = false;
        }
        private async Task Preview(CancellationToken token)
        {
            result = null; grid.Rows.Clear(); var selected = (PropertyOption)property.SelectedItem;
            reporter.NewStage();
            bool useFileNameValue = string.Equals(selected.PropertyLabel, "File Name", StringComparison.OrdinalIgnoreCase) ||
                ModelNumberPropertyPolicy.Score(selected.Label) >= 2;
            var inspected = await inspector.Inspect(selected.Key, token, (done, total) => UpdateProgress("Collecting distinct model numbers", done, total), useFileNameValue);
            if (inspected.Values.Count == 0) throw new InvalidOperationException("No supported nonblank values for this property. " + inspected.Summary + ". Choose another property or correct the source model.");
            var allValues = inspected.Values.ToList();
            PopulateValueChoices(allValues);
            var selectedValue = (value.SelectedItem as ValueChoice)?.Group;
            if (selectedValue != null) inspected.Values.RemoveAll(v => v.Criteria.ValueKey != selectedValue.Criteria.ValueKey);
            if (inspected.Values.Count == 0) throw new InvalidOperationException("The selected value is no longer available. Choose All values or rescan the current model.");
            reporter.NewStage();
            await creator.Count(inspected, token, (done, total) => UpdateProgress("Counting matching property values", done, total));
            inspector.Check(); result = inspected; summary.Text = result.Summary + $" | Selected values {result.Values.Count:N0}/{allValues.Count:N0}"; RefreshNames();
            status.Text = selectedValue == null
                ? $"Preview complete: {result.Values.Count:N0} distinct typed values. Choose a single value if needed; changing it requires a new preview."
                : $"Preview complete for the exact value '{selectedValue.Display}'. Check its match count and resulting set name before creating.";
        }
        private void RefreshNames()
        {
            if (result == null) { plan = null; SetControls(); return; }
            try
            {
                var existing = creator.ExistingNames();
                // File Name properties may contain absolute paths. Display and name the set
                // from the model filename while preserving the original typed criterion.
                var modelNumbers = result.Values.Select(v => System.IO.Path.GetFileName(v.Display)).ToList();
                plan = NamingPolicy.Plan(modelNumbers, existing);
                for (int i = 0; i < result.Values.Count; i++)
                    if (renamedSets.TryGetValue(RenameKey(result.Values[i]), out var name)) plan[i].Name = name;
                NamingPolicy.Validate(plan, existing);
                grid.Rows.Clear();
                for (int i = 0; i < plan.Count; i++)
                {
                    var item = result.Values[i]; var name = plan[i];
                    int rowIndex = grid.Rows.Add(modelNumbers[i], name.Name, item.Matches == 0 ? "Skip: no matches" : name.Status, "Rename");
                    grid.Rows[rowIndex].Tag = i;
                }
            }
            catch (Exception ex) { result = null; plan = null; status.Text = ex.Message; }
            SetControls();
        }

        private static string RenameKey(ValueGroup item)
        {
            return PropertyKey.Token(item.Criteria.Property.Id) + PropertyKey.Token(item.Criteria.ValueKey);
        }

        private void EditSetName(int rowIndex)
        {
            if (result == null || plan == null || rowIndex < 0 || rowIndex >= result.Values.Count) return;
            var item = result.Values[rowIndex];
            using (var editor = new Form
            {
                Text = "Rename Search Set",
                Font = Font,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(460, 142)
            })
            {
                var editorLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 3 };
                editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                editorLayout.Controls.Add(new Label { Text = "Search set name", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 6) }, 0, 0);
                var nameBox = new TextBox { Dock = DockStyle.Fill, Text = plan[rowIndex].Name };
                editorLayout.Controls.Add(nameBox, 0, 1);
                var editorActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
                var save = new Button { Text = "Apply", DialogResult = DialogResult.OK, AutoSize = true };
                var close = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
                editorActions.Controls.Add(save); editorActions.Controls.Add(close); editorLayout.Controls.Add(editorActions, 0, 2);
                editor.Controls.Add(editorLayout); editor.AcceptButton = save; editor.CancelButton = close;
                DialogTheme.Apply(editor, save);
                nameBox.SelectAll();
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                renamedSets[RenameKey(item)] = nameBox.Text;
            }
            RefreshNames();
            var decision = plan[rowIndex];
            status.Text = decision.Error != null
                ? "Rename needs attention: " + decision.Error
                : decision.Exists
                    ? "This name already exists in the Sets tree; the row will be skipped. Choose a different name to create it."
                    : "Search set rename applied. Review validation, then create the sets.";
        }
        private async Task Create(CancellationToken token)
        {
            RefreshNames();
            if (result == null || plan == null || plan.Any(p => p.Error != null)) throw new InvalidOperationException("Preview is invalid. Run Preview again.");
            int total = result.Values.Count;
            reporter.NewStage();
            int created = await creator.Create(result, plan, token, (done, count) => UpdateProgress("Preparing search sets / committing new folder", done, count));
            RefreshNames(); UpdateProgress("Completed", total, total);
            status.Text += $"\r\nCreated {created:N0}; skipped {total - created:N0}. Existing sets untouched. Save your Navisworks document to retain these sets.";
        }

        private async Task PreviewModelFileNumberSets(CancellationToken token)
        {
            oneClickResult = null; oneClickPlan = null; oneClickSelectedProperty = null; oneClickGrid.Rows.Clear(); SetControls();
            var selectedModels = oneClickModels.CheckedItems.Cast<ModelChoice>().Select(choice => choice.Model).ToList();
            if (selectedModels.Count == 0) throw new InvalidOperationException("Select at least one loaded model before previewing Option B.");
            oneClickSummary.Text = $"Reading Category: Item, Property: File Name from {selectedModels.Count:N0} selected model(s)…";
            reporter.NewStage();
            var selected = new PropertyOption
            {
                Key = new PropertyKey(PropertyCategoryNames.Item, DataPropertyNames.ItemFileName),
                Label = "Item / File Name", CategoryLabel = "Item", PropertyLabel = "File Name"
            };
            oneClickSelectedProperty = selected;
            oneClickProperty.Text = "Property: Item / File Name";
            var inspected = await inspector.Inspect(selectedModels, selected.Key, token, (done, total) => UpdateProgress("Collecting File Name values", done, total), true);
            if (inspected.Values.Count == 0)
                throw new InvalidOperationException("No supported nonblank Item / File Name values were found in the selected models.");

            oneClickSummary.Text = $"Found {inspected.Values.Count:N0} distinct File Name values across {selectedModels.Count:N0} selected model(s). Counting matching items across loaded models…";
            reporter.NewStage();
            var matchScan = await inspector.Inspect(selected.Key, token,
                (done, total) => UpdateProgress("Counting File Name matches across loaded models", done, total), true);
            var matchCounts = matchScan.Values.ToDictionary(value => value.Criteria.ValueKey, StringComparer.Ordinal);
            foreach (var value in inspected.Values)
                value.Matches = matchCounts.TryGetValue(value.Criteria.ValueKey, out var count)
                    ? (int)Math.Min(int.MaxValue, count.Occurrences) : 0;
            inspector.Check();

            var existing = creator.ExistingNames();
            oneClickResult = inspected;
            var modelFileNames = inspected.Values.Select(v => System.IO.Path.GetFileName(v.Display)).ToList();
            oneClickPlan = NamingPolicy.Plan(modelFileNames, existing);
            for (int i = 0; i < oneClickPlan.Count; i++)
            {
                var item = inspected.Values[i]; var decision = oneClickPlan[i];
                int row = oneClickGrid.Rows.Add(System.IO.Path.GetFileName(item.Display), decision.Name,
                    item.Matches == 0 ? "Skip: no matches" : decision.Status);
                oneClickGrid.Rows[row].Tag = i;
            }
            RefreshModelFileSetPreview();
            UpdateProgress("Preview ready", inspected.Values.Count, inspected.Values.Count);
            SetControls();
        }

        private void RefreshModelFileSetPreview()
        {
            if (oneClickResult == null || oneClickPlan == null) { SetControls(); return; }
            NamingPolicy.Validate(oneClickPlan, creator.ExistingNames());
            for (int i = 0; i < oneClickPlan.Count; i++)
                oneClickGrid.Rows[i].Cells["Status"].Value = oneClickResult.Values[i].Matches == 0
                    ? "Skip: no matches" : oneClickPlan[i].Status;
            int invalidNames = oneClickPlan.Count(p => p.Error != null);
            int eligible = Enumerable.Range(0, oneClickPlan.Count)
                .Count(i => oneClickPlan[i].Error == null && !oneClickPlan[i].Exists && oneClickResult.Values[i].Matches > 0);
            oneClickSummary.Text = $"Preview ready: {oneClickResult.Values.Count:N0} distinct model file names, {eligible:N0} ready to create, {oneClickPlan.Count(p => p.Exists):N0} existing names to skip, {oneClickResult.Values.Count(v => v.Matches == 0):N0} with no matches." +
                (invalidNames > 0 ? $" {invalidNames:N0} names need Rename / Edit before creation." : " Review or rename the list, then click Create previewed sets.");
            SetControls();
        }

        private void EditModelFileSetName(int rowIndex)
        {
            if (oneClickResult == null || oneClickPlan == null || rowIndex < 0 || rowIndex >= oneClickPlan.Count) return;
            using (var editor = new Form
            {
                Text = "Rename Search Set", Font = Font, StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
                ShowInTaskbar = false, ClientSize = new Size(460, 142)
            })
            {
                var editorLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 3 };
                editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                editorLayout.Controls.Add(new Label { Text = "Search set name", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 6) }, 0, 0);
                var nameBox = new TextBox { Dock = DockStyle.Fill, Text = oneClickPlan[rowIndex].Name };
                editorLayout.Controls.Add(nameBox, 0, 1);
                var editorActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
                var save = new Button { Text = "Apply", DialogResult = DialogResult.OK, AutoSize = true };
                var close = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
                editorActions.Controls.Add(save); editorActions.Controls.Add(close); editorLayout.Controls.Add(editorActions, 0, 2);
                editor.Controls.Add(editorLayout); editor.AcceptButton = save; editor.CancelButton = close; nameBox.SelectAll();
                DialogTheme.Apply(editor, save);
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                oneClickPlan[rowIndex].Name = nameBox.Text;
            }
            oneClickGrid.Rows[rowIndex].Cells["Name"].Value = oneClickPlan[rowIndex].Name;
            RefreshModelFileSetPreview();
            var decision = oneClickPlan[rowIndex];
            status.Text = decision.Error != null ? "Rename needs attention: " + decision.Error
                : decision.Exists ? "This name already exists in the Sets tree; the row will be skipped. Choose another name to create it."
                : "Search set rename applied. Review validation, then create the sets.";
        }

        private async Task CreatePreviewedModelFileNumberSets(CancellationToken token)
        {
            if (oneClickResult == null || oneClickPlan == null || oneClickSelectedProperty == null)
                throw new InvalidOperationException("Preview the File Name search sets before creating them.");
            inspector.Check();
            NamingPolicy.Validate(oneClickPlan, creator.ExistingNames());
            if (oneClickPlan.Any(p => p.Error != null))
                throw new InvalidOperationException("The preview includes invalid or duplicate names. Use Rename / Edit on each affected row before creating.");

            var willCreate = oneClickPlan.Select((decision, index) => !decision.Exists && oneClickResult.Values[index].Matches > 0).ToArray();
            int totalValues = oneClickResult.Values.Count;
            oneClickSummary.Text = "Creating the previewed search sets…";
            reporter.NewStage();
            int created = await creator.Create(oneClickResult, oneClickPlan,
                token, (done, total) => UpdateProgress("Creating previewed File Name sets", done, total));
            inspector.Check();
            int skipped = totalValues - created;
            for (int i = 0; i < oneClickGrid.Rows.Count; i++)
                if (willCreate[i]) oneClickGrid.Rows[i].Cells["Status"].Value = "Created";
            oneClickSummary.Text = $"Complete. Created {created:N0} search sets; skipped {skipped:N0} values with existing names or no matches. Save the Navisworks document to retain them.";
            status.Text = $"Option B complete using Item / File Name. Created {created:N0}; skipped {skipped:N0}.";
            oneClickPlan = null;
            UpdateProgress("Completed", totalValues, totalValues);
            SetControls();
        }
    }
}
