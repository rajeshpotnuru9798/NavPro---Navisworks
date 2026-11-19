using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Dialogs;
internal static class ToleranceTests
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS Tolerance: " + name); }
    private static void Reject(System.Action action, string name)
    { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, name); }
    public static void Run()
    {
        var doc = new Document(); Autodesk.Navisworks.Api.Application.ActiveDocument = doc;
        var data = ((DocumentClash)doc.Clash).TestsData;
        var a = new ClashTest { DisplayName = "A", TestType = ClashTestType.Hard, Tolerance = .01 };
        var b = new ClashTest { DisplayName = "B", TestType = ClashTestType.Clearance, Tolerance = .02 };
        var c = new ClashTest { DisplayName = "C", TestType = ClashTestType.Duplicate, Tolerance = .04 };
        var group = new GroupItem(); group.Children.Add(b); data.Tests.Add(a); data.Tests.Add(group); data.Tests.Add(c);
        a.Children.Add(new SavedItem { DisplayName = "Existing result" });
        var service = new ClashToleranceService(doc);
        Check(service.ReadTests().Count == 3, "discovers grouped tests without counting results");
        var selected = service.ReadTests().Take(2).ToList();
        Check(service.RunTests(selected.Take(1)) == 1 && data.RunCount == 1 && data.RunNames.Single() == selected[0].Name,
            "reruns only explicitly selected test by current GUID");
        Reject(() => service.RunTests(new ClashToleranceItem[0]), "rejects empty rerun selection");
        Reject(() => service.RunTests(new[] { selected[0], selected[0] }), "rejects duplicate rerun selection");
        Reject(() => service.RunTests(new[] { new ClashToleranceItem(Guid.NewGuid().ToString("D"), "Deleted", ClashTestType.Hard, 0, Units.Meters) }),
            "prevalidates missing test before rerun");
        var plan = service.Preview(selected, 30, Units.Millimeters, null);
        Check(Math.Abs(plan[0].Tolerance - .03) < 1e-12 && plan[1].Type == ClashTestType.Clearance && data.EditCount == 0, "preview converts units and preserves mixed types without writes");
        Check(service.Apply(plan) == 2 && data.EditCount == 2 && a.Children.Count == 1 && a.Rules == "Preserved rules" && c.Tolerance == .04, "edits selected copies only; leaves unchecked test alone (adapter)");
        foreach (var type in new[] { ClashTestType.Hard, ClashTestType.HardConservative, ClashTestType.Clearance, ClashTestType.Duplicate })
        { var next = service.Preview(service.ReadTests().Take(1), 0, Units.Meters, type); service.Apply(next); Check(a.TestType == type && a.Tolerance == 0, "supports " + type + " and zero tolerance"); }
        var unchanged = service.Preview(service.ReadTests().Take(1), 0, Units.Meters, null);
        int writes = data.EditCount, transactions = doc.Transactions;
        Check(service.Apply(unchanged) == 0 && data.EditCount == writes && doc.Transactions == transactions, "unchanged batch performs no writes");
        foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity }) Reject(() => service.Preview(selected, invalid, Units.Meters, null), "rejects invalid tolerance " + invalid);
        Reject(() => service.Preview(new ClashToleranceItem[0], 1, Units.Meters, null), "rejects empty selection");
        Reject(() => service.Preview(new[] { selected[0], selected[0] }, 1, Units.Meters, null), "rejects duplicate selection");
        Reject(() => service.Preview(service.ReadTests(), 1, Units.Meters, ClashTestType.Custom), "rejects unsupported replacement type");
        plan = service.Preview(service.ReadTests().Take(2), 1, Units.Meters, null); b.Tolerance = .5;
        Reject(() => service.Apply(plan), "rejects stale batch before editing any test"); Check(data.EditCount == writes, "stale second test causes zero partial writes");
        plan = service.Preview(service.ReadTests(), 1, Units.Meters, null); doc.Units = Units.Feet;
        Reject(() => service.Apply(plan), "rejects document unit change"); doc.Units = Units.Meters;
        plan = service.Preview(service.ReadTests(), 1, Units.Meters, null); group.Children.Clear();
        Reject(() => service.Apply(plan), "rejects removed selected test"); group.Children.Add(b);
        Autodesk.Navisworks.Api.Application.ActiveDocument = new Document();
        Reject(() => service.ReadTests(), "rejects switched document"); Autodesk.Navisworks.Api.Application.ActiveDocument = doc;
        Exception threadError = null; var thread = new Thread(() => { try { service.ReadTests(); } catch (Exception ex) { threadError = ex; } }); thread.Start(); thread.Join();
        Check(threadError is InvalidOperationException, "rejects background API access");
        CheckUiPreview(service, doc.Units);
        RenderDialog();
    }
    private static void CheckUiPreview(ClashToleranceService service, Units units)
    {
        using (var form = new ClashToleranceDialog(service, units))
        {
            var tolerance = Field<NumericUpDown>(form, "tolerance"); tolerance.Value = .07M;
            var preview = Descendants(form).OfType<Button>().Single(button => button.Text == "Preview changes");
            var grid = Field<DataGridView>(form, "grid"); var apply = Field<Button>(form, "apply");
            var rerun = Field<Button>(form, "rerun");
            Check(rerun.Text == "Re-Run Clashes" && rerun.Enabled, "shows rerun action for checked tests");
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show();
            preview.PerformClick(); Check(apply.Enabled && grid.Rows[0].Cells[8].Value.ToString() == "Update", "dialog previews checked batch");
            Field<ComboBox>(form, "type").SelectedIndex = 2; Check(!apply.Enabled, "type change invalidates preview");
            preview.PerformClick(); grid.Rows[0].Cells[0].Value = false; Check(!apply.Enabled, "test selection invalidates preview");
            Descendants(form).OfType<Button>().Single(button => button.Text == "Select none").PerformClick();
            preview.PerformClick(); Check(!apply.Enabled && Field<Label>(form, "status").Text.Contains("Select at least one"), "empty selection has actionable feedback");
            Descendants(form).OfType<Button>().Single(button => button.Text == "Close").PerformClick(); Check(form.IsDisposed, "Close button closes modeless window");
        }
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    private static void RenderDialog()
    {
        Directory.CreateDirectory("Tests/Artifacts");
        using (var form = new ClashToleranceDialog(null, Units.Meters))
        {
            Field<NumericUpDown>(form, "tolerance").Value = .03M;
            var grid = Field<DataGridView>(form, "grid");
            grid.Rows.Add(true, "H_CI-Irrigation_VS_CS-Storm", "Hard", "0.01 Meters", "Hard", "0.03 Meters", "Disabled", "Disabled", "Update");
            Field<Label>(form, "status").Text = "1 test to update. Review proposed values.";
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
            form.Show(); form.PerformLayout();
            foreach (var size in new[] { new Size(1020, 720), form.MinimumSize })
            {
                form.Size = size; form.PerformLayout();
                foreach (var name in new[] { "tolerance", "units", "type", "apply", "rerun", "hint" })
                {
                    var control = Field<Control>(form, name);
                    var bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                    Check(form.ClientRectangle.Contains(bounds), name + " fits at " + size);
                }
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/Artifacts/ToleranceUpdater-" + size.Width + ".png"); }
            }
            var apply = Field<Button>(form, "apply"); Check(!apply.Enabled, "apply disabled before preview");
            // Changing any setting must discard the reviewed plan.
            apply.Enabled = true; Field<NumericUpDown>(form, "tolerance").Value += .01M;
            Check(!apply.Enabled && grid.Rows[0].Cells[4].Value == null, "input changes clear proposed values and disable apply");
            var check = Descendants(form).OfType<CheckBox>().Single(c => c.Text == "Pin window"); check.Checked = true; Check(form.TopMost, "pin enables stay-on-top"); check.Checked = false;
            form.Close(); Check(form.IsDisposed, "Close disposes modeless dialog");
        }
    }
    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
    { foreach (Control child in parent.Controls) { yield return child; foreach (var next in Descendants(child)) yield return next; } }
}
