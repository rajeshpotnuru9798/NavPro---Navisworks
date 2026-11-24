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
        public const string SmartClashGrouperName = "SmartClashGrouper";
        public const string SmartReviewMenuName = "SmartReviewMenu";
        public const string SmartReviewMenuDisplayName = "Smart Review";
        public const string SmartClashGrouperDisplayName = "Smart Clash Grouper";
        public const string ChainageClashLocatorName = "ChainageClashLocator";
        public const string ChainageClashLocatorDisplayName = "Chainage-Based Clash Locator";
        public const string AiClashDescriptionName = "AiClashDescription";
        public const string AiClashDescriptionDisplayName = "AI Clash Description Assistant";
        public const string UtilitySeparationCheckerName = "UtilitySeparationChecker";
        public const string UtilitySeparationCheckerDisplayName = "Utility Separation Checker";
        public const string MaintenanceClearanceReviewName = "MaintenanceClearanceReview";
        public const string MaintenanceClearanceReviewDisplayName = "Access and Maintenance Clearance Review";
        public const string CoordinationChangeLogName = "CoordinationChangeLog";
        public const string VerticalClearanceCheckerName = "VerticalClearanceChecker";
        public const string VerticalClearanceCheckerDisplayName = "Vertical Clearance Checker";
        public const string CoordinationChangeLogDisplayName = "Coordination Change Log";
        public const string ContactEmail = "Potnuru.Rajesh@jacobs.com";
        public const string DeveloperId = "9bfa9acd-23b0-4d16-bf4c-a77fc1248c4a";
    }
}
