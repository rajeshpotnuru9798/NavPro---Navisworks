using System.Collections.Generic;

namespace NavPro.Backend.Models
{
    public sealed class ClashApprovalTest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Total { get; set; }
        public Dictionary<string, int> Statuses { get; } = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        public override string ToString() => Name + "  ·  " + Total.ToString("N0") + " results";
    }

    public sealed class ClashStatusUpdatePreview
    {
        public int SelectedTests { get; set; }
        public int ToChange { get; set; }
        public int AlreadyAtStatus { get; set; }
        public int OtherStatuses { get; set; }
        public string TargetStatus { get; set; }
        public Dictionary<string, int> CurrentStatuses { get; } = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ClashStatusUpdateResult
    {
        public int Updated { get; set; }
        public int TestsChanged { get; set; }
        public int Skipped { get; set; }
    }
}
