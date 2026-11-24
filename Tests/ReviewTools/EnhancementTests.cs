using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
internal static partial class Program
{
    private static void Enhancements()
    {
        var row = new ReviewClash { ItemA = "Pipe {Level}", ItemB = "Beam" };
        row.PropertiesA["Element/System Name"] = "Water"; row.PropertiesB["Element/System"] = "Drain";
        row.PropertiesA["Element/Level"] = "L01";
        string draft = ProjectDescriptionTemplate.Render(row, "{SystemA} / {SystemB}: {Level}; {Chainage}", out string missing);
        Check(draft.Contains("Water / Drain: L01") && missing == "Chainage" && draft.Contains("[MISSING Chainage]"), "templates flag absent chainage without inference");
        row.PropertiesB["Element/Level"] = "L02";
        ProjectDescriptionTemplate.Render(row, "{Level}", out missing); Check(missing == "Level", "conflicting levels are flagged");
        Check(ProjectDescriptionTemplate.Render(row, "{ItemA}", out missing) == "Pipe {Level}", "model text is never recursively expanded");
        Reject(() => ProjectDescriptionTemplate.Render(row, "{Unknown}", out _), "unknown template fields rejected");
        Reject(() => ProjectDescriptionTemplate.Render(row, "{Level", out _), "unbalanced template braces rejected");
        Near(AlignmentCoordinates.Transform(10, 0, 2, 90, 100, 200).E, 100, "transform rotation and translation east");
        Near(AlignmentCoordinates.Transform(10, 0, 2, 90, 100, 200).N, 220, "transform scale before rotation north");
        Reject(() => AlignmentCoordinates.Transform(0, 0, 0, 0, 0, 0), "zero transform scale rejected");
        Check(AlignmentCoordinates.Station(1250) == "1+250", "station formatting 1+250");
        Check(AlignmentCoordinates.Station(999.9996) == "1+000", "station rounding carries correctly");
        Check(AlignmentCoordinates.Station(-5.25) == "-0+005.25", "negative stations format correctly");
        var alignment = new HorizontalAlignment { Name = "Equation", Units = "meter", MetersPerUnit = 1 };
        alignment.Segments.Add(HorizontalSegment.Line(new AlignmentPoint(), new AlignmentPoint(2000, 0), 0));
        alignment.Equations.Add(new StationEquation { Internal = 1000, Back = 1000, Ahead = 1250 }); alignment.Validate();
        Near(alignment.DisplayStation(999), 999, "station before equation"); Near(alignment.DisplayStation(1000), 1250, "ahead station at equation"); Near(alignment.DisplayStation(1100), 1350, "station after equation");
        alignment.Equations[0].Back = 900; Reject(alignment.Validate, "inconsistent station equation rejected");
        var snapshot = new ReviewSnapshot { Units = "Meters" };
        for (int i = 0; i < 3; i++) snapshot.Clashes.Add(new ReviewClash { TestId = "T", TestName = "Test", Id = i.ToString(), Name = "C" + i, X = i * .9, Status = "New" });
        var plan = SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.DistanceCluster, "", 1, "Test", false);
        Check(plan.Groups.Count == 1 && plan.GroupedResults == 3 && plan.Groups[0].Reasons.Count == 3, "connected radius grouping crosses cell boundaries and explains every member");
        snapshot.Clashes[1].ParentGroup = "Existing";
        Check(SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.DistanceCluster, "", 1, "Test", false).Groups.Count == 0, "existing grouped bridge does not join clusters");
        snapshot.Clashes[1].ParentGroup = ""; snapshot.Clashes[2].TestId = "Other";
        Check(SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.DistanceCluster, "", 1, "Test", false).GroupedResults == 2, "distance groups do not cross test boundaries");
        foreach (var r in snapshot.Clashes) r.PropertiesA["Item/Level"] = "L1";
        Check(SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.Level, "", 1, "Test", false).GroupedResults == 2, "level grouping uses actual metadata");
        snapshot.Clashes[1].PropertiesB["Item/Level"] = "L2";
        Check(SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.Level, "", 1, "Test", false).Groups.Count == 0, "level conflict does not invent membership");
        var first = new Document { FileName = Path.Combine(Path.GetTempPath(), "NavPro-history-fixture.nwd") };
        CoordinationChangeLog.Record(first, "Fixture", "Scope", "Before", "After", "Incomplete / possible partial results");
        var second = new Document { FileName = first.FileName };
        Check(CoordinationChangeLog.Read(second).Single().Outcome.Contains("Incomplete"), "history and partial outcome survive document instance changes");
        CoordinationChangeLog.CaptureRevision(first, "R1", new Dictionary<string, string> { { "old", "Existing" }, { "same", "Before" } });
        CoordinationChangeLog.CaptureRevision(second, "R2", new Dictionary<string, string> { { "new", "Added" }, { "same", "After" } });
        string comparison = CoordinationChangeLog.CompareLatest(first);
        Check(comparison.Contains("REMOVED: Existing") && comparison.Contains("ADDED: Added") && comparison.Contains("CHANGED: Before → After"), "persisted revisions identify additions, removals and field changes");
        Reject(() => CoordinationChangeLog.CaptureRevision(first, "R2", new Dictionary<string, string>()), "duplicate revision cannot overwrite history");
        var largeHistory = new CoordinationHistory();
        largeHistory.Revisions.Add(new CoordinationRevision { Name = "Large federation", Results = new Dictionary<string, string> { { "result", new string('x', 9000000) } } });
        ReviewPreferences.Write("history-large-fixture", largeHistory);
        Check(ReviewPreferences.Read<CoordinationHistory>("history-large-fixture").Revisions.Single().Results["result"].Length == 9000000, "history snapshots larger than 8 MB survive persistence");
        ReviewPreferences.Write("unicode-limit-fixture", new Dictionary<string, string> { { "value", "retained" } });
        bool byteLimitRejected = false;
        try { ReviewPreferences.Write("unicode-limit-fixture", new Dictionary<string, string> { { "value", new string('\u754c', 3000000) } }); }
        catch (InvalidDataException) { byteLimitRejected = true; }
        Check(byteLimitRejected, "preset size limit measures UTF-8 bytes as well as serializer characters");
        Check(ReviewPreferences.Read<Dictionary<string, string>>("unicode-limit-fixture")["value"] == "retained", "oversize write preserves prior readable data");
        string path = Path.Combine(Path.GetTempPath(), "NavPro-rule-fixture-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            string text = ApprovedRuleTable.Header + "\nWater,Water set,Drain set,0.3,Meters,Project STD,R2,Yes,3D\n"; File.WriteAllText(path, text);
            Check(ApprovedRuleTable.Read(path).Single().Revision == "R2", "approved CSV rules retain source revision");
            foreach (string invalid in new[] { text.Replace(",Yes,", ",No,"), text.Replace(",3D", ",Horizontal"), text.Replace(",0.3,", ",NaN,"), text.Replace(",R2,", ",,") })
            { File.WriteAllText(path, invalid); bool rejected = false; try { ApprovedRuleTable.Read(path); } catch (InvalidDataException) { rejected = true; } Check(rejected, "unapproved, unsupported, nonfinite or unattributed rule rejected"); }
        }
        finally { File.Delete(path); }
        WorkbookRules();
        ProtectedDrafts();
    }
    private static void WorkbookRules()
    {
        string path = Path.Combine(Path.GetTempPath(), "NavPro-rules-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            foreach (bool formula in new[] { false, true })
            {
                using (var stream = File.Create(path)) using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
                {
                    Action<string, string> part = (name, xml) => { using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write(xml); };
                    part("xl/workbook.xml", "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Rules' sheetId='1' r:id='rId1'/></sheets></workbook>");
                    part("xl/_rels/workbook.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Target='worksheets/sheet1.xml'/></Relationships>");
                    string[][] values = { ApprovedRuleTable.Header.Split(','), new[] { "Water", "A", "B", "0.3", "Meters", "Project rule", "R3", "Yes", "3D" } };
                    string rows = string.Concat(values.Select((row, i) => "<row r='" + (i + 1) + "'>" + string.Concat(row.Select((value, c) => "<c r='" + (char)('A' + c) + (i + 1) + "' t='inlineStr'>" + (formula && i == 1 && c == 3 ? "<f>0.1+0.2</f>" : "") + "<is><t>" + value + "</t></is></c>")) + "</row>"));
                    part("xl/worksheets/sheet1.xml", "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData>" + rows + "</sheetData></worksheet>");
                }
                if (!formula) Check(ApprovedRuleTable.Read(path).Single().Revision == "R3", "approved Excel rule worksheet imports literal source/revision");
                else { bool rejected = false; try { ApprovedRuleTable.Read(path); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Excel formulas are rejected rather than trusting cached rule values"); }
            }
        }
        finally { File.Delete(path); }
    }
    private static void ProtectedDrafts()
    {
        var document = Fixture();
        using (var dialog = new NavPro.Frontend.Dialogs.ClashDescriptionDialog(new ClashReviewSession(document)))
        {
            dialog.ShowInTaskbar = false; dialog.StartPosition = System.Windows.Forms.FormStartPosition.Manual; dialog.Location = new System.Drawing.Point(-20000, -20000); dialog.Show();
            var controls = Descendants(dialog).ToList();
            controls.OfType<System.Windows.Forms.Button>().Single(b => b.Text == "Load clashes").PerformClick();
            var grid = controls.OfType<System.Windows.Forms.DataGridView>().Single();
            Check(grid.Rows.Count > 1, "description fixture loads actual detached clashes");
            grid.Rows[0].Cells[4].Value = "My manual coordination note";
            var generate = controls.OfType<System.Windows.Forms.Button>().Single(b => b.Text == "Generate checked drafts"); generate.PerformClick();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!generate.Enabled && timer.ElapsedMilliseconds < 5000) { System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(5); }
            Check(Convert.ToString(grid.Rows[0].Cells[4].Value) == "My manual coordination note", "regeneration protects manually edited drafts");
            Check(!string.IsNullOrWhiteSpace(Convert.ToString(grid.Rows[1].Cells[4].Value)), "regeneration still fills unedited rows");
            Check(document.Transactions == 0, "draft generation performs no native model transaction"); dialog.Close();
        }
    }
}
