using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;

namespace NavPro.Backend.Services
{
    public sealed class CoordinationChange
    {
        public string TimeUtc { get; set; }
        public string User { get; set; }
        public string Tool { get; set; }
        public string Scope { get; set; }
        public string Before { get; set; }
        public string After { get; set; }
        public string Outcome { get; set; }
    }
    public sealed class CoordinationRevision
    {
        public string Name { get; set; }
        public string TimeUtc { get; set; }
        public Dictionary<string, string> Results { get; set; } = new Dictionary<string, string>();
    }
    public sealed class CoordinationHistory
    {
        public List<CoordinationChange> Entries { get; set; } = new List<CoordinationChange>();
        public List<CoordinationRevision> Revisions { get; set; } = new List<CoordinationRevision>();
    }
    /// <summary>Local, path-scoped NavPro journal and explicit snapshots; independent of native Undo.</summary>
    public static class CoordinationChangeLog
    {
        private static readonly ConditionalWeakTable<object, CoordinationHistory> Unsaved = new ConditionalWeakTable<object, CoordinationHistory>();
        public static string PersistenceError { get; private set; }
        private static string Key(object document)
        {
            var native = document as Document;
            if (native == null || string.IsNullOrWhiteSpace(native.FileName)) return null;
            using (var sha = SHA256.Create()) return "history-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(native.FileName).ToUpperInvariant()))).Replace("-", "");
        }
        private static CoordinationHistory Load(object document)
        {
            string key = Key(document);
            return key == null ? Unsaved.GetOrCreateValue(document) : ReviewPreferences.Read<CoordinationHistory>(key);
        }
        private static void Save(object document, CoordinationHistory history)
        {
            string key = Key(document); if (key != null) ReviewPreferences.Write(key, history);
        }
        public static void Record(object document, string tool, string scope, string before, string after, string outcome = "Completed")
        {
            if (document == null) return;
            try
            {
                var history = Load(document);
                history.Entries.Add(new CoordinationChange { TimeUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), User = Environment.UserName,
                    Tool = tool, Scope = scope, Before = before, After = after, Outcome = outcome });
                Save(document, history); PersistenceError = null;
            }
            catch (Exception ex) { PersistenceError = "Journal could not be persisted: " + ex.Message; }
        }
        public static List<CoordinationChange> Read(object document)
        {
            if (document == null) return new List<CoordinationChange>();
            try { return Load(document).Entries.AsEnumerable().Reverse().ToList(); }
            catch (Exception ex) { PersistenceError = ex.Message; return new List<CoordinationChange>(); }
        }
        public static void CaptureRevision(object document, string name, Dictionary<string, string> results)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new InvalidOperationException("Enter a revision label of 1–100 characters.");
            var history = Load(document);
            if (history.Revisions.Any(r => r.Name == name.Trim())) throw new InvalidOperationException("That revision label already exists.");
            history.Revisions.Add(new CoordinationRevision { Name = name.Trim(), TimeUtc = DateTime.UtcNow.ToString("O"), Results = results ?? throw new ArgumentNullException(nameof(results)) });
            Save(document, history);
        }        public static string CompareLatest(object document)
        {
            var revisions = Load(document).Revisions;
            if (revisions.Count < 2) throw new InvalidOperationException("Capture at least two named revisions for this document path.");
            var a = revisions[revisions.Count - 2]; var b = revisions[revisions.Count - 1];
            var lines = new List<string> { a.Name + " → " + b.Name + " (result GUIDs; replacements appear as removed/added)" };
            foreach (string id in a.Results.Keys.Union(b.Results.Keys).OrderBy(v => v))
            {
                bool old = a.Results.TryGetValue(id, out var before), current = b.Results.TryGetValue(id, out var after);
                if (!old) lines.Add("ADDED: " + after); else if (!current) lines.Add("REMOVED: " + before);
                else if (before != after) lines.Add("CHANGED: " + before + " → " + after);
            }
            if (lines.Count == 1) lines.Add("No captured result-field differences.");
            return string.Join("\r\n", lines);
        }
        public static void Export(string path, IEnumerable<CoordinationChange> entries)
        {
            var rows = new List<IEnumerable<string>> { new[] { "Time UTC", "Windows user", "NavPro tool", "Scope", "Before", "After", "Outcome" } };
            rows.AddRange(entries.Select(row => new[] { row.TimeUtc, row.User, row.Tool, row.Scope, row.Before, row.After, row.Outcome })); ReviewCsv.Write(path, rows);
        }
    }
}
