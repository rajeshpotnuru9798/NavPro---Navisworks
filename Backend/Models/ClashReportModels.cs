namespace NavPro.Backend.Models
{
    public sealed class ClashReportRow
    {
        public string TestName { get; set; }
        public string TestType { get; set; }
        public double TestTolerance { get; set; }
        public string TestStatus { get; set; }
        public string ClashName { get; set; }
        public string ClashStatus { get; set; }
        public string Description { get; set; }
        public string ItemA { get; set; }
        public string ItemB { get; set; }
        public double Distance { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double CenterZ { get; set; }
        public string ApprovedBy { get; set; }
        public string AssignedTo { get; set; }
        public string CreatedTime { get; set; }
        public string ApprovedTime { get; set; }
        public string ImagePath { get; set; }
        public string ImageNote { get; set; }
    }
}
