using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NavPro.Backend.Models;

namespace NavPro.Backend.Core
{
    public static class SmartGroupingPlanner
    {
        public static ClashGroupingPlan Build(ReviewSnapshot snapshot, ClashGroupingMode mode, string property, double gridSize,
            string prefix, bool includeResolved)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!Enum.IsDefined(typeof(ClashGroupingMode), mode)) throw new InvalidOperationException("Choose a grouping method.");
            if ((mode == ClashGroupingMode.PropertyA || mode == ClashGroupingMode.PropertyB) && string.IsNullOrWhiteSpace(property))
                throw new InvalidOperationException("Choose a property to group by.");
            if ((mode == ClashGroupingMode.SpatialGrid || mode == ClashGroupingMode.DistanceCluster) && (double.IsNaN(gridSize) || double.IsInfinity(gridSize) || gridSize <= 0))
                throw new InvalidOperationException("Grid size must be a finite positive distance.");
            prefix = (prefix ?? "").Trim();
            if (prefix.Length == 0 || prefix.Length > 60 || prefix.Any(char.IsControl)) throw new InvalidOperationException("Enter a group prefix of 1–60 printable characters.");
            var plan = new ClashGroupingPlan { Snapshot = snapshot };
            foreach (var test in snapshot.Clashes.GroupBy(row => row.TestId))
            {
                if (mode == ClashGroupingMode.DistanceCluster)
                {
                    Cluster(plan, test.Where(r => string.IsNullOrEmpty(r.ParentGroup) && (includeResolved || r.Status != "Resolved")).OrderBy(r => r.Id, StringComparer.Ordinal).ToList(), gridSize, prefix);
                    continue;
                }
                var buckets = new Dictionary<string, List<ReviewClash>>(StringComparer.Ordinal);
                foreach (var row in test.OrderBy(row => row.Id, StringComparer.Ordinal))
                {
                    // Never dismantle existing groups or silently reclassify resolved results.
                    if (!string.IsNullOrEmpty(row.ParentGroup) || (!includeResolved && row.Status == "Resolved")) continue;
                    string key = Key(row, mode, property, gridSize);
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!buckets.TryGetValue(key, out var members)) buckets[key] = members = new List<ReviewClash>();
                    members.Add(row);
                }
                int number = 0;
                foreach (var bucket in buckets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (bucket.Value.Count < 2) continue;
                    var group = new ReviewGroup { TestId = test.Key, TestName = bucket.Value[0].TestName,
                        Name = prefix + " " + (++number).ToString("D3", CultureInfo.InvariantCulture) + " — " + Label(bucket.Value[0], mode, property, gridSize) };
                    if (group.Name.Length > 180) group.Name = group.Name.Substring(0, 180);
                    group.ResultIds.AddRange(bucket.Value.Select(row => row.Id));
                    foreach (var row in bucket.Value) group.Reasons[row.Id] = mode + ": " + Label(row, mode, property, gridSize);
                    plan.Groups.Add(group);
                }
            }
            return plan;
        }
        private static string Key(ReviewClash row, ClashGroupingMode mode, string property, double size)
        {
            switch (mode)
            {
                case ClashGroupingMode.SharedElementA: return row.KeyA;
                case ClashGroupingMode.SharedElementB: return row.KeyB;
                case ClashGroupingMode.ModelPair: return string.IsNullOrEmpty(row.ModelA) || string.IsNullOrEmpty(row.ModelB) ? null : row.ModelA.Length + ":" + row.ModelA + row.ModelB;
                case ClashGroupingMode.PropertyA: return row.PropertiesA.TryGetValue(property, out var a) && !string.IsNullOrWhiteSpace(a) ? a : null;
                case ClashGroupingMode.PropertyB: return row.PropertiesB.TryGetValue(property, out var b) && !string.IsNullOrWhiteSpace(b) ? b : null;
                case ClashGroupingMode.Level: return SharedProperty(row, "level", "reference level");
                case ClashGroupingMode.Zone: return SharedProperty(row, "zone", "zone name");
                case ClashGroupingMode.SpatialGrid:
                    if (new[] { row.X, row.Y, row.Z }.Any(v => double.IsNaN(v) || double.IsInfinity(v))) return null;
                    return string.Join(",", new[] { row.X, row.Y, row.Z }.Select(v => Math.Floor(v / size).ToString("R", CultureInfo.InvariantCulture)));
                default: return null;
            }
        }
        private static string SharedProperty(ReviewClash row, params string[] names)
        {
            string a = ProjectDescriptionTemplate.Property(row.PropertiesA, names), b = ProjectDescriptionTemplate.Property(row.PropertiesB, names);
            return string.IsNullOrWhiteSpace(a) ? b : string.IsNullOrWhiteSpace(b) || string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ? a : null;
        }
        private static void Cluster(ClashGroupingPlan plan, List<ReviewClash> rows, double radius, string prefix)
        {
            rows = rows.Where(r => new[] { r.X, r.Y, r.Z }.All(v => !double.IsNaN(v) && !double.IsInfinity(v))).ToList();
            // Deterministic connected components: each member has a neighbour within radius;
            // the end-to-end cluster diameter can exceed radius. No merging across tests.
            if (rows.Count > 10000) throw new InvalidOperationException("Distance clustering supports up to 10,000 eligible clashes per test. Narrow the selected test first.");
            int[] parent = Enumerable.Range(0, rows.Count).ToArray();
            Func<int, int> root = null; root = i => { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; };
            var reason = new Dictionary<int, string>();
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < i; j++)
                {
                    double dx = (rows[i].X - rows[j].X) / radius, dy = (rows[i].Y - rows[j].Y) / radius, dz = (rows[i].Z - rows[j].Z) / radius;
                    double squared = dx * dx + dy * dy + dz * dz;
                    if (squared > 1) continue;
                    parent[root(i)] = root(j);
                    string distance = (Math.Sqrt(squared) * radius).ToString("0.###", CultureInfo.InvariantCulture);
                    if (!reason.ContainsKey(i)) reason[i] = "Within " + distance + " of " + rows[j].Name;
                    if (!reason.ContainsKey(j)) reason[j] = "Within " + distance + " of " + rows[i].Name;
                }
            int number = 0;
            foreach (var component in Enumerable.Range(0, rows.Count).GroupBy(i => root(i)).Where(g => g.Count() > 1))
            {
                var first = rows[component.First()];
                var group = new ReviewGroup { TestId = first.TestId, TestName = first.TestName, Name = prefix + " " + (++number).ToString("D3") + " — connected distance cluster" };
                foreach (int i in component) { group.ResultIds.Add(rows[i].Id); group.Reasons[rows[i].Id] = reason[i] + " " + plan.Snapshot.Units + "; connected radius " + radius.ToString("G9", CultureInfo.InvariantCulture); }
                plan.Groups.Add(group);
            }
        }
        private static string Label(ReviewClash row, ClashGroupingMode mode, string property, double size)
        {
            switch (mode)
            {
                case ClashGroupingMode.SharedElementA: return "A: " + row.ItemA;
                case ClashGroupingMode.SharedElementB: return "B: " + row.ItemB;
                case ClashGroupingMode.ModelPair: return System.IO.Path.GetFileName(row.ModelA) + " / " + System.IO.Path.GetFileName(row.ModelB);
                default: return Key(row, mode, property, size);
            }
        }
    }
}
