using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Classes;
using NavPro.Frontend.Ui;
using Color = System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    public sealed class ClashMatrixDialog : Form
    {
        private sealed class ClashTypeChoice
        {
            public ClashTestType Type { get; }
            public string Label { get; }
            public ClashTypeChoice(ClashTestType type, string label) { Type = type; Label = label; }
            public override string ToString() => Label;
        }

        private readonly Document document;
        private readonly ClashMatrixService service;
        private readonly CheckBox pin = new CheckBox { Text = "Pin window", AutoSize = true, ForeColor = Color.White, BackColor = Color.FromArgb(20, 39, 58), FlatStyle = FlatStyle.Flat };
        private readonly CheckedListBox sideA = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        private readonly CheckedListBox sideB = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        private readonly ComboBox sideAGroup = CreateGroupPicker();
        private readonly ComboBox sideBGroup = CreateGroupPicker();
        private readonly ComboBox toleranceUnits = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, AccessibleName = "Tolerance units" };
        private readonly ListBox models = new ListBox
        {
            Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle,
            ScrollAlwaysVisible = true, HorizontalScrollbar = true
        };
        private readonly ComboBox clashType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 205 };
        private readonly NumericUpDown tolerance = new NumericUpDown { DecimalPlaces = 6, Minimum = 0, Maximum = 1000000, Increment = 0.001M, Value = 0, Width = 122, ThousandsSeparator = true };
        private readonly CheckBox aTriangles = new CheckBox { Text = "A surfaces", AutoSize = true, Checked = true };
        private readonly CheckBox aLines = new CheckBox { Text = "A lines", AutoSize = true };
        private readonly CheckBox aPoints = new CheckBox { Text = "A points", AutoSize = true };
        private readonly CheckBox bTriangles = new CheckBox { Text = "B surfaces", AutoSize = true, Checked = true };
        private readonly CheckBox bLines = new CheckBox { Text = "B lines", AutoSize = true };
        private readonly CheckBox bPoints = new CheckBox { Text = "B points", AutoSize = true };
        private readonly RadioButton selfIntersect = new RadioButton { Text = "Self-Intersect", AutoSize = true };
        private readonly RadioButton excludeSelfSelf = new RadioButton { Text = "Exclude Self-Self", AutoSize = true, Checked = true };
        private readonly CheckBox compositeObjectClashing = new CheckBox { Text = "Composite Object Clashing", AutoSize = true };
        private readonly TextBox namePrefix = new TextBox { Width = 150 };
        private readonly TextBox nameSeparator = new TextBox { Width = 70, Text = " vs " };
        private readonly Label unitsLabel = new Label { AutoSize = true, ForeColor = Color.FromArgb(67, 84, 101), Padding = new Padding(0, 6, 0, 0) };
        private readonly Label summary = new Label { AutoSize = true, ForeColor = Color.FromArgb(65, 82, 99), Dock = DockStyle.Fill };
        private readonly Label existingSummary = new Label { AutoSize = true, ForeColor = Color.FromArgb(65, 82, 99), Dock = DockStyle.Fill };
        private readonly Label status = new Label { AutoSize = true, ForeColor = Color.FromArgb(35, 75, 96), Dock = DockStyle.Fill };
        private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Height = 9, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25 };
        private readonly DataGridView grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, ScrollBars = ScrollBars.Both,
            RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None
        };
        private readonly Button refresh = new Button { Text = "Refresh models & sets", AutoSize = true };
        private readonly Button preview = new Button { Text = "Preview matrix", AutoSize = true };
        private readonly Button create = new Button { Text = "Create previewed tests", AutoSize = true, Enabled = false };
        private readonly Button runClashes = new Button { Text = "Run Clashes", AutoSize = true, Enabled = false };
        private readonly Button close = new Button { Text = "Close", AutoSize = true };
        private ClashMatrixSnapshot snapshot;
        private List<ClashMatrixPair> plan = new List<ClashMatrixPair>();
        private readonly List<string> pendingRunNames = new List<string>();
        private bool busy;
        private bool createdForCurrentPlan;
        private bool changingToleranceUnits;
        private Units previousToleranceUnits;

        public ClashMatrixDialog(Document document)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            service = new ClashMatrixService(document);
            Text = "NavPro | Clash Matrix"; Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1320, 880); MinimumSize = new Size(1020, 720); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(241, 244, 248); MinimizeBox = false; MaximizeBox = true;
            BuildLayout();
            previousToleranceUnits = document.Units;
            refresh.Click += async (s, e) => await RefreshData();
            preview.Click += async (s, e) => await PreviewMatrix();
            create.Click += (s, e) => CreateTests();
            runClashes.Click += (s, e) => RunClashes();
            grid.CellEndEdit += (s, e) => ApplyEditedName(e.RowIndex);
            close.Click += (s, e) => Close();
            pin.CheckedChanged += (s, e) => TopMost = pin.Checked;
            sideA.ItemCheck += InvalidatePreview;
            sideB.ItemCheck += InvalidatePreview;
            sideAGroup.SelectedIndexChanged += (s, e) => LoadGroup(sideAGroup, sideA);
            sideBGroup.SelectedIndexChanged += (s, e) => LoadGroup(sideBGroup, sideB);
            clashType.SelectedIndexChanged += (s, e) => InvalidatePreview();
            tolerance.ValueChanged += (s, e) => { if (!changingToleranceUnits) InvalidatePreview(); };
            toleranceUnits.SelectedIndexChanged += (s, e) => OnToleranceUnitsChanged();
            namePrefix.TextChanged += (s, e) => ApplyNameOptions();
            nameSeparator.TextChanged += (s, e) => ApplyNameOptions();
            foreach (var option in new Control[] { aTriangles, aLines, aPoints, bTriangles, bLines, bPoints, selfIntersect, excludeSelfSelf, compositeObjectClashing }) option.Click += (s, e) => InvalidatePreview();
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 39, 58);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 239, 245);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 39, 58);
            grid.RowTemplate.Height = 30;
            grid.Columns.Add("A", "Selection A"); grid.Columns.Add("B", "Selection B");
            grid.Columns.Add("Test", "Proposed Clash Test"); grid.Columns.Add("Type", "Clash Type");
            grid.Columns.Add("Tolerance", "Tolerance"); grid.Columns.Add("GeometryA", "Selection A geometry"); grid.Columns.Add("GeometryB", "Selection B geometry"); grid.Columns.Add("Composite", "Composite Object Clashing"); grid.Columns.Add("Status", "QA / validation");
            foreach (DataGridViewColumn column in grid.Columns) column.ReadOnly = column.Name != "Test";
            grid.Columns["A"].Width = 220; grid.Columns["B"].Width = 220; grid.Columns["Test"].Width = 300;
            grid.Columns["Type"].Width = 150; grid.Columns["Tolerance"].Width = 130; grid.Columns["Status"].Width = 280;
            grid.Columns["GeometryA"].Width = 190; grid.Columns["GeometryB"].Width = 190; grid.Columns["Composite"].Width = 190;
            DialogTheme.Apply(this, create);
            Shown += async (s, e) => await RefreshData();
        }

        private void BuildLayout()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 8 };
            Controls.Add(layout);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, BackColor = Color.FromArgb(20, 39, 58), Padding = new Padding(16), Margin = new Padding(0, 0, 0, 10) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            var title = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.FromArgb(20, 39, 58) };
            title.Controls.Add(new Label { Text = "NavPro", Font = new Font("Segoe UI Semibold", 22F), ForeColor = Color.White, AutoSize = true }, 0, 0);
            title.Controls.Add(new Label { Text = "Clash Matrix  ·  Matrix Designer", ForeColor = Color.FromArgb(198, 215, 229), AutoSize = true }, 0, 1);
            title.Controls.Add(pin, 0, 2);
            header.Controls.Add(title, 0, 0);
            var headerNote = new Label { Text = "DESIGN\nPreview first\nCreate tests only when ready", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.FromArgb(191, 221, 235), Font = new Font("Segoe UI Semibold", 10F), BackColor = Color.FromArgb(20, 39, 58) };
            header.Controls.Add(headerNote, 1, 0); layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(new Label { Text = "Choose a Navisworks selection-tree view for each side. Standard lists loaded model files; Compact chooses model-tree nodes; Properties uses property-based Search Sets; Sets shows all saved Selection and Search Sets.", AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(48, 67, 84), Padding = new Padding(2, 4, 2, 8) }, 0, 1);

            var modelPanel = MakePanel("LOADED MODELS", models);
            layout.Controls.Add(modelPanel, 0, 2);
            var pick = ResizableLayout.Split(MakeSelectionPanel("SELECTION A  ·  DISCIPLINE / SYSTEM SETS", sideAGroup, sideA),
                MakeSelectionPanel("SELECTION B  ·  DISCIPLINE / SYSTEM SETS", sideBGroup, sideB), Orientation.Vertical, 0.5, "Selection A and B");
            layout.Controls.Add(pick, 0, 3);

            var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(10, 8, 10, 8), BackColor = Color.White, Margin = new Padding(0, 0, 0, 8) };
            settings.Controls.Add(new Label { Text = "CLASH PROFILE", AutoSize = true, Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.FromArgb(20, 39, 58), Padding = new Padding(0, 7, 8, 0) });
            clashType.Items.Add(new ClashTypeChoice(ClashTestType.Hard, "Hard"));
            clashType.Items.Add(new ClashTypeChoice(ClashTestType.HardConservative, "Hard (Conservative)"));
            clashType.Items.Add(new ClashTypeChoice(ClashTestType.Clearance, "Clearance"));
            clashType.Items.Add(new ClashTypeChoice(ClashTestType.Duplicate, "Duplicate"));
            clashType.SelectedIndex = 0; settings.Controls.Add(clashType);
            settings.Controls.Add(new Label { Text = "Tolerance", AutoSize = true, ForeColor = Color.FromArgb(48, 67, 84), Padding = new Padding(14, 7, 4, 0) });
            settings.Controls.Add(tolerance);
            foreach (Units unit in Enum.GetValues(typeof(Units))) toleranceUnits.Items.Add(unit);
            toleranceUnits.SelectedItem = document.Units;
            settings.Controls.Add(toleranceUnits);
            UpdateUnitsLabel();
            settings.Controls.Add(unitsLabel);
            settings.Controls.Add(new Label { Text = "Changing units converts the tolerance to preserve the same distance. The preview follows the selected units; test values are stored in the model document units. Verify project standards before creating tests.", AutoSize = true, ForeColor = Color.FromArgb(95, 108, 120), Padding = new Padding(12, 7, 0, 0) });
            settings.Controls.Add(new Label { Text = "SELECTION GEOMETRY", AutoSize = true, Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.FromArgb(20, 39, 58), Padding = new Padding(12, 7, 0, 0) });
            foreach (var option in new Control[] { aTriangles, aLines, aPoints, bTriangles, bLines, bPoints, selfIntersect, excludeSelfSelf, compositeObjectClashing }) settings.Controls.Add(option);
            settings.Controls.Add(new Label { Text = "Prefix", AutoSize = true, ForeColor = Color.FromArgb(48, 67, 84), Padding = new Padding(12, 7, 4, 0) });
            settings.Controls.Add(namePrefix);
            settings.Controls.Add(new Label { Text = "Separator", AutoSize = true, ForeColor = Color.FromArgb(48, 67, 84), Padding = new Padding(8, 7, 4, 0) });
            settings.Controls.Add(nameSeparator);
            layout.Controls.Add(settings, 0, 4);

            var previewArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White, Padding = new Padding(10), Margin = new Padding(0, 0, 0, 8) };
            previewArea.RowStyles.Add(new RowStyle(SizeType.AutoSize)); previewArea.RowStyles.Add(new RowStyle(SizeType.AutoSize)); previewArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewArea.Controls.Add(summary, 0, 0); previewArea.Controls.Add(existingSummary, 0, 1); previewArea.Controls.Add(grid, 0, 2);
            layout.Controls.Add(previewArea, 0, 5);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 3, 0, 4) };
            foreach (var button in new[] { refresh, preview, create, runClashes }) { button.FlatStyle = FlatStyle.Flat; button.Padding = new Padding(10, 7, 10, 7); }
            create.BackColor = Color.FromArgb(0, 111, 139); create.ForeColor = Color.White; create.Font = new Font("Segoe UI Semibold", 10F); create.Padding = new Padding(14, 9, 14, 9);
            runClashes.BackColor = Color.FromArgb(35, 30, 220); runClashes.ForeColor = Color.White; runClashes.Font = new Font("Segoe UI Semibold", 10F); runClashes.Padding = new Padding(14, 9, 14, 9);
            actions.Controls.Add(refresh); actions.Controls.Add(preview); actions.Controls.Add(create); actions.Controls.Add(runClashes);
            layout.Controls.Add(actions, 0, 6);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, AutoSize = true };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(progress, 0, 1); footer.Controls.Add(close, 1, 0); layout.Controls.Add(footer, 0, 7);
            // Keep the profile outside the flexible regions so it cannot consume the preview's height.
            layout.Controls.Remove(settings);
            layout.SetRow(modelPanel, 3); layout.SetRow(pick, 4);
            layout.RowStyles[2] = new RowStyle(SizeType.Absolute, 140);
            layout.RowStyles[3] = new RowStyle(SizeType.Percent, 30);
            layout.RowStyles[4] = new RowStyle(SizeType.Percent, 40);
            settings.Dock = DockStyle.Top;
            var profileViewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            profileViewport.Controls.Add(settings); layout.Controls.Add(profileViewport, 0, 2);
            var regions = ResizableLayout.Rows(layout, 3, 5, 4, 0.6, "Clash selections and matrix preview");
            ResizableLayout.Rows((TableLayoutPanel)regions.Panel1.Controls[0], 0, 1, 0, 0.35, "Loaded models and selections");
        }

        private static Control MakePanel(string title, Control content)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8), BackColor = Color.White, Margin = new Padding(4) };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = Color.FromArgb(20, 39, 58), Padding = new Padding(2, 0, 0, 6) }, 0, 0);
            panel.Controls.Add(content, 0, 1); return panel;
        }

        private static ComboBox CreateGroupPicker()
        {
            var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 6) };
            picker.Items.AddRange(new object[] { "Standard", "Compact", "Properties", "Sets" });
            picker.SelectedItem = "Sets";
            return picker;
        }

        private static Control MakeSelectionPanel(string title, ComboBox group, CheckedListBox content)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8), BackColor = Color.White, Margin = new Padding(4) };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = Color.FromArgb(20, 39, 58), Padding = new Padding(2, 0, 0, 6) }, 0, 0);
            panel.Controls.Add(group, 0, 1); panel.Controls.Add(content, 0, 2); return panel;
        }

        private async Task RefreshData()
        {
            if (busy) return;
            busy = true; SetControls(); status.Text = "Reading loaded models, Search Sets and existing Clash Detective tests…";
            try
            {
                snapshot = service.ReadSnapshot();
                models.Items.Clear(); foreach (var model in snapshot.Models) models.Items.Add(model);
                models.HorizontalExtent = snapshot.Models.Count == 0 ? 0 : snapshot.Models.Max(model => TextRenderer.MeasureText(model.ToString(), models.Font).Width + 24);
                sideA.Items.Clear(); sideB.Items.Clear(); grid.Rows.Clear(); plan.Clear();
                sideAGroup.SelectedItem = "Sets"; sideBGroup.SelectedItem = "Sets";
                LoadGroup(sideAGroup, sideA); LoadGroup(sideBGroup, sideB);
                unitsLabel.Text = "Document units: " + document.Units;
                var profiles = snapshot.ExistingTests.Select(t => t.TestType + " / " + t.Tolerance.ToString("G6")).Distinct(StringComparer.Ordinal).ToList();
                var statusCounts = string.Join("  ·  ", snapshot.ResultStatuses.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + ": " + kv.Value.ToString("N0")));
                existingSummary.Text = $"Existing Clash tests: {snapshot.ExistingTests.Count:N0}  ·  Tests with a Last Run: {snapshot.TestsRun:N0}  ·  Results: {snapshot.ResultCount:N0}" +
                    (string.IsNullOrEmpty(statusCounts) ? "" : "  ·  " + statusCounts) + $"  ·  Profiles in use: {profiles.Count:N0}" +
                    (profiles.Count > 1 ? "  ·  Mixed type/tolerance settings detected" : "  ·  Settings are consistent");
                summary.Text = $"Loaded {snapshot.Models.Count:N0} model(s), {snapshot.Sets.Count:N0} saved set(s), including {snapshot.Sets.Count(s => s.IsSearchSet):N0} Search Sets.";
                status.Text = "Ready. Choose a view on each side, select items or sets, then preview.";
            }
            catch (Exception ex) { status.Text = "Refresh failed: " + ex.Message; }
            finally { busy = false; SetControls(); await Task.CompletedTask; }
        }

        private void LoadGroup(ComboBox picker, CheckedListBox list)
        {
            if (snapshot == null) return;
            var view = Convert.ToString(picker.SelectedItem);
            List<ClashSetChoice> choices;
            if (view == "Standard") choices = service.ReadModelTreeChoices(false);
            else if (view == "Compact") choices = service.ReadModelTreeChoices(true);
            else if (view == "Properties") choices = snapshot.Sets.Where(s => s.IsSearchSet).ToList();
            else choices = snapshot.Sets;
            list.BeginUpdate();
            try { list.Items.Clear(); foreach (var choice in choices) list.Items.Add(choice); }
            finally { list.EndUpdate(); }
            InvalidatePreview();
        }

        private void OnToleranceUnitsChanged()
        {
            if (changingToleranceUnits || !(toleranceUnits.SelectedItem is Units selectedUnits) || selectedUnits == previousToleranceUnits) return;

            Units oldUnits = previousToleranceUnits;
            decimal oldValue = tolerance.Value;
            bool hasPreview = plan.Count > 0;
            decimal convertedValue;
            try
            {
                double scale = UnitConversion.ScaleFactor(oldUnits, selectedUnits);
                double converted = (double)oldValue * scale;
                if (double.IsNaN(converted) || double.IsInfinity(converted) || converted > (double)tolerance.Maximum)
                    throw new InvalidOperationException("The converted tolerance is outside the input range.");

                convertedValue = decimal.Round((decimal)converted, tolerance.DecimalPlaces, MidpointRounding.AwayFromZero);
                if (oldValue > 0 && convertedValue == 0)
                    throw new InvalidOperationException("The converted value is too small for the displayed precision.");
            }
            catch (Exception ex)
            {
                changingToleranceUnits = true;
                toleranceUnits.SelectedItem = oldUnits;
                changingToleranceUnits = false;
                status.Text = "Tolerance units were not changed: " + ex.Message;
                return;
            }

            changingToleranceUnits = true;
            tolerance.Value = convertedValue;
            previousToleranceUnits = selectedUnits;
            changingToleranceUnits = false;
            UpdateUnitsLabel();

            if (hasPreview && !createdForCurrentPlan) RefreshPreviewToleranceValues();
            else InvalidatePreview();
        }

        private void RefreshPreviewToleranceValues()
        {
            Units inputUnits = (Units)toleranceUnits.SelectedItem;
            double documentTolerance = (double)tolerance.Value * UnitConversion.ScaleFactor(inputUnits, document.Units);
            for (int i = 0; i < plan.Count; i++)
            {
                plan[i].Tolerance = documentTolerance;
                if (i < grid.Rows.Count)
                    grid.Rows[i].Cells[4].Value = FormatTolerancePreview(tolerance.Value, inputUnits);
            }

            var selectedType = clashType.SelectedItem as ClashTypeChoice;
            int ready = plan.Count(p => p.Ready);
            int empty = plan.Count(p => p.A.ItemCount == 0 || p.B.ItemCount == 0);
            int warnings = plan.Count(p => p.Validation.StartsWith("Warning:", StringComparison.Ordinal));
            int duplicate = plan.Count(p => p.Validation.StartsWith("Duplicate", StringComparison.Ordinal));
            summary.Text = $"Preview: {plan.Count:N0} A/B pairs  ·  {ready:N0} ready  ·  {empty:N0} empty Search Set rows ({warnings:N0} allowed with warning)  ·  {duplicate:N0} duplicate rows. Type: {selectedType?.Label ?? "Unknown"}; tolerance: {tolerance.Value:G6} {inputUnits} (stored as {documentTolerance:G6} {document.Units}).";
            SetControls();
        }

        private void UpdateUnitsLabel()
        {
            var inputUnits = toleranceUnits.SelectedItem is Units selected ? selected : document.Units;
            unitsLabel.Text = $"Tolerance / preview units: {inputUnits}  ·  Model document units: {document.Units}";
        }

        private static string FormatTolerancePreview(decimal inputTolerance, Units inputUnits)
        {
            return $"{inputTolerance:G6} {inputUnits}";
        }

        private async Task PreviewMatrix()
        {
            if (busy || snapshot == null) return;
            var selectedA = sideA.CheckedItems.Cast<ClashSetChoice>().ToList();
            var selectedB = sideB.CheckedItems.Cast<ClashSetChoice>().ToList();
            if (selectedA.Count == 0 || selectedB.Count == 0)
            {
                MessageBox.Show(this, "Choose at least one item or saved set in both Selection A and Selection B.", "Clash Matrix", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            busy = true; SetControls(); grid.Rows.Clear(); plan.Clear(); createdForCurrentPlan = false; progress.Style = ProgressBarStyle.Marquee;
            try
            {
                var unique = selectedA.Concat(selectedB).Distinct().ToList();
                for (int i = 0; i < unique.Count; i++)
                {
                    status.Text = $"Checking set contents {i + 1:N0} of {unique.Count:N0}: {unique[i].Name}";
                    unique[i].ItemCount = service.CountItems(unique[i]);
                    await Task.Delay(15);
                    if (IsDisposed) return;
                }
                var selectedType = (ClashTypeChoice)clashType.SelectedItem;
                var inputUnits = (Units)toleranceUnits.SelectedItem;
                double documentTolerance = (double)tolerance.Value * UnitConversion.ScaleFactor(inputUnits, document.Units);
                plan = service.BuildPreview(selectedA, selectedB, selectedType.Type, documentTolerance);
                ApplyNameOptions();
                var primitivesA = SelectedPrimitives(aTriangles, aLines, aPoints);
                var primitivesB = SelectedPrimitives(bTriangles, bLines, bPoints);
                foreach (var pair in plan)
                {
                    pair.PrimitiveTypesA = primitivesA; pair.PrimitiveTypesB = primitivesB;
                    pair.SelfIntersect = selfIntersect.Checked;
                    pair.CompositeObjectClashing = compositeObjectClashing.Checked;
                    if (primitivesA == 0 || primitivesB == 0) pair.Validation = "Invalid: select geometry types for both sides";
                }
                foreach (var pair in plan)
                    grid.Rows.Add(pair.A.Path, pair.B.Path, pair.TestName, pair.Type.ToString(),
                    FormatTolerancePreview(tolerance.Value, (Units)toleranceUnits.SelectedItem), GeometryLabel(pair.PrimitiveTypesA, pair.SelfIntersect), GeometryLabel(pair.PrimitiveTypesB, pair.SelfIntersect), pair.CompositeObjectClashing ? "Enabled" : "Disabled", pair.Validation);
                int ready = plan.Count(p => p.Ready);
                int empty = plan.Count(p => p.A.ItemCount == 0 || p.B.ItemCount == 0);
                int warnings = plan.Count(p => p.Validation.StartsWith("Warning:", StringComparison.Ordinal));
                int duplicate = plan.Count(p => p.Validation.StartsWith("Duplicate", StringComparison.Ordinal));
                summary.Text = $"Preview: {plan.Count:N0} A/B pairs  ·  {ready:N0} ready  ·  {empty:N0} empty Search Set rows ({warnings:N0} allowed with warning)  ·  {duplicate:N0} duplicate rows. Type: {selectedType.Label}; tolerance: {tolerance.Value:G6} {inputUnits} (stored as {documentTolerance:G6} {document.Units}).";
                status.Text = "Review every pair and validation status. Creating tests will not run them; run and review results in Clash Detective.";
            }
            catch (Exception ex) { status.Text = "Preview failed: " + ex.Message; }
            finally { if (!IsDisposed) { busy = false; progress.Style = ProgressBarStyle.Blocks; SetControls(); } }
        }

        private void CreateTests()
        {
            var ready = plan.Where(p => p.Ready).ToList();
            if (ready.Count == 0) return;
            int emptyWarnings = ready.Count(p => p.Validation.StartsWith("Warning:", StringComparison.Ordinal));
            string emptyNote = emptyWarnings == 0 ? string.Empty : $"\r\n\r\nWarning: {emptyWarnings:N0} pair(s) use Search Sets that currently return no items. Their saved dynamic sources will be retained, but these tests may produce no results until the sets match model items.";
            var answer = MessageBox.Show(this,
                $"Create {ready.Count:N0} Clash Detective test(s) from the reviewed matrix? Existing tests will be preserved. NavPro will not run the tests.{emptyNote}",
                "Create previewed clash tests", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try
            {
                busy = true; SetControls();
                int created = service.CreateTests(plan);
                foreach (string name in plan.Where(p => p.Ready).Select(p => p.TestName))
                    if (!pendingRunNames.Contains(name, StringComparer.OrdinalIgnoreCase)) pendingRunNames.Add(name);
                createdForCurrentPlan = true;
                status.Text = $"Verified {created:N0} Clash Detective test(s) in the Tests list. Click Run Clashes to run these new tests, then review the results and save the NWF/NWD.";
                create.Enabled = false;
                MessageBox.Show(this, status.Text, "Clash Matrix creation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                createdForCurrentPlan = true;
                status.Text = "Creation could not be verified: " + ex.Message + " Inspect the Clash Detective Tests list before retrying; rollback is not guaranteed.";
                MessageBox.Show(this, status.Text, "Clash Matrix creation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { busy = false; SetControls(); }
        }

        private void RunClashes()
        {
            if (busy || pendingRunNames.Count == 0) return;
            try
            {
                busy = true; SetControls();
                int total = pendingRunNames.Count;
                status.Text = $"Running {total:N0} Clash Matrix test(s). Navisworks runs each test synchronously; a test may pause this dialog until it completes.";
                progress.Style = ProgressBarStyle.Marquee; status.Refresh(); progress.Refresh();
                int completed = service.RunTests(pendingRunNames.ToList(), (name, done, count) =>
                {
                    pendingRunNames.RemoveAll(pending => string.Equals(pending, name, StringComparison.OrdinalIgnoreCase));
                    status.Text = $"Completed {done:N0} of {count:N0} Clash Matrix test(s). Last completed: {name}";
                    status.Refresh();
                });
                status.Text = $"Completed all {completed:N0} newly created Clash Matrix test(s). Review results in Clash Detective, then save the NWF/NWD.";
                MessageBox.Show(this, status.Text, "Run Clashes complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                status.Text = "Clash run stopped: " + ex.Message + (pendingRunNames.Count > 0 ? " Remaining newly created tests are available to run again." : "");
                MessageBox.Show(this, status.Text, "Run Clashes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { busy = false; progress.Style = ProgressBarStyle.Blocks; SetControls(); }
        }

        private void InvalidatePreview(object sender, ItemCheckEventArgs e)
        {
            if (IsHandleCreated) BeginInvoke((Action)InvalidatePreview);
        }

        private void InvalidatePreview()
        {
            if (busy) return;
            plan.Clear(); grid.Rows.Clear(); createdForCurrentPlan = false;
            summary.Text = snapshot == null ? "Refresh data to start." : "Settings or selected sets changed. Preview the matrix again before creating tests.";
            SetControls();
        }

        private void SetControls()
        {
            refresh.Enabled = !busy; preview.Enabled = !busy && snapshot != null;
            runClashes.Enabled = !busy && pendingRunNames.Count > 0;
            sideA.Enabled = sideB.Enabled = sideAGroup.Enabled = sideBGroup.Enabled = clashType.Enabled = tolerance.Enabled = !busy;
            toleranceUnits.Enabled = !busy;
            foreach (var option in new Control[] { aTriangles, aLines, aPoints, bTriangles, bLines, bPoints, selfIntersect, excludeSelfSelf, compositeObjectClashing }) option.Enabled = !busy;
            namePrefix.Enabled = nameSeparator.Enabled = !busy;
            create.Enabled = !busy && !createdForCurrentPlan && plan.Any(p => p.Ready);
            close.Enabled = !busy;
            close.Text = busy ? "Working…" : "Close";
        }

        private void ApplyEditedName(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= plan.Count) return;
            plan[rowIndex].TestName = Convert.ToString(grid.Rows[rowIndex].Cells["Test"].Value) ?? string.Empty;
            var existing = new HashSet<string>(snapshot.ExistingTests.Select(t => t.DisplayName), StringComparer.OrdinalIgnoreCase);
            var duplicateNames = new HashSet<string>(plan.GroupBy(p => p.TestName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var pair in plan)
            {
                if (string.IsNullOrWhiteSpace(pair.TestName)) pair.Validation = "Invalid: test name is blank";
                else if (existing.Contains(pair.TestName)) pair.Validation = "Existing test name";
                else if (duplicateNames.Contains(pair.TestName)) pair.Validation = "Duplicate test name";
                else if (pair.Validation == "Invalid: test name is blank" || pair.Validation == "Duplicate test name" || pair.Validation == "Existing test name") pair.Validation = "Ready";
                grid.Rows[plan.IndexOf(pair)].Cells["Status"].Value = pair.Validation;
            }
            create.Enabled = !busy && plan.Any(p => p.Ready);
        }

        private void ApplyNameOptions()
        {
            if (plan.Count == 0) return;
            foreach (var pair in plan)
            {
                pair.TestName = (namePrefix.Text ?? string.Empty) + pair.A.Name + (nameSeparator.Text ?? string.Empty) + pair.B.Name;
                int row = plan.IndexOf(pair);
                if (row < grid.Rows.Count) grid.Rows[row].Cells["Test"].Value = pair.TestName;
            }
            RevalidateNames();
        }

        private void RevalidateNames()
        {
            var existing = new HashSet<string>(snapshot.ExistingTests.Select(t => t.DisplayName), StringComparer.OrdinalIgnoreCase);
            var duplicateNames = new HashSet<string>(plan.GroupBy(p => p.TestName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var pair in plan)
            {
                if (string.IsNullOrWhiteSpace(pair.TestName)) pair.Validation = "Invalid: test name is blank";
                else if (existing.Contains(pair.TestName)) pair.Validation = "Existing test name";
                else if (duplicateNames.Contains(pair.TestName)) pair.Validation = "Duplicate test name";
                else if (pair.Validation == "Invalid: test name is blank" || pair.Validation == "Duplicate test name" || pair.Validation == "Existing test name") pair.Validation = "Ready";
                int row = plan.IndexOf(pair);
                if (row < grid.Rows.Count) grid.Rows[row].Cells["Status"].Value = pair.Validation;
            }
            create.Enabled = !busy && plan.Any(p => p.Ready);
        }

        private static PrimitiveTypes SelectedPrimitives(CheckBox triangles, CheckBox lines, CheckBox points)
        {
            PrimitiveTypes value = 0;
            if (triangles.Checked) value |= PrimitiveTypes.Triangles;
            if (lines.Checked) value |= PrimitiveTypes.Lines;
            if (points.Checked) value |= PrimitiveTypes.Points;
            return value;
        }

        private static string GeometryLabel(PrimitiveTypes value, bool selfIntersect)
        {
            var names = new List<string>();
            if ((value & PrimitiveTypes.Triangles) != 0) names.Add("Surfaces");
            if ((value & PrimitiveTypes.Lines) != 0) names.Add("Lines");
            if ((value & PrimitiveTypes.Points) != 0) names.Add("Points");
            if (selfIntersect) names.Add("Self-check");
            return string.Join(", ", names);
        }
    }
}
