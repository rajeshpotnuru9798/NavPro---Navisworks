using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using NavApplication = Autodesk.Navisworks.Api.Application;

namespace NavPro.Backend.Services
{
    public sealed class ModelInspectionService : IDisposable
    {
        private readonly Document document;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        private bool invalid;
        public ModelInspectionService(Document document)
        {
            this.document = document;
            document.Models.CollectionChanged += ModelsChanged;
            document.Models.ModelItemPropertiesChanged += PropertiesChanged;
            NavApplication.ActiveDocumentChanged += ActiveChanged;
        }
        private void ModelsChanged(object sender, EventArgs e) => invalid = true;
        private void PropertiesChanged(object sender, EventArgs e) => invalid = true;
        private void ActiveChanged(object sender, EventArgs e) => invalid = true;
        public void Check()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Navisworks access must stay on its UI thread.");
            if (invalid || NavApplication.ActiveDocument != document) throw new InvalidOperationException("The model changed. Close SearchSetCreater and reopen it to scan the current model.");
        }
        public async Task<List<PropertyOption>> Discover(CancellationToken cancel, Action<long, long?> progress)
            => await Discover(null, cancel, progress);

        public async Task<List<PropertyOption>> Discover(Model selectedModel, CancellationToken cancel, Action<long, long?> progress)
        {
            Check();
            var found = new Dictionary<string, PropertyOption>(StringComparer.Ordinal);
            long count = 0;
            var slice = Stopwatch.StartNew();
            var items = selectedModel == null ? document.Models.RootItemDescendantsAndSelf : selectedModel.RootItem.DescendantsAndSelf;
            foreach (var item in items)
            {
                Check(); cancel.ThrowIfCancellationRequested(); count++;
                var seen = new HashSet<string>();
                foreach (var category in item.PropertyCategories)
                    foreach (var property in category.Properties)
                    {
                        if (string.IsNullOrWhiteSpace(category.Name) || string.IsNullOrWhiteSpace(property.Name)) continue;
                        var key = new PropertyKey(category.Name, property.Name);
                        if (!found.TryGetValue(key.Id, out var option))
                        {
                            string categoryLabel = string.IsNullOrWhiteSpace(category.DisplayName) ? category.Name : category.DisplayName;
                            string propertyLabel = string.IsNullOrWhiteSpace(property.DisplayName) ? property.Name : property.DisplayName;
                            found.Add(key.Id, option = new PropertyOption
                            {
                                Key = key,
                                Label = categoryLabel + " / " + propertyLabel,
                                CategoryLabel = categoryLabel,
                                PropertyLabel = propertyLabel
                            });
                        }
                        if (seen.Add(key.Id)) option.Occurrences++;
                    }
                if (slice.ElapsedMilliseconds >= 35)
                {
                    progress(count, null); await Task.Delay(15, cancel); Check(); slice.Restart();
                }
            }
            progress(count, count);
            return found.Values.OrderByDescending(p => ModelNumberPropertyPolicy.Score(p.Label)).ThenBy(p => p.Label).ToList();
        }
        public async Task<InspectionResult> Inspect(PropertyKey key, CancellationToken cancel, Action<long, long?> progress)
            => await Inspect((Model)null, key, cancel, progress, false);

        public async Task<InspectionResult> Inspect(PropertyKey key, CancellationToken cancel, Action<long, long?> progress, bool useFileNameValue)
            => await Inspect((Model)null, key, cancel, progress, useFileNameValue);

        public async Task<InspectionResult> Inspect(Model selectedModel, PropertyKey key, CancellationToken cancel, Action<long, long?> progress)
            => await Inspect(selectedModel, key, cancel, progress, false);

        public async Task<InspectionResult> Inspect(Model selectedModel, PropertyKey key, CancellationToken cancel, Action<long, long?> progress, bool useFileNameValue)
        {
            Check();
            var result = new InspectionResult();
            var groups = new Dictionary<string, ValueGroup>(StringComparer.Ordinal);
            var slice = Stopwatch.StartNew();
            var items = selectedModel == null ? document.Models.RootItemDescendantsAndSelf : selectedModel.RootItem.DescendantsAndSelf;
            foreach (var item in items)
            {
                Check(); cancel.ThrowIfCancellationRequested(); result.Items++;
                bool present = false, nonblank = false, unsupported = false;
                var seenValues = new HashSet<string>(StringComparer.Ordinal);
                foreach (var category in item.PropertyCategories.Where(c => c.Name == key.Category))
                    foreach (var property in category.Properties.Where(p => p.Name == key.Property))
                    {
                        present = true;
                        try
                        {
                            var group = SearchCriteriaFactory.Read(key, property.Value, useFileNameValue);
                            if (group != null)
                            {
                                nonblank = true;
                                if (!groups.TryGetValue(group.Criteria.ValueKey, out var existing)) groups.Add(group.Criteria.ValueKey, existing = group);
                                if (seenValues.Add(group.Criteria.ValueKey)) existing.Occurrences++;
                            }
                        }
                        catch (NotSupportedException) { unsupported = true; }
                    }
                if (!present) result.Missing++;
                else if (unsupported) result.Unsupported++;
                else if (!nonblank) result.Blank++;
                if (slice.ElapsedMilliseconds >= 35)
                {
                    progress(result.Items, null); await Task.Delay(15, cancel); Check(); slice.Restart();
                }
            }
            result.Values.AddRange(groups.Values.OrderBy(g => g.Display, StringComparer.Ordinal));
            progress(result.Items, result.Items);
            return result;
        }

        public async Task<InspectionResult> Inspect(IEnumerable<Model> selectedModels, PropertyKey key, CancellationToken cancel, Action<long, long?> progress)
            => await Inspect(selectedModels, key, cancel, progress, false);

        public async Task<InspectionResult> Inspect(IEnumerable<Model> selectedModels, PropertyKey key, CancellationToken cancel, Action<long, long?> progress, bool useFileNameValue)
        {
            if (selectedModels == null) throw new ArgumentNullException(nameof(selectedModels));
            var models = selectedModels.ToList();
            if (models.Count == 0) throw new InvalidOperationException("Select at least one loaded model.");
            var combined = new InspectionResult();
            var values = new Dictionary<string, ValueGroup>(StringComparer.Ordinal);
            foreach (var model in models)
            {
                Check(); cancel.ThrowIfCancellationRequested();
                var part = await Inspect(model, key, cancel, (done, total) => progress(combined.Items + done, null), useFileNameValue);
                combined.Items += part.Items;
                combined.Missing += part.Missing;
                combined.Blank += part.Blank;
                combined.Unsupported += part.Unsupported;
                foreach (var value in part.Values)
                    if (!values.TryGetValue(value.Criteria.ValueKey, out var existing)) values.Add(value.Criteria.ValueKey, existing = value);
                    else existing.Occurrences += value.Occurrences;
            }
            combined.Values.AddRange(values.Values.OrderBy(value => value.Display, StringComparer.Ordinal));
            progress(combined.Items, combined.Items);
            return combined;
        }
        public void Dispose()
        {
            document.Models.CollectionChanged -= ModelsChanged;
            document.Models.ModelItemPropertiesChanged -= PropertiesChanged;
            NavApplication.ActiveDocumentChanged -= ActiveChanged;
        }
    }
}
