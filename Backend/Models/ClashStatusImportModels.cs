using System.Collections.Generic;

namespace NavPro.Backend.Models
{
    public sealed class ClashStatusImportReport
    {
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public List<ClashStatusImportRecord> Records { get; } = new List<ClashStatusImportRecord>();
        public List<string> AvailableStatuses { get; } = new List<string>();
    }

    public sealed class ClashStatusImportRecord
    {
        public int LineNumber { get; set; }
        public string TestName { get; set; }
        public string ClashName { get; set; }
        public string Status { get; set; }
        public string ItemA { get; set; }
        public string ModelA { get; set; }
        public string ItemB { get; set; }
        public string ModelB { get; set; }
        public string ApprovedBy { get; set; }
        public string ApprovedAt { get; set; }
    }

    public enum ClashStatusImportDisposition
    {
        InvalidStatus,
        StatusNotSelected,
        IncompleteIdentity,
        NotFound,
        Ambiguous,
        AlreadyCurrent,
        Ready,
        Duplicate,
        ConflictingRows
    }

    public sealed class ClashStatusImportPreviewRow
    {
        public ClashStatusImportRecord Source { get; set; }
        public string CurrentStatus { get; set; }
        public ClashStatusImportDisposition Disposition { get; set; }
        public string Details { get; set; }
        public string TargetIdentity { get; set; }
    }

    public sealed class ClashStatusImportAction
    {
        public ClashStatusImportRecord Source { get; set; }
        public string TestId { get; set; }
        public string TargetIdentity { get; set; }
        public string CurrentStatus { get; set; }
        public string TargetStatus { get; set; }
    }

    public sealed class ClashStatusImportPreview
    {
        public ClashStatusImportReport Report { get; set; }
        public List<string> SelectedStatuses { get; } = new List<string>();
        public List<ClashStatusImportPreviewRow> Rows { get; } = new List<ClashStatusImportPreviewRow>();
        public List<ClashStatusImportAction> Actions { get; } = new List<ClashStatusImportAction>();
        public int ReadyCount { get; set; }
        public int AlreadyCurrentCount { get; set; }
        public int NotFoundCount { get; set; }
        public int AmbiguousCount { get; set; }
        public int InvalidStatusCount { get; set; }
        public int FilteredOutCount { get; set; }
        public int IncompleteIdentityCount { get; set; }
        public int DuplicateCount { get; set; }
        public int ConflictingRowsCount { get; set; }
    }

    public sealed class ClashStatusImportResult
    {
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int TestsChanged { get; set; }
    }
}
