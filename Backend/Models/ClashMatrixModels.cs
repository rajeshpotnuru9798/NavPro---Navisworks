using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace NavPro.Backend.Models
{
    public sealed class ClashSetChoice
    {
        public int Index { get; set; }
        public SelectionSet Set { get; set; }
        public ModelItemCollection ExplicitItems { get; set; }
        public string SourceKind { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string StablePath { get; set; }
        public string StableKey => SourceKind + "|" + (StablePath ?? Path);
        public bool IsSearchSet { get; set; }
        public int ItemCount { get; set; }
        public string Label => Name + "  ·  " + (SourceKind.StartsWith("Tree", System.StringComparison.Ordinal) ? "Model item" : (IsSearchSet ? "Search Set" : "Selection Set")) + "  ·  " + ItemCount.ToString("N0") + " items";
        public override string ToString() => SourceKind == "TreeStandard" ? Name + "  ·  Loaded model"
            : Path + (SourceKind.StartsWith("Tree", System.StringComparison.Ordinal) ? "  ·  Model item" : (IsSearchSet ? "  ·  Search" : "  ·  Selection"));
    }

    public sealed class ClashMatrixPair
    {
        public ClashSetChoice A { get; set; }
        public ClashSetChoice B { get; set; }
        public string TestName { get; set; }
        public ClashTestType Type { get; set; }
        public double Tolerance { get; set; }
        public PrimitiveTypes PrimitiveTypesA { get; set; } = PrimitiveTypes.Triangles;
        public PrimitiveTypes PrimitiveTypesB { get; set; } = PrimitiveTypes.Triangles;
        public bool SelfIntersect { get; set; }
        public bool CompositeObjectClashing { get; set; }
        public string Validation { get; set; }
        public bool Ready => Validation == "Ready" || Validation.StartsWith("Warning:", System.StringComparison.Ordinal);
    }

    public sealed class ClashMatrixSnapshot
    {
        public List<string> Models { get; } = new List<string>();
        public List<ClashSetChoice> Sets { get; } = new List<ClashSetChoice>();
        public List<ClashTest> ExistingTests { get; } = new List<ClashTest>();
        public Dictionary<string, int> ResultStatuses { get; } = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        public int TestsRun { get; set; }
        public int ResultCount => ResultStatuses.Values.Sum();
    }
}
