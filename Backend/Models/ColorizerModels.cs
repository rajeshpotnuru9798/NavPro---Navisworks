using Autodesk.Navisworks.Api;

namespace NavPro.Backend.Models
{
    public sealed class ColorizerTarget
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public string Label { get; set; }
        public Model Model { get; set; }
        public SelectionSet Set { get; set; }
        public override string ToString() => Label ?? Name ?? string.Empty;
    }

    public sealed class ColorizerColorRow
    {
        public string TargetType { get; set; }
        public string TargetName { get; set; }
        public string TargetKey { get; set; }
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public string Hex => "#" + Red.ToString("X2") + Green.ToString("X2") + Blue.ToString("X2");
    }
}
