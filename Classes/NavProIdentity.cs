namespace NavPro.Classes
{
    /// <summary>Shared, host-independent product identity. Keep existing registration IDs stable.</summary>
    public static class NavProIdentity
    {
        public const string ProductName = "NavPro";
        // Retain this command ID for compatibility with existing Navisworks registrations.
        public const string SearchSetCreaterName = "SearchSetCreater";
        public const string SetCreatorName = "SetCreator";
        public const string SetCreatorDisplayName = ProductName + " - " + SetCreatorName;
        public const string SearchSetCreaterDisplayName = SetCreatorDisplayName;
        public const string ClashMatrixName = "ClashMatrix";
        public const string ClashMatrixDisplayName = "Clash Matrix";
        public const string ClashMatrixPluginDisplayName = ProductName + " - " + ClashMatrixDisplayName;
        public const string ClashApproverName = "ClashApprover";
        public const string ClashApproverDisplayName = "Status Updater";
        public const string ClashStatusImportName = "ClashStatusImport";
        public const string ClashStatusImportDisplayName = "Import Clash Status";
        public const string ClashRefreshModelsName = "ClashRefreshModels";
        public const string ClashRefreshModelsDisplayName = "Refresh Models";
        public const string ClashColorizerName = "ClashColorizer";
        public const string ClashColorizerDisplayName = "Colorizer";
        public const string ClashToleranceName = "ClashTolerance";
        public const string ClashToleranceDisplayName = "Tolerance Updater";
        public const string ClashRenamerName = "ClashRenamer";
        public const string ClashRenamerDisplayName = "Renamer";
        public const string ClashDashboardName = "ClashDashboard";
        public const string ClashDashboardDisplayName = "Clash Dashboard";
        public const string ContactInfoName = "ContactInfo";
        public const string ContactInfoDisplayName = "Contact Info";
        public const string ContactEmail = "Potnuru.Rajesh@jacobs.com";
        public const string DeveloperId = "9bfa9acd-23b0-4d16-bf4c-a77fc1248c4a";
    }
}
