using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NavPro.Backend.Models;

namespace NavPro.Backend.Core
{
    public static class RenamePatterns
    {
        private static readonly Regex Token = new Regex(@"\{([^{}]+)\}", RegexOptions.CultureInvariant);
        public static List<ClashRenameItem> Build(IEnumerable<ClashRenameItem> items, string pattern, string prefix, int start, int step, int digits)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 1000) throw new InvalidOperationException("Enter a naming pattern of 1–1000 characters.");
            if (start < 0 || step < 1 || digits < 1 || digits > 9) throw new InvalidOperationException("Use a nonnegative start, positive step and 1–9 number digits.");
            string literal = Token.Replace(pattern, "");
            if (literal.Contains("{") || literal.Contains("}")) throw new InvalidOperationException("The naming pattern has unmatched braces.");
            var result = new List<ClashRenameItem>();
            foreach (var item in items)
            {
                long number = (long)start + (long)result.Count * step;
                if (number > int.MaxValue) throw new InvalidOperationException("Automatic numbering exceeds 2,147,483,647. Reduce the start or step.");
                var parts = ClashTestNameParser.Parse(item.CurrentName);
                string name = Token.Replace(pattern, match =>
                {
                    string token = match.Groups[1].Value;
                    switch (token)
                    {
                        case "name": return item.CurrentName ?? "";
                        case "prefix": return prefix ?? "";
                        case "n": return number.ToString("D" + digits, CultureInfo.InvariantCulture);
                        case "priority": case "a": case "b":
                            if (!parts.IsValid) throw new InvalidOperationException("Name '" + item.CurrentName + "' must follow Priority_DisciplineA_VS_DisciplineB to use {priority}, {a} or {b}.");
                            return token == "priority" ? parts.Priority : token == "a" ? parts.DisciplineA : parts.DisciplineB;
                        default: throw new InvalidOperationException("Unknown pattern field {" + token + "}. Use {name}, {prefix}, {n}, {priority}, {a} or {b}.");
                    }
                }).Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl)) throw new InvalidOperationException("Each generated name must contain 1–255 printable characters.");
                result.Add(new ClashRenameItem { Id = item.Id, CurrentName = item.CurrentName, NewName = name });
            }
            if (result.Count == 0) throw new InvalidOperationException("Check at least one test to preview a pattern.");
            return result;
        }

        // A compact text diff: unchanged prefix/suffix with the changed span highlighted on each side.
        public static Tuple<int, int, int> Difference(string before, string after)
        {
            before = before ?? ""; after = after ?? "";
            int start = 0, suffix = 0;
            while (start < before.Length && start < after.Length && before[start] == after[start]) start++;
            while (suffix < before.Length - start && suffix < after.Length - start && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix]) suffix++;
            return Tuple.Create(start, before.Length - start - suffix, after.Length - start - suffix);
        }
    }
}
