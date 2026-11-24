// Test-only adapters. These cannot prove native test-copy semantics or transaction behavior.
using System;
using System.Collections.Generic;
namespace Autodesk.Navisworks.Api
{
    public enum Units { Meters, Millimeters, Centimeters, Feet, Inches }
    public static class UnitConversion
    {
        private static double Meters(Units unit) => unit == Units.Millimeters ? .001 : unit == Units.Centimeters ? .01 : unit == Units.Feet ? .3048 : unit == Units.Inches ? .0254 : 1;
        public static double ScaleFactor(Units from, Units to) => Meters(from) / Meters(to);
    }
    public class SavedItem : IDisposable
    {
        public Guid Guid { get; set; } = Guid.NewGuid();
        public string DisplayName { get; set; }
        public virtual SavedItem CreateCopy() => (SavedItem)MemberwiseClone();
        public void Dispose() { }
    }
    public class GroupItem : SavedItem { public List<SavedItem> Children { get; } = new List<SavedItem>(); }
    public static class Application { public static Document ActiveDocument { get; set; } }
    public sealed class Document
    {
        public string FileName { get; set; }
        public Units Units { get; set; } = Units.Meters;
        public object Clash { get; set; } = new Clash.DocumentClash();
        public int Transactions;
        public Transaction BeginTransaction(string name) { Transactions++; return new Transaction(); }
    }
    public sealed class Transaction : IDisposable { public void Commit() { } public void Dispose() { } }
}
namespace Autodesk.Navisworks.Api.Clash
{
    public enum ClashTestType { Hard, HardConservative, Clearance, Duplicate, Custom }
    public sealed class ClashTest : GroupItem
    {
        public ClashTestType TestType { get; set; }
        public double Tolerance { get; set; }
        public bool MergeComposites { get; set; }
        public string Rules = "Preserved rules";
    }
    public sealed class DocumentClash { public DocumentClashTests TestsData { get; } = new DocumentClashTests(); }
    public sealed class DocumentClashTests
    {
        public List<SavedItem> Tests { get; } = new List<SavedItem>();
        public int EditCount;
        public int RunCount;
        public List<string> RunNames { get; } = new List<string>();
        public void TestsEditTestFromCopy(ClashTest original, ClashTest copy)
        {
            if (ReferenceEquals(original, copy)) throw new Exception("Expected an editable copy");
            EditCount++; original.TestType = copy.TestType; original.Tolerance = copy.Tolerance; original.MergeComposites = copy.MergeComposites;
        }
        public void TestsRunTest(ClashTest test) { RunCount++; RunNames.Add(test.DisplayName); }
    }
}
