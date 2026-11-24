using System;
using System.Collections.Generic;
using System.Linq;

namespace NavPro.Backend.Models
{
    public sealed class ReviewTest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public override string ToString() => Name + "  ·  " + Count + " results";
    }

    /// <summary>Detached data only. No native handles may enter AI/network or geometry processing.</summary>
    public sealed class ReviewClash
    {
        public string TestId, TestName, Id, Name, Description, Status, TestType, ParentGroup;
        public string ItemA, ItemB, KeyA, KeyB, ModelA, ModelB, Units;
        public double X, Y, Z, Distance;
        public Dictionary<string, string> PropertiesA { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> PropertiesB { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Identity => TestId + "/" + Id;
    }

    public sealed class ReviewSnapshot
    {
        public long Revision { get; internal set; }
        public string Units { get; internal set; }
        public List<string> TestIds { get; } = new List<string>();
        public List<ReviewClash> Clashes { get; } = new List<ReviewClash>();
        internal string Signature;
    }

    public enum ClashGroupingMode { SharedElementA, SharedElementB, ModelPair, PropertyA, PropertyB, SpatialGrid, DistanceCluster, Level, Zone }
    public sealed class ReviewGroup
    {
        public string TestId, TestName, Name;
        public List<string> ResultIds { get; } = new List<string>();
        public Dictionary<string, string> Reasons { get; } = new Dictionary<string, string>();
    }
    public sealed class ClashGroupingPlan
    {
        public ReviewSnapshot Snapshot { get; set; }
        public List<ReviewGroup> Groups { get; } = new List<ReviewGroup>();
        public int GroupedResults => Groups.Sum(group => group.ResultIds.Count);
        public int Skipped => Snapshot.Clashes.Count - GroupedResults;
    }
    public sealed class DescriptionDraft
    {
        public ReviewClash Source { get; set; }
        public string Text { get; set; }
    }
}
