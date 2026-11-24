using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NavPro.Backend.Core
{
    public static class ReviewCsv
    {
        public static List<List<string>> Read(string text)
        {
            var output = new List<List<string>>(); var row = new List<string>(); var value = new StringBuilder(); bool quoted = false, closed = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { value.Append('"'); i++; } else { quoted = false; closed = true; } }
                    else value.Append(c);
                }
                else if (c == '"' && value.Length == 0 && !closed) quoted = true;
                else if (c == ',' || c == '\r' || c == '\n')
                {
                    row.Add(value.ToString()); value.Clear(); closed = false;
                    if (c != ',') { if (row.Any(v => v.Length != 0)) output.Add(row); row = new List<string>(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
                }
                else { if (closed || c == '"') throw new InvalidOperationException("Malformed CSV quoting."); value.Append(c); }
            }
            if (quoted) throw new InvalidOperationException("Unclosed CSV quote.");
            if (value.Length > 0 || row.Count > 0 || closed) { row.Add(value.ToString()); output.Add(row); }
            return output;
        }
        public static void Write(string path, IEnumerable<IEnumerable<string>> rows)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
                foreach (var row in rows) writer.WriteLine(string.Join(",", row.Select(Escape)));
        }
        private static string Escape(string value)
        {
            value = value ?? "";
            // Prevent model-controlled labels being interpreted as spreadsheet formulae.
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && ("=+@".IndexOf(trimmed[0]) >= 0 || (trimmed[0] == '-'
                && !double.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)))) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
