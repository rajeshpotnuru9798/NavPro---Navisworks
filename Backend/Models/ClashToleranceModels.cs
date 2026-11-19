using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace NavPro.Backend.Models
{
    public sealed class ClashToleranceItem
    {
        public string Id { get; }
        public string Name { get; }
        public ClashTestType Type { get; }
        public double Tolerance { get; }
        public bool CompositeObjectClashing { get; }
        public Units DocumentUnits { get; }
        public ClashToleranceItem(string id, string name, ClashTestType type, double tolerance, Units documentUnits, bool compositeObjectClashing = false)
        { Id = id; Name = name; Type = type; Tolerance = tolerance; DocumentUnits = documentUnits; CompositeObjectClashing = compositeObjectClashing; }
    }

    public sealed class ClashToleranceChange
    {
        public ClashToleranceItem Original { get; }
        public ClashTestType Type { get; }
        public double Tolerance { get; }
        public bool CompositeObjectClashing { get; }
        public bool Changed => Original.Type != Type || Original.Tolerance != Tolerance || Original.CompositeObjectClashing != CompositeObjectClashing;
        public ClashToleranceChange(ClashToleranceItem original, ClashTestType type, double tolerance, bool compositeObjectClashing)
        { Original = original; Type = type; Tolerance = tolerance; CompositeObjectClashing = compositeObjectClashing; }
    }
}
