using System.Collections.Generic;

namespace NavPro.Backend.Models
{
    public sealed class ClashDashboardOptions
    {
        public string ProjectName { get; set; }
        public string DisplayUnits { get; set; }
        public double UnitScale { get; set; } = 1.0;
        public bool IncludeImages { get; set; }
        public bool IncludeAllResults { get; set; } = true;
        public bool IncludeTabularHtml { get; set; }
        public bool IncludeXml { get; set; }
        public bool IncludeExcel { get; set; }
        public HashSet<string> IncludedStatuses { get; set; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ClashDashboardSnapshot
    {
        public string ReportId { get; set; }
        public string ProjectName { get; set; }
        public string ExportedAt { get; set; }
        public string ExportedAtIso { get; set; }
        public string DisplayUnits { get; set; }
        public string SourceUnits { get; set; }
        public string[] Statuses { get; set; }
        public int AllResultCount { get; set; }
        public Dictionary<string, int> AllStatusCounts { get; set; } = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        public List<ClashDashboardModelFile> Files { get; set; } = new List<ClashDashboardModelFile>();
        public List<ClashDashboardTest> Tests { get; set; } = new List<ClashDashboardTest>();
        public List<ClashDashboardResult> Results { get; set; } = new List<ClashDashboardResult>();
    }

    public sealed class ClashDashboardModelFile
    {
        public string Name { get; set; }
        public string ModifiedDate { get; set; }
    }

    public sealed class ClashDashboardTest
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public double Tolerance { get; set; }
        public string Status { get; set; }
        public string LastRun { get; set; }
        public string Priority { get; set; }
        public string DisciplineA { get; set; }
        public string DisciplineB { get; set; }
        public string NameValidation { get; set; }
        public bool IsNameValid { get; set; }
        public int ClashCount { get; set; }
        public Dictionary<string, int> ClashStatusCounts { get; set; } = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ClashDashboardResult
    {
        public string ReportId { get; set; }
        public string Id { get; set; }
        public string TestName { get; set; }
        public string Priority { get; set; }
        public string NameValidation { get; set; }
        public bool IsNameValid { get; set; }
        public string TestType { get; set; }
        public double TestTolerance { get; set; }
        public string TestStatus { get; set; }
        public string ClashName { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
        public string ItemA { get; set; }
        public string ItemB { get; set; }
        public string ModelA { get; set; }
        public string ModelB { get; set; }
        public string DisciplineA { get; set; }
        public string DisciplineB { get; set; }
        public string Viewpoint { get; set; }
        public string DisplayUnits { get; set; }
        public double Distance { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string AssignedTo { get; set; }
        public string ApprovedBy { get; set; }
        public string CreatedAt { get; set; }
        public string ApprovedAt { get; set; }
        public string ImageData { get; set; }
        public string ImageNote { get; set; }
    }
}
