using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Dialogs;
using NavPro.Frontend.Ui;
using Alignment = NavPro.Backend.Core.HorizontalAlignment;

internal static partial class Program
{
    private static int checks;
    private static void Check(bool condition, string text) { if (!condition) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
    private static void Near(double actual, double expected, string text) => Check(Math.Abs(actual - expected) < 1e-6, text);
    private static void Reject(System.Action action, string text) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, text); }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            WindowPreferences.StorageRoot = Path.Combine(Path.GetTempPath(), "NavPro-ReviewTests-" + Guid.NewGuid().ToString("N"));
            if(args.Contains("--vertical-ui")){Ui(true);return 0;}
            ReviewPreferences.Root = Path.Combine(WindowPreferences.StorageRoot, "presets"); VerticalCover(); DashboardImages(); Enhancements(); Geometry(); NativeAdapter(); SpatialChecks(); Ai().GetAwaiter().GetResult(); CsvAndJournal(); RenameAndPreferences(); Ui();
            Console.WriteLine("PASS " + checks + " Smart Review checks. API doubles and HTTP mocks only; no live Autodesk or provider calls."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Geometry()
    {
        var route = new Alignment { Name = "Eastbound", Units = "meter", MetersPerUnit = 1 };
        route.Segments.Add(HorizontalSegment.Line(new AlignmentPoint(1000, 2000), new AlignmentPoint(1100, 2000), 500));
        var location = route.Locate(1040, 1995, 50); Near(location.Station, 540, "line chainage includes nonzero start"); Near(location.Offset, 5, "right offset is positive");
        Near(route.Locate(1040, 2005, 50).Offset, -5, "left offset is negative");
        Check(route.Locate(900, 2000, 200).Note.Contains("endpoint"), "projection before start is flagged");
        Check(route.Locate(1040, 2100, 10).Note.Contains("Outside"), "out-of-range location is flagged");
        var arc = new Alignment { Name = "CCW", Units = "meter", MetersPerUnit = 1 };
        arc.Segments.Add(HorizontalSegment.Arc(new AlignmentPoint(10, 0), new AlignmentPoint(0, 10), new AlignmentPoint(0, 0), 100, false));
        location = arc.Locate(6 * Math.Sqrt(2), 6 * Math.Sqrt(2), 50);
        Near(location.Station, 100 + 2.5 * Math.PI, "circular arc projection uses exact arc length"); Near(location.Offset, 2, "CCW outside is right");
        Check(arc.Locate(0, 0, 50).Note.Contains("Ambiguous"), "arc centre ambiguity is surfaced");
        Check(arc.Segments[0].Project(new AlignmentPoint(-1, -1)).Note.Contains("Ambiguous"), "equally near endpoints on one arc are flagged");
        var clockwise = HorizontalSegment.Arc(new AlignmentPoint(10, 0), new AlignmentPoint(0, -10), new AlignmentPoint(), 0, true);
        Near(clockwise.Project(new AlignmentPoint(6 * Math.Sqrt(2), -6 * Math.Sqrt(2))).Offset, -2, "CW arc side is correct");
        var crossing = new Alignment { Name = "Crossing", Units = "meter", MetersPerUnit = 1 };
        crossing.Segments.Add(HorizontalSegment.Line(new AlignmentPoint(0, 0), new AlignmentPoint(10, 10), 0));
        crossing.Segments.Add(HorizontalSegment.Line(new AlignmentPoint(10, 10), new AlignmentPoint(0, 10), Math.Sqrt(200)));
        crossing.Segments.Add(HorizontalSegment.Line(new AlignmentPoint(0, 10), new AlignmentPoint(10, 0), Math.Sqrt(200) + 10));
        Check(crossing.Locate(5, 5, 100).Note.Contains("Ambiguous"), "self-crossing station ambiguity is surfaced");
        Reject(() => HorizontalSegment.Line(new AlignmentPoint(), new AlignmentPoint(), 0), "zero-length route rejected");
        Reject(() => HorizontalSegment.Line(new AlignmentPoint(1e300, 0), new AlignmentPoint(0, 0), 0), "numeric overflow cannot produce a valid alignment");
        Reject(() => route.Locate(double.NaN, 0, 1), "nonfinite location rejected");
        var folder = Path.Combine(Path.GetTempPath(), "NavPro-review-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string csv = Path.Combine(folder, "alignment.csv"), xml = Path.Combine(folder, "alignment.xml");
        File.WriteAllText(csv, "Alignment,Station,Easting,Northing\r\n\"A, main\",100,0,0\r\n\"A, main\",200,100,0\r\n");
        var data = AlignmentDataReader.ReadCsv(csv, "meter", 1); Check(data.Count == 1 && data[0].Name == "A, main", "quoted CSV alignment import");
        File.WriteAllText(csv, "Alignment,Station,Easting,Northing\nA,0,0,0\nA,200,100,0"); Reject(() => AlignmentDataReader.ReadCsv(csv, "meter", 1), "inconsistent CSV stationing rejected");
        string valid = "<LandXML xmlns='http://www.landxml.org/schema/LandXML-1.2'><Units><Metric linearUnit='meter'/></Units><Alignments><Alignment name='Route' staStart='10' length='100'><CoordGeom><Line length='100'><Start>2000 1000</Start><End>2000 1100</End></Line></CoordGeom></Alignment></Alignments></LandXML>";
        File.WriteAllText(xml, valid); data = AlignmentDataReader.ReadLandXml(xml); Near(data[0].Locate(1040, 2005, 50).Station, 50, "LandXML northing/easting order");
        File.WriteAllText(xml, valid.Replace("<CoordGeom>", "<StaEquation staAhead='1'/><CoordGeom>")); Reject(() => AlignmentDataReader.ReadLandXml(xml), "station equations rejected explicitly");
        File.WriteAllText(xml, valid.Replace("<Line length='100'>", "<Spiral length='100'>").Replace("</Line>", "</Spiral>")); Reject(() => AlignmentDataReader.ReadLandXml(xml), "spirals never approximated");
        File.WriteAllText(xml, valid.Replace("linearUnit='meter'", "linearUnit='unknown'")); Reject(() => AlignmentDataReader.ReadLandXml(xml), "unknown LandXML units rejected");
        File.Delete(csv); File.Delete(xml); Directory.Delete(folder);
    }
    private static Document Fixture()
    {
        var doc = new Document(); Autodesk.Navisworks.Api.Application.ActiveDocument = doc;
        var a = new ModelItem { DisplayName = "Pipe", TestPath = "0/2", Model = new Model { FileName = @"C:\private\Water.nwc" } };
        var b = new ModelItem { DisplayName = "Beam", TestPath = "0/3", Model = new Model { FileName = @"C:\private\Structure.nwc" } };
        var category = new PropertyCategory { DisplayName = "Element" }; category.Properties.Add(new DataProperty { DisplayName = "System", Value = new VariantData { Text = "Water" } }); a.PropertyCategories.Add(category);
        var test = new ClashTest { DisplayName = "Water / Structure", TestType = ClashTestType.Hard };
        for (int i = 0; i < 3; i++) test.Children.Add(new ClashResult { DisplayName = "Clash " + i, Description = "Original " + i, Status = i == 2 ? ClashResultStatus.Resolved : ClashResultStatus.Approved,
            Item1 = a, Item2 = b, Center = new Point3D(i, 0, 0) });
        var existing = new ClashResultGroup { DisplayName = "Existing" }; existing.Children.Add(new ClashResult { DisplayName = "Already grouped", Item1 = a, Item2 = b }); test.Children.Add(existing);
        doc.Clash.TestsData.Tests.Add(test); return doc;
    }
    private static void NativeAdapter()
    {
        var doc = Fixture();
        using (var session = new ClashReviewSession(doc))
        {
            var selected = session.ReadTests(); var snapshot = session.ReadSnapshot(selected.Select(t => t.Id));
            Check(snapshot.Clashes.Count == 4, "leaf snapshots include grouped results exactly once");
            var plan = SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.SharedElementA, null, 1, "Review", false);
            Check(plan.Groups.Count == 1 && plan.GroupedResults == 2 && plan.Skipped == 2, "group plan protects existing groups and resolved clashes");
            Check(SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.SharedElementA, null, 1, "Review", true).GroupedResults == 3, "resolved inclusion is explicit");
            Check(SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.PropertyA, "Element / System", 1, "Properties", false).GroupedResults == 2, "property grouping uses supplied values");
            Check(SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.PropertyB, "Missing", 1, "Properties", false).Groups.Count == 0, "missing properties never collapse into one group");
            Check(SmartGroupingPlanner.Build(snapshot, ClashGroupingMode.SpatialGrid, null, 1, "Grid", false).Groups.Count == 0, "grid boundaries remain separate");
            var test = (ClashTest)doc.Clash.TestsData.Tests[0]; var original = test.Children.OfType<ClashResult>().First(); var id = original.Guid;
            Check(session.ApplyGroups(plan, plan.Groups) == 2 && doc.Clash.TestsData.MoveCount == 2, "group adapter moves original results");
            var moved = test.Children.OfType<ClashResultGroup>().Single(g => g.DisplayName.StartsWith("Review"));
            Check(moved.Children.Any(item => item.Guid == id) && original.Description == "Original 0" && original.Status == ClashResultStatus.Approved, "moves preserve identity/status/description in double");
            Check(test.Children.OfType<ClashResultGroup>().Single(g => g.DisplayName == "Existing").Children.Count == 1, "existing group untouched");
            Reject(() => session.ApplyGroups(plan, plan.Groups), "stale group plan cannot be applied twice");
            snapshot = session.ReadSnapshot(selected.Select(t => t.Id)); var first = snapshot.Clashes.First(row => row.Id == id.ToString("D"));
            Check(session.ApplyDescriptions(snapshot, new[] { new DescriptionDraft { Source = first, Text = "Reviewed draft" } }) == 1 && original.Status == ClashResultStatus.Approved, "description edits do not change status in adapter");
            snapshot = session.ReadSnapshot(selected.Select(t => t.Id)); original.Description = "External edit without event";
            Reject(() => session.Validate(snapshot), "signature catches edits even without an event");
            snapshot = session.ReadSnapshot(selected.Select(t => t.Id)); doc.Models.Transform(); Reject(() => session.Validate(snapshot), "model transform invalidates preview");
            Exception error = null; var thread = new Thread(() => { try { session.ReadTests(); } catch (Exception ex) { error = ex; } }); thread.Start(); thread.Join(); Check(error is InvalidOperationException, "background native access blocked");
            Autodesk.Navisworks.Api.Application.ActiveDocument = new Document(); Reject(() => session.ReadTests(), "document switch blocked"); Autodesk.Navisworks.Api.Application.ActiveDocument = doc;
        }
    }
    private static void SpatialChecks()
    {
        var doc = Fixture(); var a = new SelectionSet { DisplayName = "Water" }; var b = new SelectionSet { DisplayName = "Sewer" };
        ClashMatrixService.Available = new List<ClashSetChoice> { new ClashSetChoice { Set = a, Name = "Water", Path = "Utilities/Water", SourceKind = "Set" }, new ClashSetChoice { Set = b, Name = "Sewer", Path = "Utilities/Sewer", SourceKind = "Set" } };
        using (var session = new ClashReviewSession(doc))
        {
            var utility = new SpatialCheckService(session, false);
            var rules = new List<SpatialCheckRule> { new SpatialCheckRule { Name = "Project requirement", SetAId = a.Guid.ToString("D"), SetBId = b.Guid.ToString("D"), Distance = 300, InputUnits = Units.Millimeters } };
            var plan = utility.Preview(rules); Near(plan[0].Tolerance, .3, "utility separation converts mm to document metres"); Check(plan[0].Type == ClashTestType.Clearance, "utility uses native Clearance type");
            rules[0].Distance = 0; Reject(() => utility.Preview(rules), "zero utility clearance rejected"); rules[0].Distance = 300;
            b.TestItemCount = 0; Reject(() => utility.Preview(rules), "empty geometry cannot appear clear"); b.TestItemCount = 1;
            var maintenance = new SpatialCheckService(session, true); plan = maintenance.Preview(rules); Check(plan[0].Type == ClashTestType.Hard && plan[0].Tolerance == 0, "maintenance checks supplied envelopes using Hard/zero tolerance");
            long revision = session.Revision; doc.SelectionSets.Touch(); Reject(() => utility.Create(rules, revision), "set changes block stale creation");
            var created = utility.Create(rules, session.Revision); Check(created.Count == 1, "created test GUID is retained for scoped run");
            int runs = doc.Clash.TestsData.RunCount; utility.Run(created, session.Revision, null); Check(doc.Clash.TestsData.RunCount == runs + 1, "runs created tests only");
            Reject(() => utility.Preview(rules), "existing test names prevent duplicate creation");
        }
    }
    private sealed class FakeHttp : HttpMessageHandler
    {
        public string Body, Auth, Response = "{\"message\":{\"content\":\"Draft from available data\"}}";
        public HttpStatusCode Code = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Body = await request.Content.ReadAsStringAsync(); Auth = request.Headers.Authorization?.ToString(); token.ThrowIfCancellationRequested(); return new HttpResponseMessage(Code) { Content = new StringContent(Response) }; }
    }
    private static async Task Ai()
    {
        var source = new ReviewClash { TestName = "Water / Structure", Name = "Clash", ItemA = "Pipe", ItemB = "Beam", TestType = "Clearance", ModelA = @"C:\secret\Water.nwc", ModelB = @"C:\secret\Structure.nwc", Units = "Meters" };
        source.PropertiesA["Element / Level"] = "L02"; source.PropertiesA["Private / Cost"] = "CONFIDENTIAL";
        string context = ClashDescriptionGenerator.Context(source); Check(!context.Contains("secret") && !context.Contains("CONFIDENTIAL") && context.Contains("L02"), "AI payload uses limited metadata and omits paths/unrelated properties");
        var local = new DescriptionAiOptions { Provider = DescriptionProvider.LocalOllama, Endpoint = "http://localhost:11434/api/chat", Model = "installed-model" };
        var handler = new FakeHttp(); string draft = await ClashDescriptionGenerator.GenerateAsync(source, local, CancellationToken.None, handler);
        Check(draft == "Draft from available data" && handler.Body.Contains("\"stream\":false") && handler.Auth == null, "local AI uses nonstreaming chat without cloud credentials");
        local.Endpoint = "http://remote.example/api/chat"; Reject(() => ClashDescriptionGenerator.ValidateOptions(local), "local mode rejects nonloopback hosts");
        var cloud = new DescriptionAiOptions { Provider = DescriptionProvider.CloudChatCompletions, Endpoint = "https://provider.example/v1/chat/completions", Model = "provider-model", ApiKey = "test-not-a-real-key" };
        Reject(() => ClashDescriptionGenerator.ValidateOptions(cloud), "cloud transmission needs explicit consent"); cloud.AllowCloud = true;
        handler = new FakeHttp { Response = "{\"choices\":[{\"message\":{\"content\":\"Cloud draft\"}}]}" };
        draft = await ClashDescriptionGenerator.GenerateAsync(source, cloud, CancellationToken.None, handler); Check(draft == "Cloud draft" && handler.Auth == "Bearer test-not-a-real-key", "compatible cloud response parsed, key only in authorization");
        handler = new FakeHttp { Code = HttpStatusCode.Unauthorized, Response = "SECRET provider error body" };
        bool safeError = false; try { await ClashDescriptionGenerator.GenerateAsync(source, cloud, CancellationToken.None, handler); } catch (InvalidOperationException ex) { safeError = ex.Message.Contains("401") && !ex.Message.Contains("SECRET"); } Check(safeError, "provider error bodies are not exposed");
        using (var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); bool stopped = false; try { await ClashDescriptionGenerator.GenerateAsync(source, cloud, cancelled.Token, new FakeHttp()); } catch (OperationCanceledException) { stopped = true; } Check(stopped, "AI generation cancellation works"); }
        Check(ClashDescriptionGenerator.Template(source).Contains("Reported Clearance conflict"), "template does not assert a physical hard clash");
        Check(ClashDescriptionGenerator.Template(source, ClashDescriptionGenerator.DetailedStyle).Contains("Native clash distance:") && ClashDescriptionGenerator.Template(source, ClashDescriptionGenerator.DetailedStyle).Contains(source.TestName), "detailed offline style includes test and native distance");
        Check(ClashDescriptionGenerator.Template(source, ClashDescriptionGenerator.ReviewNoteStyle).StartsWith("Coordination review — ") && !ClashDescriptionGenerator.Template(source).Contains("Native clash distance:"), "offline styles produce distinct supported formats");
        var offline = new DescriptionAiOptions { Provider = DescriptionProvider.Template, Style = ClashDescriptionGenerator.DetailedStyle };
        Check(await ClashDescriptionGenerator.GenerateAsync(source, offline, CancellationToken.None) == ClashDescriptionGenerator.Template(source, offline.Style), "offline drafting needs no endpoint model or credentials");
        using (var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); bool stopped = false; try { await ClashDescriptionGenerator.GenerateAsync(source, offline, cancelled.Token); } catch (OperationCanceledException) { stopped = true; } Check(stopped, "offline generation respects cancellation"); }
    }
    private static void CsvAndJournal()
    {
        Check(ReviewCsv.Read("A,B\r\n\"one,two\",\"line1\nline2\"\r\n")[1][1] == "line1\nline2", "CSV handles quoted separators and newlines");
        Reject(() => ReviewCsv.Read("\"unfinished"), "malformed CSV rejected");
        var doc = new object(); CoordinationChangeLog.Record(doc, "Test", "Scope", "Before", "After");
        Check(CoordinationChangeLog.Read(doc).Single().Before == "Before" && CoordinationChangeLog.Read(new object()).Count == 0, "change log is document-scoped with before/after");
        string path = Path.GetTempFileName(); ReviewCsv.Write(path, new[] { new[] { "=HYPERLINK(\"bad\")", "-SUM(1,2)", "-1.23" } });
        var row = ReviewCsv.Read(File.ReadAllText(path))[0]; Check(row[0].StartsWith("'") && row[1].StartsWith("'") && row[2] == "-1.23", "CSV blocks formula labels while retaining signed numbers"); File.Delete(path);
    }
    private static IEnumerable<Control> Descendants(Control parent)
    { foreach (Control child in parent.Controls) { yield return child; foreach (var next in Descendants(child)) yield return next; } }
    private static void Ui(bool verticalOnly=false)
    {
        string folder = Path.GetFullPath("Tests/Artifacts/SmartReview"); Directory.CreateDirectory(folder);
        ResizableRegions();
        var forms = verticalOnly ? new Form[]{new VerticalClearanceDialog()} : new Form[] { new SmartClashGrouperDialog((ClashReviewSession)null), new ChainageClashLocatorDialog((ClashReviewSession)null),
            new ClashDescriptionDialog((ClashReviewSession)null), new SpatialCheckDialog((ClashReviewSession)null, false), new SpatialCheckDialog((ClashReviewSession)null, true), new CoordinationChangeLogDialog(new object()), new ClashRenamerDialog(new RenameDouble()), new ContactInfoDialog(),new VerticalClearanceDialog() };
        foreach (var form in forms)
        using (form)
        {
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show();
            if(form is VerticalClearanceDialog)
            {
                var sourceModes=Descendants(form).OfType<ComboBox>().Where(c=>c.Name.EndsWith("SourceMode",StringComparison.Ordinal)).ToList();
                Check(sourceModes.Count==2 && sourceModes.All(c=>c.Items.Cast<string>().SequenceEqual(new[]{"Sets","Standard","Compact"})),"pipe and surface expose three selection views");
                foreach(var mode in sourceModes){mode.SelectedItem="Standard";mode.SelectedItem="Compact";mode.SelectedItem="Sets";}
                foreach(var pick in Descendants(form).OfType<ComboBox>().Where(c=>c.Name.EndsWith("SourceChoice",StringComparison.Ordinal)))
                {pick.Items.Add(new ClashSetChoice{Name="Long pipe model",Path="Federation/Drainage/Very long discipline model name and selection set for vertical cover review",SourceKind="Set"});pick.SelectedIndex=0;Check(pick.Dock==DockStyle.Fill,"source names stretch with their panel");}
                var resultGrid=Descendants(form).OfType<DataGridView>().Single();
                foreach(DataGridViewColumn col in resultGrid.Columns){int old=col.Width;col.Width=old+80;Check(col.Width==old+80 && col.Resizable==DataGridViewTriState.True,"vertical result column can be widened: "+col.Name);}
                var values=new List<VerticalClearanceFinding>{new VerticalClearanceFinding{Pipe="Pipe P-101 — long coordination element name",Result=new VerticalCoverResult{State="Insufficient cover",Cover=.7,Required=1,SurfaceLevel=12.7,PipeTop=12,X=750000,Y=8885000,CoveragePercent=100,Note="Minimum vertical cover on the complete model mesh."}}};
                typeof(VerticalClearanceDialog).GetField("findings",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,values);
                typeof(VerticalClearanceDialog).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
                Check(Descendants(form).OfType<DataGridView>().Single().Rows.Count==1,"vertical review renders populated result and section");
            }
            if (form is ClashRenamerDialog)
            {
                Descendants(form).OfType<TextBox>().Single(c => c.AccessibleName == "Naming pattern").Text = "{n}_{a}_VS_{b}";
                Descendants(form).OfType<Button>().Single(b => b.Text == "Preview pattern").PerformClick();
            }
            if (form is ClashDescriptionDialog)
            {
                var controls = Descendants(form).ToList();
                Check(!controls.OfType<Label>().Any(c => new[] { "Mode", "Endpoint", "Model ID", "API key" }.Contains(c.Text)) && !controls.OfType<TextBox>().Any(c => c.UseSystemPasswordChar), "description dialog removes marked provider and credential fields");
                Check(!controls.OfType<CheckBox>().Any(c => c.Text.Contains("cloud")), "offline dialog removes unused cloud consent");
                var styles = controls.OfType<ComboBox>().Single(c => c.AccessibleName == "Description style");
                Check(styles.Items.Count == 3 && styles.SelectedIndex == 0, "description style has supported offline formats");
                var grid = controls.OfType<DataGridView>().Single(); grid.Rows.Add(true, "Test", "Clash", "Original", "Old draft");
                styles.SelectedIndex = 1;
                Check(Convert.ToString(grid.Rows[0].Cells[4].Value) == "Old draft", "style changes retain existing drafts");
                grid.Rows.Clear();
            }
            foreach (var size in new[] { form.Size, form.MinimumSize, new Size(1600, 1000) })
            {
                form.Size = size; form.PerformLayout();
                foreach (var child in Descendants(form)) child.PerformLayout();
                foreach (var control in Descendants(form).Where(c => c.Visible && (c is Button || c is ComboBox || c is NumericUpDown || c is TextBox || c is CheckBox)))
                {
                    var bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                    Check(form.ClientRectangle.Contains(bounds), form.Text + " control fits: " + (control.AccessibleName ?? control.Text) + " @" + size.Width);
                }
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(folder, form.GetType().Name + (form is SpatialCheckDialog ? (form.Text.Contains("Utility") ? "-Utility" : "-Maintenance") : "") + "-" + size.Width + ".png")); }
                if (form is ContactInfoDialog)
                {
                    var tabs = Descendants(form).OfType<TabControl>().Single();
                    for (int tab = 1; tab < tabs.TabCount; tab++)
                    {
                        tabs.SelectedIndex = tab; tabs.PerformLayout(); foreach (var child in Descendants(form)) child.PerformLayout();
                        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(folder, "ContactInfoDialog-" + tab + "-" + size.Width + ".png")); }
                    }
                    tabs.SelectedIndex = 0;
                }
            }
            var pin = Descendants(form).OfType<CheckBox>().SingleOrDefault(c => c.Text == "Pin window"); if (pin != null) { pin.Checked = true; Check(form.TopMost, form.Text + " pin works"); pin.Checked = false; }
            Descendants(form).OfType<Button>().Single(button => button.Text == "Close").PerformClick(); Check(form.IsDisposed, form.Text + " modeless close works");
        }
    }

    private sealed class RenameDouble : IClashRenamerService
    {
        public int Writes;
        public List<ClashRenameItem> ReadTests() => new List<ClashRenameItem> {
            new ClashRenameItem { Id = "1", CurrentName = "H_Pipes_VS_Structure", NewName = "H_Pipes_VS_Structure" },
            new ClashRenameItem { Id = "2", CurrentName = "M_Ducts_VS_Walls", NewName = "M_Ducts_VS_Walls" } };
        public ClashRenameResult Apply(IEnumerable<ClashRenameItem> rows) { Writes++; return new ClashRenameResult(); }
    }

    private static void RenameAndPreferences()
    {
        var service = new RenameDouble(); var source = service.ReadTests();
        var names = RenamePatterns.Build(source, "{prefix}{n}_{name}", "ZONE_", 10, 5, 3);
        Check(names[0].NewName == "ZONE_010_H_Pipes_VS_Structure" && names[1].NewName == "ZONE_015_M_Ducts_VS_Walls", "pattern numbering honours start step padding and input order");
        Check(RenamePatterns.Build(source, "{priority}_{a}_VS_{b}_{n}", "", 1, 1, 2)[1].NewName == "M_Ducts_VS_Walls_02", "discipline preset parses valid naming fields");
        Reject(() => RenamePatterns.Build(source, "{unknown}", "", 1, 1, 3), "unknown pattern tokens are rejected");
        Reject(() => RenamePatterns.Build(new[] { new ClashRenameItem { CurrentName = "Unstructured" } }, "{a}_{b}", "", 1, 1, 3), "discipline fields reject unstructured names without guessing");
        Reject(() => RenamePatterns.Build(source, "{name", "", 1, 1, 3), "unmatched braces rejected");
        Reject(() => RenamePatterns.Build(source, "{n}", "", int.MaxValue, 1, 3), "numbering overflow rejected before staging");
        Reject(() => RenamePatterns.Build(source, "\n", "", 1, 1, 3), "empty pattern rejected");
        var literal = new[] { new ClashRenameItem { CurrentName = "Literal {n}" } };
        Check(RenamePatterns.Build(literal, "{name}_{n}", "", 1, 1, 1)[0].NewName == "Literal {n}_1", "model names are data and never recursively expanded");
        var diff = RenamePatterns.Difference("ABC_old_end", "ABC_new_end");
        Check(diff.Item1 == 4 && diff.Item2 == 3 && diff.Item3 == 3, "diff locates changed span with common prefix and suffix");
        Check(RenamePatterns.Difference("ABC", "ABC").Item2 == 0 && RenamePatterns.Difference("ABC", "XABC").Item3 == 1, "diff handles identical names and insertion");
        using (var form = new ClashRenamerDialog(service))
        {
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show();
            var controls = Descendants(form).ToList(); var grid = controls.OfType<DataGridView>().Single();
            grid.Rows[1].Cells["Selected"].Value = false;
            controls.OfType<Button>().Single(b => b.Text == "Preview pattern").PerformClick();
            Check(Convert.ToString(grid.Rows[0].Cells["NewName"].Value) == "001_H_Pipes_VS_Structure" && Convert.ToString(grid.Rows[1].Cells["NewName"].Value) == source[1].CurrentName && service.Writes == 0, "pattern preview stages checked rows only without model writes");
            var afterPane = controls.OfType<RichTextBox>().Single(r => r.AccessibleName == "After text differences"); afterPane.Select(0, 4);
            Check(afterPane.SelectionBackColor == Color.FromArgb(199, 239, 206), "inserted numbering is highlighted in after pane");
            grid.Rows[0].Cells["NewName"].Value = source[1].CurrentName;
            Check(!controls.OfType<Button>().Single(b => b.Text == "Apply selected names").Enabled && grid.Rows[0].ErrorText.Contains("Duplicate"), "duplicate against unchecked original name blocks apply");
            var rich = controls.OfType<RichTextBox>().ToList();
            Check(rich.Count == 2 && rich.All(r => r.ReadOnly), "before after highlighted panes are read only");
        }
        using (var form = new ContactInfoDialog())
        {
            var text = string.Join(" ", Descendants(form).Select(c => c.Text));
            Check(text.Contains("2024") && text.Contains("2025") && text.Contains("2026") && text.Contains("1.0.33") && text.Contains("Release notes"), "contact window contains version releases and release notes");
            Check(Descendants(form).OfType<Button>().Any(b => b.Text == "Copy diagnostics"), "diagnostics copy is an explicit button");
        }
        string diagnostic = NavPro.Classes.NavProSupportInfo.Diagnostics();
        Check(!diagnostic.Contains(Environment.UserName + "\\") && !diagnostic.Contains(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) && !diagnostic.Contains("EPD-JCB"), "diagnostics omit profile paths and project names");
        var doc = Fixture(); var nativeRenamer = new ClashRenamerService(doc); var requested = nativeRenamer.ReadTests();
        var nativeTest = doc.Clash.TestsData.Tests.OfType<ClashTest>().Single(); nativeTest.DisplayName = "Changed externally"; requested[0].NewName = "Proposed";
        Reject(() => nativeRenamer.Apply(requested), "native rename adapter rejects externally renamed source before writing");
        Check(doc.Transactions == 0, "stale rename opens no transaction in double");
        nativeTest.DisplayName = requested[0].CurrentName; var children = nativeTest.Children.ToArray();
        nativeRenamer.Apply(requested);
        Check(nativeTest.DisplayName == "Proposed" && children.SequenceEqual(nativeTest.Children), "rename adapter preserves original child result objects");

        Func<Form> make = () =>
        {
            var form = new Form { Name = "PreferencesFixture", Size = new Size(900, 650), MinimumSize = new Size(500, 400), StartPosition = FormStartPosition.Manual };
            var filter = new TextBox { AccessibleName = "Filter tests", Dock = DockStyle.Top };
            var secret = new TextBox { UseSystemPasswordChar = true, AccessibleName = "Filter API key", Text = "DO-NOT-PERSIST", Dock = DockStyle.Bottom };
            var grid = new DataGridView { Name = "Results", Dock = DockStyle.Fill }; grid.Columns.Add("name", "Name");
            var split = ResizableLayout.Split(filter, grid, Orientation.Vertical, .3, "List and results"); form.Controls.Add(split); form.Controls.Add(secret);
            form.Location = Screen.PrimaryScreen.WorkingArea.Location;
            return form;
        };
        using (var original = make())
        {
            original.CreateControl(); original.PerformLayout();
            Descendants(original).OfType<TextBox>().Single(b => !b.UseSystemPasswordChar).Text = "Water";
            Descendants(original).OfType<DataGridView>().Single().Columns[0].Width = 237;
            var split = Descendants(original).OfType<SplitContainer>().Single(); split.SplitterDistance = 300;
            WindowPreferences.Save(original);
        }
        string file = Directory.GetFiles(WindowPreferences.StorageRoot, "*.json").Single();
        Check(!File.ReadAllText(file).Contains("DO-NOT-PERSIST"), "saved preferences never include credentials");
        using (var restored = make())
        {
            restored.Size = new Size(600, 450); restored.CreateControl(); restored.PerformLayout(); WindowPreferences.Restore(restored);
            Check(restored.Size == new Size(900, 650), "window size survives close and reopen");
            Check(Descendants(restored).OfType<TextBox>().Single(b => !b.UseSystemPasswordChar).Text == "Water" && Descendants(restored).OfType<DataGridView>().Single().Columns[0].Width == 237, "filter and column width survive reopen");
            Check(Math.Abs(Descendants(restored).OfType<SplitContainer>().Single().SplitterDistance - 300) <= 2, "divider position survives reopen");
            WindowPreferences.Save(restored); Check(File.Exists(file), "preference updates replace existing file atomically");
        }
        var saved = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<WindowPreferences.State>(File.ReadAllText(file));
        saved.X = 90000; saved.Y = 90000; saved.Maximized = true;
        File.WriteAllText(file, new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(saved));
        using (var moved = make()) { WindowPreferences.Restore(moved); Check(Screen.AllScreens.Any(s => s.WorkingArea.Contains(moved.Location)), "saved position on a removed monitor returns to an available screen"); Check(moved.WindowState == FormWindowState.Maximized, "maximized preference is restored"); }
        File.WriteAllText(file, "{invalid-json");
        using (var fresh = make()) { WindowPreferences.Restore(fresh); Check(fresh.Size.Width == 900, "corrupt preferences keep a usable default window"); }
    }

    private static void ResizableRegions()
    {
        using (var form = new Form { Size = new Size(1100, 850), ShowInTaskbar = false, Location = new Point(-20000, -20000), StartPosition = FormStartPosition.Manual })
        {
            var table = new TableLayoutPanel { ColumnCount = 1, RowCount = 5, Dock = DockStyle.Fill };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 30)); table.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 30)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var header = new Label { Text = "Header", Dock = DockStyle.Fill };
            var models = new CheckedListBox { Dock = DockStyle.Fill, IntegralHeight = false }; models.Items.Add("A long loaded model filename.nwc", true);
            var picks = ResizableLayout.Split(new ListBox(), new ListBox(), Orientation.Vertical, 0.5, "A and B");
            var results = new DataGridView { Dock = DockStyle.Fill }; results.Columns.Add("result", "Result"); results.Rows.Add("Retained draft");
            var close = new Button { Text = "Close", Dock = DockStyle.Fill };
            table.Controls.Add(header, 0, 0); table.Controls.Add(models, 0, 1); table.Controls.Add(picks, 0, 2); table.Controls.Add(results, 0, 3); table.Controls.Add(close, 0, 4);
            var split = ResizableLayout.Rows(table, 1, 3, 1, 0.3, "Models and design");
            ResizableLayout.Rows((TableLayoutPanel)split.Panel2.Controls[0], 0, 1, 0, 0.55, "Selections and preview");
            form.Controls.Add(table); form.Show(); form.PerformLayout();
            Check(table.RowCount == 3 && table.GetRow(close) == 2 && table.GetRow(header) == 0, "split conversion preserves fixed header/footer rows");
            Check(Descendants(form).Count(c => ReferenceEquals(c, models)) == 1 && models.GetItemChecked(0) && Convert.ToString(results.Rows[0].Cells[0].Value) == "Retained draft", "resizing retains original controls selections and preview values");
            int before = models.Height; split.SplitterDistance += 40; form.PerformLayout();
            Check(models.Height > before, "dragging divider enlarges loaded model list");
            double proportion = (double)split.SplitterDistance / (split.ClientSize.Height - split.SplitterWidth);
            form.Size = new Size(1500, 1050); form.PerformLayout();
            Check(Math.Abs(proportion - (double)split.SplitterDistance / (split.ClientSize.Height - split.SplitterWidth)) < 0.02, "window expansion preserves chosen divider proportion");
            Check(models.Height > before && picks.Width > 0 && results.Height > 0 && close.Bottom <= table.ClientSize.Height, "expanded nested layout keeps lists preview and footer visible");
            foreach (var size in new[] { new Size(720, 620), new Size(1100, 850) }) { form.Size = size; form.PerformLayout(); Check(Descendants(form).OfType<SplitContainer>().All(s => s.Panel1.Width > 0 && s.Panel2.Width > 0 && s.Panel1.Height > 0 && s.Panel2.Height > 0), "split panels remain usable after shrink and restore"); }
            Check(Descendants(form).OfType<SplitContainer>().All(s => s.Parent.ClientRectangle.Contains(s.Bounds)), "nested dividers stay inside their parent without overflowing previews");
            Check(results.RectangleToScreen(results.ClientRectangle).Bottom <= close.RectangleToScreen(close.ClientRectangle).Top, "matrix preview remains visible above fixed footer");
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/Artifacts/SmartReview/Resizable-regions.png"); }
        }
    }
}
