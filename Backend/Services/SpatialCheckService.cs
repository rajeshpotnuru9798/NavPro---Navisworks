using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    public sealed class SpatialCheckRule
    {
        public string Name, SetAId, SetBId, Source, Revision;
        public double Distance;
        public Units InputUnits;
    }
    public sealed class CreatedSpatialTest
    {
        public string Id, Name;
        public ClashTestType Type;
        public double Tolerance;
    }
    /// <summary>Native solid-surface checks, using the existing typed selection-source adapter.</summary>
    public sealed class SpatialCheckService
    {
        private readonly ClashReviewSession session;
        private readonly ClashMatrixService matrix;
        private readonly bool maintenance;
        public SpatialCheckService(ClashReviewSession session, bool maintenance)
        { this.session = session; matrix = new ClashMatrixService(session.Document); this.maintenance = maintenance; }
        public List<ClashSetChoice> ReadSets() { session.CheckHost(); return matrix.ReadSnapshot().Sets; }
        public List<ClashMatrixPair> Preview(IList<SpatialCheckRule> rules)
        {
            session.CheckHost(); if (rules == null || rules.Count == 0) throw new InvalidOperationException("Add at least one check rule.");
            var snapshot = matrix.ReadSnapshot(); var sets = snapshot.Sets.ToDictionary(set => set.Set.Guid.ToString("D"));
            var existing = new HashSet<string>(snapshot.ExistingTests.Select(test => test.DisplayName), StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var pairs = new HashSet<string>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal); var output = new List<ClashMatrixPair>();
            foreach (var rule in rules)
            {
                if (!sets.TryGetValue(rule.SetAId, out var a) || !sets.TryGetValue(rule.SetBId, out var b)) throw new InvalidOperationException("A selected set changed or was removed. Refresh and rebuild the rules.");
                string name = (rule.Name ?? "").Trim();
                if (name.Length == 0 || name.Length > 180 || name.Any(char.IsControl)) throw new InvalidOperationException("Each test name needs 1–180 printable characters.");
                if (!names.Add(name) || existing.Contains(name)) throw new InvalidOperationException("Test name already exists or is repeated: " + name);
                string key = string.CompareOrdinal(rule.SetAId, rule.SetBId) < 0 ? rule.SetAId + "/" + rule.SetBId : rule.SetBId + "/" + rule.SetAId;
                if (rule.SetAId == rule.SetBId || !pairs.Add(key)) throw new InvalidOperationException("Same-set and repeated/reversed rule pairs are not allowed.");
                if (double.IsNaN(rule.Distance) || double.IsInfinity(rule.Distance) || rule.Distance < 0 || (!maintenance && rule.Distance <= 0))
                    throw new InvalidOperationException("Utility separation must be positive. Maintenance uses the supplied envelope itself.");
                foreach (var set in new[] { a, b })
                {
                    string id = set.Set.Guid.ToString("D"); if (!counts.ContainsKey(id)) counts[id] = matrix.CountItems(set);
                    if (counts[id] == 0) throw new InvalidOperationException("Set '" + set.Path + "' is empty. An empty scope cannot be reported as clear.");
                    set.ItemCount = counts[id];
                }
                double tolerance = maintenance ? 0 : rule.Distance * UnitConversion.ScaleFactor(rule.InputUnits, session.Document.Units);
                if (double.IsNaN(tolerance) || double.IsInfinity(tolerance) || (!maintenance && tolerance <= 0)) throw new InvalidOperationException("The converted separation is outside the supported numeric range.");
                output.Add(new ClashMatrixPair { A = a, B = b, TestName = name, Type = maintenance ? ClashTestType.Hard : ClashTestType.Clearance,
                    Tolerance = tolerance, PrimitiveTypesA = PrimitiveTypes.Triangles, PrimitiveTypesB = PrimitiveTypes.Triangles, Validation = "Ready" });
            }
            return output;
        }
        public List<CreatedSpatialTest> Create(IList<SpatialCheckRule> rules, long expectedRevision)
        {
            session.CheckHost(); if (session.Revision != expectedRevision) throw new InvalidOperationException("Models, tests or sets changed since preview. Preview again.");
            var plan = Preview(rules); matrix.CreateTests(plan, false);
            var live = Tests().ToList(); var created = new List<CreatedSpatialTest>();
            foreach (var pair in plan)
            {
                var test = live.Single(item => item.DisplayName == pair.TestName);
                if (test.TestType != pair.Type || test.Tolerance != pair.Tolerance) throw new InvalidOperationException("Native test settings were not retained. Inspect Clash Detective before retrying.");
                created.Add(new CreatedSpatialTest { Id = test.Guid.ToString("D"), Name = test.DisplayName, Type = test.TestType, Tolerance = test.Tolerance });
                CoordinationChangeLog.Record(session.Document, maintenance ? "Access and Maintenance Clearance Review" : "Utility Separation Checker", test.DisplayName,
                    "No test", test.TestType + "; tolerance " + test.Tolerance.ToString("G9", CultureInfo.InvariantCulture) + " " + session.UnitsLabel + "; A: " + pair.A.Path + "; B: " + pair.B.Path);
            }
            return created;
        }
        public Dictionary<string, int> Run(IList<CreatedSpatialTest> created, long expectedRevision, Action<int, int> progress)
        {
            session.CheckHost(); if (created == null || created.Count == 0) throw new InvalidOperationException("Create the previewed checks first.");
            if (session.Revision != expectedRevision) throw new InvalidOperationException("Model, set or test data changed since creation. Review and run the saved checks in Clash Detective.");
            foreach (var expected in created)
            {
                var live = Tests().SingleOrDefault(test => test.Guid.ToString("D") == expected.Id);
                if (live == null || live.DisplayName != expected.Name || live.TestType != expected.Type || live.Tolerance != expected.Tolerance)
                    throw new InvalidOperationException("A newly created check changed. Review it in Clash Detective before running.");
            }
            var output = new Dictionary<string, int>(); int completed = 0;
            foreach (var expected in created)
            {
                session.CheckHost(); var test = Tests().Single(item => item.Guid.ToString("D") == expected.Id);
                ((DocumentClash)session.Document.Clash).TestsData.TestsRunTest(test);
                test = Tests().Single(item => item.Guid.ToString("D") == expected.Id);
                output.Add(expected.Id, CountOpen(test.Children)); progress?.Invoke(++completed, created.Count);
                CoordinationChangeLog.Record(session.Document, "Run spatial checks", expected.Name, "Native clash run requested", output[expected.Id] + " current non-resolved result(s); saved history retained by Navisworks");
            }
            return output;
        }
        private IEnumerable<ClashTest> Tests() => Flatten(((DocumentClash)session.Document.Clash).TestsData.Tests);
        private static IEnumerable<ClashTest> Flatten(IEnumerable<SavedItem> items)
        { foreach (var item in items) { if (item is ClashTest test) yield return test; else if (item is GroupItem group) foreach (var child in Flatten(group.Children)) yield return child; } }
        private static int CountOpen(IEnumerable<SavedItem> items) => items.Sum(item => item is ClashResult result ? (result.Status == ClashResultStatus.Resolved ? 0 : 1) : item is ClashResultGroup group ? CountOpen(group.Children) : 0);
    }
}
