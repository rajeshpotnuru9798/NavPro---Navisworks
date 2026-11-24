using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
using NavApplication = Autodesk.Navisworks.Api.Application;

namespace NavPro.Backend.Services
{
    /// <summary>Applies permanent model appearance colors on the Navisworks host UI thread.</summary>
    public sealed class ColorizerService
    {
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;

        public ColorizerService(Document document) { this.document = document ?? throw new ArgumentNullException(nameof(document)); }

        public List<ColorizerTarget> ReadTargets(bool models)
        {
            CheckHost();
            if (models)
            {
                var targets = document.Models.Select(model => new ColorizerTarget
                {
                    Type = "Model",
                    Name = string.IsNullOrWhiteSpace(model.FileName) ? "Unnamed model" : Path.GetFileName(model.FileName),
                    Key = model.FileName ?? string.Empty,
                    Model = model
                }).ToList();
                foreach (var group in targets.GroupBy(target => target.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
                    foreach (var target in group) target.Label = target.Name + "  ·  " + target.Key;
                foreach (var target in targets.Where(target => target.Label == null)) target.Label = target.Name;
                return targets.OrderBy(target => target.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            }

            var sets = Flatten(document.SelectionSets.RootItem.Children).OfType<SelectionSet>()
                .Select(set => new ColorizerTarget { Type = "Set", Name = set.DisplayName ?? string.Empty, Key = SavedItemPath(set), Set = set })
                .OrderBy(target => target.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(target => target.Key, StringComparer.CurrentCultureIgnoreCase).ToList();
            foreach (var group in sets.GroupBy(target => target.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
                foreach (var target in group) target.Label = target.Name + "  ·  " + target.Key;
            foreach (var target in sets.Where(target => target.Label == null)) target.Label = target.Name;
            return sets;
        }

        public int Apply(IEnumerable<ColorizerColorRow> assignments)
        {
            CheckHost();
            var rows = (assignments ?? Enumerable.Empty<ColorizerColorRow>()).ToList();
            if (rows.Count == 0) throw new InvalidOperationException("No color assignments were selected.");
            int colored = 0;
            foreach (var row in rows)
            {
                CheckHost();
                var target = ResolveTarget(row);
                var items = ReadItems(target);
                if (items.Count == 0) continue;
                try { document.Models.OverridePermanentColor(items,
                    Autodesk.Navisworks.Api.Color.FromByteRGB(row.Red, row.Green, row.Blue)); }
                catch (Exception ex) { CoordinationChangeLog.Record(document, "Colorizer", target.Name, colored + " target(s) completed before this target", ex.Message + "; inspect native appearance", "Incomplete / possible partial changes"); throw; }
                colored++;
                CoordinationChangeLog.Record(document, "Colorizer", target.Name, "Previous appearance not captured", "Permanent RGB override " + row.Red + "," + row.Green + "," + row.Blue);
            }
            CheckHost();
            return colored;
        }

        public string ModeLabel => "permanent Navisworks appearance overrides";

        private ColorizerTarget ResolveTarget(ColorizerColorRow row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.TargetName)) throw new InvalidOperationException("The workbook contains a row with no target name.");
            var targets = ReadTargets(string.Equals(row.TargetType, "Model", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(row.TargetType, "Model", StringComparison.OrdinalIgnoreCase) == false
                && !string.Equals(row.TargetType, "Set", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Workbook target type must be Model or Set for '" + row.TargetName + "'.");
            var byKey = targets.Where(target => !string.IsNullOrWhiteSpace(row.TargetKey)
                && string.Equals(target.Key, row.TargetKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(target.Name, row.TargetName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (byKey.Count == 1) return byKey[0];
            var byName = targets.Where(target => string.Equals(target.Name, row.TargetName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (byName.Count == 1) return byName[0];
            if (byName.Count > 1) throw new InvalidOperationException("Target name '" + row.TargetName + "' is ambiguous. Export a fresh workbook so its Target Key can identify the exact target.");
            throw new InvalidOperationException("No current " + row.TargetType + " matches workbook target '" + row.TargetName + "'. Refresh the list and check the workbook names.");
        }

        private List<ModelItem> ReadItems(ColorizerTarget target)
        {
            if (target.Model != null)
                return target.Model.RootItem == null ? new List<ModelItem>() : new List<ModelItem> { target.Model.RootItem };
            if (target.Set == null) throw new InvalidOperationException("The color target is no longer available. Refresh the target list.");
            if (target.Set.HasSearch)
            {
                using (var search = new Search(target.Set.Search))
                using (var found = search.FindAll(document, false)) return found.ToList();
            }
            using (var selected = target.Set.GetSelectedItems(document)) return selected.ToList();
        }

        private static IEnumerable<SavedItem> Flatten(IEnumerable<SavedItem> items)
        {
            foreach (var item in items)
            {
                if (item is SelectionSet) yield return item;
                else if (item is GroupItem group)
                    foreach (var child in Flatten(group.Children)) yield return child;
            }
        }

        private static string SavedItemPath(SavedItem item)
        {
            var names = new Stack<string>();
            for (var current = item; current != null; current = current.Parent) names.Push(current.DisplayName ?? string.Empty);
            return string.Join(" / ", names);
        }

        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks color operations must run on the host UI thread.");
            if (NavApplication.ActiveDocument != document) throw new InvalidOperationException("The active document changed. Close Colorizer and reopen it for the current document.");
        }
    }
}
