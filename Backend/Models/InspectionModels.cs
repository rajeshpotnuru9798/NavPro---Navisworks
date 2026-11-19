using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;

namespace NavPro.Backend.Models
{
    public sealed class CategoryOption
    {
        public string InternalName { get; set; }
        public string DisplayName { get; set; }
        public override string ToString() => DisplayName;
    }
    public sealed class PropertyOption
    {
        public PropertyKey Key { get; set; }
        public string Label { get; set; }
        public string CategoryLabel { get; set; }
        public string PropertyLabel { get; set; }
        public long Occurrences { get; set; }
        public override string ToString() => $"{PropertyLabel} ({Occurrences:N0} items)";
    }
    public sealed class ValueGroup
    {
        public CriteriaSpec Criteria { get; set; }
        public VariantData NativeValue { get; set; }
        public string Display { get; set; }
        public int Matches { get; set; }
        public long Occurrences { get; set; }
        public bool MatchFilenameSuffix { get; set; }
    }
    public sealed class InspectionResult
    {
        public List<ValueGroup> Values { get; } = new List<ValueGroup>();
        public long Items, Missing, Blank, Unsupported;
        public string Summary => $"Scanned {Items:N0} items | Missing property {Missing:N0} | Blank {Blank:N0} | Unsupported values {Unsupported:N0}";
    }
}
