using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NavPro.Backend.Models;

namespace NavPro.Backend.Core
{
    public static class ProjectDescriptionTemplate
    {
        public const string Default = "Reported conflict between {SystemA} and {SystemB} at {Level}, chainage {Chainage}. Review in model context.";
        public static string Property(IDictionary<string, string> properties, params string[] names)
        {
            var matches = properties.Where(p => names.Any(n => string.Equals(p.Key.Substring(p.Key.LastIndexOf('/') + 1).Trim(), n, StringComparison.OrdinalIgnoreCase)))
                .Select(p => p.Value?.Trim()).Where(v => !string.IsNullOrEmpty(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        public static string Render(ReviewClash row, string pattern, out string missing)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 2000) throw new InvalidOperationException("Enter a project template of 1–2000 characters.");
            string a = Property(row.PropertiesA, "level", "reference level"), b = Property(row.PropertiesB, "level", "reference level");
            string ca = Property(row.PropertiesA, "chainage", "station"), cb = Property(row.PropertiesB, "chainage", "station");
            var fields = new Dictionary<string, string>(StringComparer.Ordinal) {
                { "SystemA", Property(row.PropertiesA, "system", "system name") }, { "SystemB", Property(row.PropertiesB, "system", "system name") },
                { "Level", string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) || a == b ? a : null },
                { "Chainage", string.IsNullOrEmpty(ca) ? cb : string.IsNullOrEmpty(cb) || ca == cb ? ca : null },
                { "ItemA", row.ItemA }, { "ItemB", row.ItemB }, { "Test", row.TestName }, { "Clash", row.Name } };
            var absent = new HashSet<string>(StringComparer.Ordinal);
            string rendered = Regex.Replace(pattern, @"\{([^{}]+)\}", match => {
                if (!fields.TryGetValue(match.Groups[1].Value, out var value)) throw new InvalidOperationException("Unknown template field: " + match.Value);
                if (string.IsNullOrWhiteSpace(value)) { absent.Add(match.Groups[1].Value); return "[MISSING " + match.Groups[1].Value + "]"; }
                return value;
            });
            if (Regex.Replace(pattern, @"\{[^{}]+\}", "").IndexOfAny(new[] { '{', '}' }) >= 0) throw new InvalidOperationException("Unbalanced template braces.");
            missing = string.Join(", ", absent.OrderBy(v => v)); return rendered;
        }
    }
}
