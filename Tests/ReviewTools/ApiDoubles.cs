// Detached API contract doubles. They cannot establish Autodesk geometry, undo or native transaction semantics.
using System;
using System.Collections.Generic;
using System.Linq;
namespace Autodesk.Navisworks.Api
{
    public enum Units { Meters, Millimeters, Feet, Inches }
    public enum PrimitiveTypes { Triangles, Lines, Points }
    public static class UnitConversion
    { public static double ScaleFactor(Units from, Units to) => Factor(from) / Factor(to); private static double Factor(Units unit) => unit == Units.Millimeters ? .001 : unit == Units.Feet ? .3048 : unit == Units.Inches ? .0254 : 1; }
    public class SavedItem : IDisposable
    {
        public Guid Guid { get; set; } = Guid.NewGuid(); public string DisplayName { get; set; }
        public virtual SavedItem CreateCopy() => (SavedItem)MemberwiseClone(); public void Dispose() { }
    }
    public class GroupItem : SavedItem { public List<SavedItem> Children { get; } = new List<SavedItem>(); }
    public class SelectionSet : SavedItem { public int TestItemCount = 1; }
    public sealed class ModelItemCollection : List<ModelItem>, IDisposable { public void Dispose() { } }
    public sealed class Point3D { public double X, Y, Z; public Point3D(double x, double y, double z) { X = x; Y = y; Z = z; } }
    public sealed class Model { public string FileName { get; set; } }
    public sealed class ModelItem
    {
        public string DisplayName, TestPath;
        public Model Model { get; set; } = new Model();
        public List<PropertyCategory> PropertyCategories { get; } = new List<PropertyCategory>();
    }
    public sealed class PropertyCategory { public string DisplayName; public List<DataProperty> Properties { get; } = new List<DataProperty>(); }
    public sealed class DataProperty { public string DisplayName; public VariantData Value; }
    public sealed class VariantData
    { public string Text; public bool IsNone => Text == null; public bool IsDisplayString => true; public string ToDisplayString() => Text; public override string ToString() => Text; }
    public sealed class SavedItemChangedEventArgs : EventArgs { }
    public sealed class ChangedPart
    { public event EventHandler<SavedItemChangedEventArgs> Changed; public void Touch() => Changed?.Invoke(this, new SavedItemChangedEventArgs()); }
    public sealed class ModelsPart
    {
        public event EventHandler<EventArgs> CollectionChanged, ModelTransformChanged, ModelItemPropertiesChanged;
        public DocumentParts.ModelItemPathId CreatePathId(ModelItem item) => new DocumentParts.ModelItemPathId { ModelIndex = 0, PathId = item.TestPath };
        public void Transform() => ModelTransformChanged?.Invoke(this, EventArgs.Empty);
        public void Touch() { CollectionChanged?.Invoke(this, EventArgs.Empty); ModelItemPropertiesChanged?.Invoke(this, EventArgs.Empty); }
    }
    public sealed class SelectionPart { public void CopyFrom(IEnumerable<ModelItem> items) { } }
    public sealed class Viewpoint : IDisposable { public void Dispose() { } }
    public sealed class ViewpointPart { public void CopyFrom(Viewpoint view) { } }
    public static class Application { public static Document ActiveDocument { get; set; } }
    public sealed class Document
    {
        public string FileName { get; set; }
        public bool IsDisposed { get; set; }
        public Units Units { get; set; } = Units.Meters;
        public Clash.DocumentClash Clash { get; } = new Clash.DocumentClash();
        public ModelsPart Models { get; } = new ModelsPart();
        public ChangedPart SelectionSets { get; } = new ChangedPart();
        public SelectionPart CurrentSelection { get; } = new SelectionPart();
        public ViewpointPart CurrentViewpoint { get; } = new ViewpointPart();
        public event EventHandler<EventArgs> UnitsChanged;
        public int Transactions;
        public Transaction BeginTransaction(string name) { Transactions++; return new Transaction(); }
        public void ChangeUnits(Units unit) { Units = unit; UnitsChanged?.Invoke(this, EventArgs.Empty); }
    }
    public sealed class Transaction : IDisposable { public void Commit() { } public void Dispose() { } }
}
namespace Autodesk.Navisworks.Api.DocumentParts { public sealed class ModelItemPathId { public int ModelIndex; public string PathId; } }
namespace Autodesk.Navisworks.Api.Clash
{
    public enum ClashTestType { Hard, HardConservative, Clearance, Duplicate }
    public enum ClashResultStatus { New, Active, Reviewed, Approved, Resolved }
    public sealed class ClashTest : GroupItem { public ClashTestType TestType; public double Tolerance; }
    public sealed class ClashResult : SavedItem
    {
        public string Description; public ClashResultStatus Status; public double Distance;
        public Point3D Center = new Point3D(0, 0, 0);
        public ModelItem Item1, Item2, CompositeItem1, CompositeItem2;
    }
    public sealed class ClashResultGroup : GroupItem { }
    public sealed class DocumentClash { public DocumentClashTests TestsData { get; } = new DocumentClashTests(); }
    public sealed class DocumentClashTests
    {
        public List<SavedItem> Tests { get; } = new List<SavedItem>();
        public int MoveCount, EditCount, RunCount;
        public event EventHandler<SavedItemChangedEventArgs> Changed;
        private void Touch() => Changed?.Invoke(this, new SavedItemChangedEventArgs());
        public void TestsAddCopy(GroupItem parent, SavedItem item)
        { parent.Children.Add(new ClashResultGroup { DisplayName = item.DisplayName }); Touch(); }
        public void TestsMove(GroupItem from, int sourceIndex, GroupItem to, int targetIndex)
        { var item = from.Children[sourceIndex]; from.Children.RemoveAt(sourceIndex); to.Children.Insert(targetIndex, item); MoveCount++; Touch(); }
        public void TestsEditResultDescription(ClashResult result, string value) { result.Description = value; EditCount++; Touch(); }
        public void TestsEditDisplayName(SavedItem item, string value) { item.DisplayName = value; EditCount++; Touch(); }
        public void TestsRunTest(ClashTest test) { RunCount++; Touch(); }
        public Viewpoint TestsViewpointForResult(ClashResult result) => new Viewpoint();
    }
}
namespace NavPro.Backend.Services
{
    // The existing selection-source/creation adapter is separately compiled against real Autodesk DLLs.
    public sealed class ClashMatrixService
    {
        public static List<NavPro.Backend.Models.ClashSetChoice> Available = new List<NavPro.Backend.Models.ClashSetChoice>();
        private readonly Autodesk.Navisworks.Api.Document document;
        public ClashMatrixService(Autodesk.Navisworks.Api.Document document) { this.document = document; }
        public NavPro.Backend.Models.ClashMatrixSnapshot ReadSnapshot()
        {
            var data = new NavPro.Backend.Models.ClashMatrixSnapshot(); data.Sets.AddRange(Available);
            data.ExistingTests.AddRange(document.Clash.TestsData.Tests.OfType<Autodesk.Navisworks.Api.Clash.ClashTest>()); return data;
        }
        public int CountItems(NavPro.Backend.Models.ClashSetChoice set) => set.Set.TestItemCount;
        public int CreateTests(IList<NavPro.Backend.Models.ClashMatrixPair> pairs, bool recordChangeLog = true)
        { foreach (var pair in pairs) document.Clash.TestsData.Tests.Add(new Autodesk.Navisworks.Api.Clash.ClashTest { DisplayName = pair.TestName, TestType = pair.Type, Tolerance = pair.Tolerance }); return pairs.Count; }
    }
}
