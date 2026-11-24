using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using NavPro.Frontend.Dialogs;
using NavPro.Classes;
using NavPro.Backend.Services;
using Autodesk.Navisworks.Api.Clash;
using NavApplication = Autodesk.Navisworks.Api.Application;

namespace NavPro.Frontend.Plugins
{
    [Plugin(NavProIdentity.ProductName, NavProIdentity.DeveloperId, DisplayName = NavProIdentity.SetCreatorDisplayName, ToolTip = "Create reusable model-number search sets with preview and safe naming.")]
    [RibbonLayout("SearchSetCreaterRibbon.xaml")]
    [RibbonTab(RibbonTabId, DisplayName = NavProIdentity.ProductName)]
    [Command(CommandId, DisplayName = NavProIdentity.SetCreatorName, Icon = "Assets/SearchSetCreater16.png", LargeIcon = "Assets/SearchSetCreater32.png", ToolTip = "Scan model properties and create previewed search sets.", CallCanExecute = CallCanExecute.Always)]
    [Command(ClashMatrixCommandId, DisplayName = NavProIdentity.ClashMatrixDisplayName, Icon = "Assets/ClashMatrix16.png", LargeIcon = "Assets/ClashMatrix32.png", ToolTip = "Design and create Clash Detective tests from saved sets.", CallCanExecute = CallCanExecute.Always)]
    [Command(ClashApproverCommandId, DisplayName = NavProIdentity.ClashApproverDisplayName, Icon = "Assets/ClashApprover16.png", LargeIcon = "Assets/ClashApprover32.png", ToolTip = "Preview and update clash result statuses in bulk.", CallCanExecute = CallCanExecute.Always)]
    [Command(ClashStatusImportCommandId, DisplayName = NavProIdentity.ClashStatusImportDisplayName, Icon = "Assets/ClashStatusImport16.png", LargeIcon = "Assets/ClashStatusImport32.png", ToolTip = "Import selected clash statuses from a NavPro Dashboard XML or CSV report.", CallCanExecute = CallCanExecute.Always)]
    [Command(ClashRefreshModelsCommandId, DisplayName = NavProIdentity.ClashRefreshModelsDisplayName, Icon = "Assets/ClashRefreshModels16.png", LargeIcon = "Assets/ClashRefreshModels32.png", ToolTip = "Refresh changed model files already referenced by the open document.", CallCanExecute = CallCanExecute.Always)]
    [Command(ClashColorizerCommandId, DisplayName = NavProIdentity.ClashColorizerDisplayName, Icon = "Assets/ClashColorizer16.png", LargeIcon = "Assets/ClashColorizer32.png", ToolTip = "Apply random or spreadsheet-defined colors to loaded models or saved selection/search sets.", CallCanExecute = CallCanExecute.Always)]
    [Command(ClashToleranceCommandId, DisplayName = NavProIdentity.ClashToleranceDisplayName, Icon = "Assets/ClashTolerance16.png", LargeIcon = "Assets/ClashTolerance32.png", ToolTip = "Preview and update tolerance and type across selected clash tests.", CallCanExecute = CallCanExecute.Always)]
    [Command(ClashRenamerCommandId, DisplayName = NavProIdentity.ClashRenamerDisplayName, Icon = "Assets/ClashRenamer16.png", LargeIcon = "Assets/ClashRenamer32.png", ToolTip = "Find, bulk edit and prefix Clash Detective test names.", CallCanExecute = CallCanExecute.Always)]
    [Command(ContactInfoCommandId, DisplayName = NavProIdentity.ContactInfoDisplayName, Icon = "Assets/ContactInfo16.png", LargeIcon = "Assets/ContactInfo32.png", ToolTip = "View NavPro technical support contact details.", CallCanExecute = CallCanExecute.Always)]
    [Command(ClashDashboardCommandId, DisplayName = NavProIdentity.ClashDashboardDisplayName, Icon = "Assets/ClashDashboard16.png", LargeIcon = "Assets/ClashDashboard32.png", ToolTip = "Export saved Clash Detective tests and results to an interactive dashboard.", CallCanExecute = CallCanExecute.Always)]
    [Command(NavProIdentity.SmartClashGrouperName, DisplayName = NavProIdentity.SmartClashGrouperDisplayName, Icon = "Assets/SmartClashGrouper16.png", LargeIcon = "Assets/SmartClashGrouper32.png", ToolTip = "Open SmartClashGrouper with preview and explicit review controls.", CallCanExecute = CallCanExecute.Always)]
    [Command(NavProIdentity.ChainageClashLocatorName, DisplayName = NavProIdentity.ChainageClashLocatorDisplayName, Icon = "Assets/ChainageClashLocator16.png", LargeIcon = "Assets/ChainageClashLocator32.png", ToolTip = "Open ChainageClashLocator with preview and explicit review controls.", CallCanExecute = CallCanExecute.Always)]
    [Command(NavProIdentity.AiClashDescriptionName, DisplayName = NavProIdentity.AiClashDescriptionDisplayName, Icon = "Assets/AiClashDescription16.png", LargeIcon = "Assets/AiClashDescription32.png", ToolTip = "Open AiClashDescription with preview and explicit review controls.", CallCanExecute = CallCanExecute.Always)]
    [Command(NavProIdentity.UtilitySeparationCheckerName, DisplayName = NavProIdentity.UtilitySeparationCheckerDisplayName, Icon = "Assets/UtilitySeparationChecker16.png", LargeIcon = "Assets/UtilitySeparationChecker32.png", ToolTip = "Open UtilitySeparationChecker with preview and explicit review controls.", CallCanExecute = CallCanExecute.Always)]
    [Command(NavProIdentity.MaintenanceClearanceReviewName, DisplayName = NavProIdentity.MaintenanceClearanceReviewDisplayName, Icon = "Assets/MaintenanceClearanceReview16.png", LargeIcon = "Assets/MaintenanceClearanceReview32.png", ToolTip = "Open MaintenanceClearanceReview with preview and explicit review controls.", CallCanExecute = CallCanExecute.Always)]
    [Command(NavProIdentity.CoordinationChangeLogName, DisplayName = NavProIdentity.CoordinationChangeLogDisplayName, Icon = "Assets/CoordinationChangeLog16.png", LargeIcon = "Assets/CoordinationChangeLog32.png", ToolTip = "Open CoordinationChangeLog with preview and explicit review controls.", CallCanExecute = CallCanExecute.Always)]
    [Command(NavProIdentity.SmartReviewMenuName, DisplayName = NavProIdentity.SmartReviewMenuDisplayName, Icon = "Assets/SmartReviewMenu16.png", LargeIcon = "Assets/SmartReviewMenu32.png", ToolTip = "Choose a Smart Review tool.", CallCanExecute = CallCanExecute.Always)]
    [Command(NavProIdentity.VerticalClearanceCheckerName, DisplayName = NavProIdentity.VerticalClearanceCheckerDisplayName, Icon = "Assets/VerticalClearanceChecker16.png", LargeIcon = "Assets/VerticalClearanceChecker32.png", ToolTip = "Check vertical cover from a ground/top surface to the outer pipe mesh at matching XY.", CallCanExecute = CallCanExecute.Always)]
    public sealed class SearchSetCreaterPlugin : CommandHandlerPlugin
    {
        private static readonly HashSet<string> ReviewToolIds = new HashSet<string>(StringComparer.Ordinal) {
            NavProIdentity.SmartClashGrouperName, NavProIdentity.ChainageClashLocatorName, NavProIdentity.AiClashDescriptionName,
            NavProIdentity.UtilitySeparationCheckerName, NavProIdentity.MaintenanceClearanceReviewName, NavProIdentity.CoordinationChangeLogName, NavProIdentity.VerticalClearanceCheckerName };
        private static readonly Dictionary<string, Form> ReviewWindows = new Dictionary<string, Form>(StringComparer.Ordinal);
        private const string RibbonTabId = "NavProTab";
        private const string CommandId = NavProIdentity.SearchSetCreaterName;
        private const string ClashMatrixCommandId = NavProIdentity.ClashMatrixName;
        private const string ClashApproverCommandId = NavProIdentity.ClashApproverName;
        private const string ClashStatusImportCommandId = NavProIdentity.ClashStatusImportName;
        private const string ClashRefreshModelsCommandId = NavProIdentity.ClashRefreshModelsName;
        private const string ClashColorizerCommandId = NavProIdentity.ClashColorizerName;
        private const string ClashToleranceCommandId = NavProIdentity.ClashToleranceName;
        private const string ClashRenamerCommandId = NavProIdentity.ClashRenamerName;
        private const string ContactInfoCommandId = NavProIdentity.ContactInfoName;
        private const string ClashDashboardCommandId = NavProIdentity.ClashDashboardName;
        private static SearchSetCreaterDialog dialog;
        private static ClashMatrixDialog clashDialog;
        private static ClashApproverDialog approverDialog;
        private static ClashStatusImportDialog statusImportDialog;
        private static ClashToleranceDialog toleranceDialog;
        private static ClashRenamerDialog renamerDialog;
        private static ClashDashboardDialog dashboardDialog;
        private static ColorizerDialog colorizerDialog;

        public override CommandState CanExecuteCommand(string name)
        {
            if (string.Equals(name, NavProIdentity.SmartReviewMenuName, StringComparison.Ordinal))
                return new CommandState { IsEnabled = true, IsVisible = true, IsChecked = false };
            var state = new CommandState
            {
                IsEnabled = ReviewToolIds.Contains(name) || string.Equals(name, CommandId, StringComparison.Ordinal) || string.Equals(name, ClashMatrixCommandId, StringComparison.Ordinal) || string.Equals(name, ClashApproverCommandId, StringComparison.Ordinal) || string.Equals(name, ClashStatusImportCommandId, StringComparison.Ordinal) || string.Equals(name, ClashRefreshModelsCommandId, StringComparison.Ordinal) || string.Equals(name, ClashColorizerCommandId, StringComparison.Ordinal) || string.Equals(name, ClashRenamerCommandId, StringComparison.Ordinal) || string.Equals(name, ClashToleranceCommandId, StringComparison.Ordinal) || string.Equals(name, ContactInfoCommandId, StringComparison.Ordinal) || string.Equals(name, ClashDashboardCommandId, StringComparison.Ordinal),
                IsVisible = ReviewToolIds.Contains(name) || string.Equals(name, CommandId, StringComparison.Ordinal) || string.Equals(name, ClashMatrixCommandId, StringComparison.Ordinal) || string.Equals(name, ClashApproverCommandId, StringComparison.Ordinal) || string.Equals(name, ClashStatusImportCommandId, StringComparison.Ordinal) || string.Equals(name, ClashRefreshModelsCommandId, StringComparison.Ordinal) || string.Equals(name, ClashColorizerCommandId, StringComparison.Ordinal) || string.Equals(name, ClashRenamerCommandId, StringComparison.Ordinal) || string.Equals(name, ClashToleranceCommandId, StringComparison.Ordinal) || string.Equals(name, ContactInfoCommandId, StringComparison.Ordinal) || string.Equals(name, ClashDashboardCommandId, StringComparison.Ordinal),
                IsChecked = false
            };
            return state;
        }

        public override int ExecuteCommand(string name, params string[] parameters)
        {
            // Menu-only header has no model operation; its children retain their command IDs.
            if (string.Equals(name, NavProIdentity.SmartReviewMenuName, StringComparison.Ordinal)) return 0;
            if (ReviewToolIds.Contains(name)) return ExecuteReviewTool(name);
            if (string.Equals(name, ContactInfoCommandId, StringComparison.Ordinal))
            {
                using (var contact = new ContactInfoDialog()) contact.ShowDialog(NavApplication.Gui.MainWindow);
                return 0;
            }
            if (string.Equals(name, ClashMatrixCommandId, StringComparison.Ordinal)) return ExecuteClashMatrix();
            if (string.Equals(name, ClashApproverCommandId, StringComparison.Ordinal)) return ExecuteClashApprover();
            if (string.Equals(name, ClashStatusImportCommandId, StringComparison.Ordinal)) return ExecuteClashStatusImport();
            if (string.Equals(name, ClashRefreshModelsCommandId, StringComparison.Ordinal)) return ExecuteClashRefreshModels();
            if (string.Equals(name, ClashColorizerCommandId, StringComparison.Ordinal)) return ExecuteColorizer();
            if (string.Equals(name, ClashRenamerCommandId, StringComparison.Ordinal)) return ExecuteClashRenamer();
            if (string.Equals(name, ClashToleranceCommandId, StringComparison.Ordinal)) return ExecuteClashTolerance();
            if (string.Equals(name, ClashDashboardCommandId, StringComparison.Ordinal)) return ExecuteClashDashboard();
            if (!string.Equals(name, CommandId, StringComparison.Ordinal)) return -1;
            if (dialog != null && !dialog.IsDisposed)
            {
                if (dialog.WindowState == FormWindowState.Minimized) dialog.WindowState = FormWindowState.Normal;
                dialog.Activate();
                return 0;
            }
            try
            {
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0)
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Open or append a model before using SetCreator.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                // Modeless dialog lets users keep Navisworks interactive; service guards invalidate stale work after model/document changes.
                dialog = new SearchSetCreaterDialog(document);
                dialog.FormClosed += (s, e) => dialog = null;
                dialog.Show(NavApplication.Gui.MainWindow);
                return 0;
            }
            catch (Exception ex)
            {
                var failedDialog = dialog;
                dialog = null;
                if (failedDialog != null && !failedDialog.IsDisposed) failedDialog.Dispose();
                MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro - SetCreator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static int ExecuteClashMatrix()
        {
            if (clashDialog != null && !clashDialog.IsDisposed)
            {
                if (clashDialog.WindowState == FormWindowState.Minimized) clashDialog.WindowState = FormWindowState.Normal;
                clashDialog.Activate(); return 0;
            }
            try
            {
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0)
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Open or append models before using Clash Matrix.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                if (!(document.Clash is DocumentClash))
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Clash Detective is unavailable. This tool requires Navisworks Manage.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                clashDialog = new ClashMatrixDialog(document);
                clashDialog.FormClosed += (s, e) => clashDialog = null;
                clashDialog.Show(NavApplication.Gui.MainWindow);
                return 0;
            }
            catch (Exception ex)
            {
                var failedDialog = clashDialog; clashDialog = null;
                if (failedDialog != null && !failedDialog.IsDisposed) failedDialog.Dispose();
                MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro - Clash Matrix", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static int ExecuteClashApprover()
        {
            if (approverDialog != null && !approverDialog.IsDisposed)
            {
                if (approverDialog.WindowState == FormWindowState.Minimized) approverDialog.WindowState = FormWindowState.Normal;
                approverDialog.Activate(); return 0;
            }
            try
            {
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0)
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Open a model before using Status Updater.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                if (!(document.Clash is DocumentClash))
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Clash Detective is unavailable. Status Updater requires Navisworks Manage.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                approverDialog = new ClashApproverDialog(document);
                approverDialog.FormClosed += (s, e) => approverDialog = null;
                approverDialog.Show(NavApplication.Gui.MainWindow);
                return 0;
            }
            catch (Exception ex)
            {
                var failedDialog = approverDialog; approverDialog = null;
                if (failedDialog != null && !failedDialog.IsDisposed) failedDialog.Dispose();
                MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro - Status Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static int ExecuteClashStatusImport()
        {
            if (statusImportDialog != null && !statusImportDialog.IsDisposed)
            {
                if (statusImportDialog.WindowState == FormWindowState.Minimized) statusImportDialog.WindowState = FormWindowState.Normal;
                statusImportDialog.Activate(); return 0;
            }
            try
            {
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0)
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Open a model before importing Clash statuses.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                if (!(document.Clash is DocumentClash))
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Clash Detective is unavailable. Import Clash Status requires Navisworks Manage.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                statusImportDialog = new ClashStatusImportDialog(document);
                statusImportDialog.FormClosed += (s, e) => statusImportDialog = null;
                statusImportDialog.Show(NavApplication.Gui.MainWindow);
                return 0;
            }
            catch (Exception ex)
            {
                var failed = statusImportDialog; statusImportDialog = null;
                if (failed != null && !failed.IsDisposed) failed.Dispose();
                MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro - Import Clash Status", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static int ExecuteClashRefreshModels()
        {
            var document = NavApplication.ActiveDocument;
            if (document == null || document.Models.Count == 0)
            {
                MessageBox.Show(NavApplication.Gui.MainWindow, "Open or append models before refreshing model files.", "NavPro - Refresh Models", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            var message = "Refresh changed model files already referenced by this document from their existing file paths?\r\n\r\n" +
                "This does not append newly added files or update renamed/moved file paths. The refresh runs synchronously and Navisworks may be busy while it reloads files.\r\n\r\n" +
                "Afterward, review the model, rerun Clash Detective tests if needed, and save the NWF. NavPro will not run clashes or save the document automatically.";
            if (document.IsModified)
                message += "\r\n\r\nThe current NWF has unsaved changes. Save it first if you need to preserve those changes.";
            message += "\r\n\r\nContinue?";

            if (MessageBox.Show(NavApplication.Gui.MainWindow, message, "NavPro - Refresh Models", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return 0;

            try
            {
                var refresher = new ClashModelRefreshService(document);
                string before = refresher.Inspect();
                var updated = refresher.RefreshChangedFiles();
                var result = updated
                    ? "Navisworks refreshed one or more loaded files that had changed on disk. Review the updated model and rerun Clash Detective tests if needed, then save the NWF."
                    : "No loaded files needed refreshing. Navisworks did not detect changed files among this document's current references.";
                CoordinationChangeLog.Record(document, "Refresh Models", document.FileName, before, updated ? "Native files refreshed; clash results potentially outdated" : "No native file changes detected");
                using (var summary = new Form { Name = "RefreshModelsSummary", Text = "NavPro | Refresh Models summary", Width = 1050, Height = 700, StartPosition = FormStartPosition.CenterParent })
                { summary.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = result + "\r\n\r\n" + refresher.Inspect(updated) }); NavPro.Frontend.Ui.DialogTheme.Apply(summary); summary.ShowDialog(NavApplication.Gui.MainWindow); }
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(NavApplication.Gui.MainWindow, "Navisworks could not refresh the loaded model files.\r\n\r\n" + ex.Message, "NavPro - Refresh Models", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static int ExecuteColorizer()
        {
            if (colorizerDialog != null && !colorizerDialog.IsDisposed)
            {
                if (colorizerDialog.WindowState == FormWindowState.Minimized) colorizerDialog.WindowState = FormWindowState.Normal;
                colorizerDialog.Activate(); return 0;
            }
            try
            {
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0)
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Open or append a model before using Colorizer.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                colorizerDialog = new ColorizerDialog(document);
                colorizerDialog.FormClosed += (s, e) => colorizerDialog = null;
                colorizerDialog.Show(NavApplication.Gui.MainWindow);
                return 0;
            }
            catch (Exception ex)
            {
                var failed = colorizerDialog; colorizerDialog = null;
                if (failed != null && !failed.IsDisposed) failed.Dispose();
                MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro - Colorizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static int ExecuteClashDashboard()
        {
            if (dashboardDialog != null && !dashboardDialog.IsDisposed)
            {
                if (dashboardDialog.WindowState == FormWindowState.Minimized) dashboardDialog.WindowState = FormWindowState.Normal;
                dashboardDialog.Activate(); return 0;
            }
            try
            {
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0)
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Open a model before exporting the Clash Dashboard.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                if (!(document.Clash is DocumentClash))
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Clash Detective is unavailable. Clash Dashboard requires Navisworks Manage.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                dashboardDialog = new ClashDashboardDialog(document);
                dashboardDialog.FormClosed += (s, e) => dashboardDialog = null;
                dashboardDialog.Show(NavApplication.Gui.MainWindow);
                return 0;
            }
            catch (Exception ex)
            {
                var failed = dashboardDialog; dashboardDialog = null;
                if (failed != null && !failed.IsDisposed) failed.Dispose();
                MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro - Clash Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static int ExecuteReviewTool(string name)
        {
            try
            {
                if (ReviewWindows.TryGetValue(name, out var current) && !current.IsDisposed)
                { if (current.WindowState == FormWindowState.Minimized) current.WindowState = FormWindowState.Normal; current.Activate(); return 0; }
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0 || !(document.Clash is DocumentClash))
                { MessageBox.Show(NavApplication.Gui.MainWindow, "Open a model in Navisworks Manage before using this tool.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information); return 0; }
                Form window;
                switch (name)
                {
                    case NavProIdentity.SmartClashGrouperName: window = new SmartClashGrouperDialog(document); break;
                    case NavProIdentity.ChainageClashLocatorName: window = new ChainageClashLocatorDialog(document); break;
                    case NavProIdentity.AiClashDescriptionName: window = new ClashDescriptionDialog(document); break;
                    case NavProIdentity.UtilitySeparationCheckerName: window = new SpatialCheckDialog(document, false); break;
                    case NavProIdentity.MaintenanceClearanceReviewName: window = new SpatialCheckDialog(document, true); break;
                    case NavProIdentity.CoordinationChangeLogName: window = new CoordinationChangeLogDialog(document); break;
                    case NavProIdentity.VerticalClearanceCheckerName: window = new VerticalClearanceDialog(document); break;
                    default: return -1;
                }
                ReviewWindows[name] = window;
                window.Disposed += (sender, args) => { if (ReviewWindows.TryGetValue(name, out var stored) && ReferenceEquals(stored, window)) ReviewWindows.Remove(name); };
                try { window.Show(NavApplication.Gui.MainWindow); } catch { window.Dispose(); throw; }
                return 0;
            }
            catch (Exception ex)
            { MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Error); return -1; }
        }
        private static int ExecuteClashTolerance()
        {
            if (toleranceDialog != null && !toleranceDialog.IsDisposed)
            {
                if (toleranceDialog.WindowState == FormWindowState.Minimized) toleranceDialog.WindowState = FormWindowState.Normal;
                toleranceDialog.Activate(); return 0;
            }
            try
            {
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0)
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Open a model before using Tolerance Updater.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                if (!(document.Clash is DocumentClash))
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Clash Detective is unavailable. Tolerance Updater requires Navisworks Manage.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                toleranceDialog = new ClashToleranceDialog(document);
                toleranceDialog.FormClosed += (s, e) => toleranceDialog = null;
                toleranceDialog.Show(NavApplication.Gui.MainWindow);
                return 0;
            }
            catch (Exception ex)
            {
                var failed = toleranceDialog; toleranceDialog = null;
                if (failed != null && !failed.IsDisposed) failed.Dispose();
                MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro - Tolerance Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        private static int ExecuteClashRenamer()
        {
            if (renamerDialog != null && !renamerDialog.IsDisposed)
            {
                if (renamerDialog.WindowState == FormWindowState.Minimized) renamerDialog.WindowState = FormWindowState.Normal;
                renamerDialog.Activate(); return 0;
            }
            try
            {
                var document = NavApplication.ActiveDocument;
                if (document == null || document.Models.Count == 0)
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Open a model before using Renamer.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                if (!(document.Clash is DocumentClash))
                {
                    MessageBox.Show(NavApplication.Gui.MainWindow, "Clash Detective is unavailable. Renamer requires Navisworks Manage.", "NavPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                renamerDialog = new ClashRenamerDialog(document);
                renamerDialog.FormClosed += (s, e) => renamerDialog = null;
                renamerDialog.Show(NavApplication.Gui.MainWindow);
                return 0;
            }
            catch (Exception ex)
            {
                var failed = renamerDialog; renamerDialog = null;
                if (failed != null && !failed.IsDisposed) failed.Dispose();
                MessageBox.Show(NavApplication.Gui.MainWindow, ex.Message, "NavPro - Renamer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }
    }
}
