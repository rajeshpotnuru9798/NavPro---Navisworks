namespace NavPro.Backend.Models
{
    public sealed class ClashRenameItem
    {
        public string Id { get; set; }
        public string CurrentName { get; set; }
        public string NewName { get; set; }
    }

    public sealed class ClashRenameResult
    {
        public int Renamed { get; set; }
        public int Unchanged { get; set; }
    }
}
