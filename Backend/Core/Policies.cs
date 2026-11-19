using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using NavPro.Backend.Models;

namespace NavPro.Backend.Core
{
    // No Autodesk dependencies: executable tests run outside the native host.
    public sealed class PropertyKey
    {
        public string Category { get; }
        public string Property { get; }
        public string Id => Token(Category) + Token(Property);
        public PropertyKey(string category, string property)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(property))
                throw new ArgumentException("Both internal category and property names are required.");
            Category = category; Property = property;
        }
        public static string Token(string value) => value.Length + ":" + value;
    }

    public sealed class CriteriaSpec
    {
        public PropertyKey Property { get; }
        public string ValueKey { get; }
        public CriteriaSpec(PropertyKey property, string nativeType, string canonicalValue)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            if (string.IsNullOrEmpty(nativeType) || canonicalValue == null) throw new ArgumentException("Typed value required.");
            ValueKey = PropertyKey.Token(nativeType) + PropertyKey.Token(canonicalValue);
        }
    }

    public sealed class NameDecision
    {
        public string Name { get; set; }
        public string Error { get; set; }
        public bool Exists { get; set; }
        public string Status => Error ?? (Exists ? "Skip: name already exists" : "Ready");
    }

    public static class NamingPolicy
    {
        // Conservative NavPro policy, not a claim that Navisworks forbids all these characters.
        public static List<NameDecision> Plan(IEnumerable<string> values, IEnumerable<string> existing)
        {
            var plans = values.Select(v => new NameDecision { Name = v }).ToList();
            Validate(plans, existing);
            return plans;
        }

        public static void Validate(IList<NameDecision> plans, IEnumerable<string> existing)
        {
            var occupied = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            var duplicates = new HashSet<string>(plans.GroupBy(p => p.Name?.Trim() ?? "", StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var p in plans)
            {
                p.Error = null;
                if (string.IsNullOrWhiteSpace(p.Name)) p.Error = "Name is blank";
                else if (p.Name != p.Name.Trim()) p.Error = "Remove leading/trailing whitespace (including in model value)";
                else if (p.Name.Length > 240) p.Error = "Name exceeds NavPro's 240-character limit";
                else if (p.Name.Any(c => char.IsControl(c) || "\\/:*?\"<>|".Contains(c))) p.Error = "Name contains a control or reserved character: \\ / : * ? \" < > |";
                else if (duplicates.Contains(p.Name)) p.Error = "Duplicate generated name: change naming or source values";
                p.Exists = occupied.Contains(p.Name);
            }
        }
    }

    public static class ModelNumberPropertyPolicy
    {
        public static int Score(string label)
        {
            var words = Regex.Matches(label ?? "", "[A-Za-z0-9]+")
                .Cast<Match>().Select(match => match.Value.ToLowerInvariant()).ToArray();
            bool model = words.Contains("model") || words.Any(word => word.StartsWith("model", StringComparison.Ordinal));
            bool explicitModelNumber = words.Any(word => word == "modelnumber" || word == "modelno" || word == "modelfilenumber" || word == "modelfileno") ||
                HasSequence(words, "model", "number") || HasSequence(words, "model", "no") ||
                HasSequence(words, "model", "file", "number") || HasSequence(words, "model", "file", "no");
            if (explicitModelNumber) return 3;
            bool explicitFileNumber = words.Any(word => word == "filenumber" || word == "fileno") ||
                HasSequence(words, "file", "number") || HasSequence(words, "file", "no");
            if (explicitFileNumber) return 2;
            return model ? 1 : 0;
        }

        private static bool HasSequence(string[] words, params string[] sequence)
        {
            for (int i = 0; i <= words.Length - sequence.Length; i++)
                if (sequence.Select((word, offset) => words[i + offset] == word).All(match => match)) return true;
            return false;
        }

        public static PropertyOption SelectUnique(IEnumerable<PropertyOption> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var candidates = options.Where(option => Score(option.Label) == 3).ToList();
            if (candidates.Count == 0) candidates = options.Where(option => Score(option.Label) == 2).ToList();
            if (candidates.Count == 0)
                throw new InvalidOperationException("No clearly named Model Number / Model No, Model/File Number, or File Number property was found.");
            if (candidates.Count > 1)
                throw new InvalidOperationException("More than one equally ranked model/file-number property was found.");
            return candidates[0];
        }
    }

    public static class ClashPairPolicy
    {
        public static string PairKey(string a, string b) =>
            string.CompareOrdinal(a, b) <= 0 ? a + "\n" + b : b + "\n" + a;

        public static bool TryClaimPair(string a, string b, ISet<string> seen)
        {
            if (seen == null) throw new ArgumentNullException(nameof(seen));
            return seen.Add(PairKey(a, b));
        }

        public static string EmptySourceValidation(int countA, bool searchSetA, int countB, bool searchSetB)
        {
            if ((countA == 0 && !searchSetA) || (countB == 0 && !searchSetB))
                return "Invalid: empty selection";
            if (countA == 0 || countB == 0)
                return "Warning: Search Set currently matches no items";
            return null;
        }
    }

    public sealed class ProgressReporter
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Stopwatch stageClock = Stopwatch.StartNew();
        public void NewStage() => stageClock.Restart();
        public string Remaining(long done, long? total)
        {
            if (!total.HasValue || done < 2 || stageClock.Elapsed.TotalSeconds < 1) return "Estimating remaining time…";
            var seconds = Math.Max(0, stageClock.Elapsed.TotalSeconds * (total.Value - done) / done);
            var estimate = TimeSpan.FromSeconds(seconds);
            return "ETA " + DateTime.Now.Add(estimate).ToString("t") + " · approx. " + estimate.ToString(@"hh\:mm\:ss") + " left";
        }
        public string Describe(string stage, long done, long? total)
        {
            string count = total.HasValue ? $"{done:N0}/{total:N0} ({(total.Value == 0 ? 100 : Math.Min(100, 100.0 * done / total.Value)):0}%)" : $"{done:N0} items · total unknown";
            return $"{stage} · {count}\r\nElapsed {clock.Elapsed:hh\\:mm\\:ss} · {Remaining(done, total)}";
        }
    }
}
