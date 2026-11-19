# Backend

- `Core/Policies.cs`: host-independent property/criteria keys, naming rules and approximate ETA reporting.
- `Models/InspectionModels.cs`: property options, distinct typed model numbers and inspection results.
- `Services/ModelInspectionService.cs`: property discovery and UI-thread model inspection.
- `Services/SearchCriteriaFactory.cs`: native dynamic search construction from internal property names and typed values; File Name/model-number path strings are reduced to basename for portable equality conditions.
- `Services/SearchSetCreationService.cs`: actual search counts, existing-name detection and new-folder creation without replacing saved sets.
- `Models/ClashMatrixModels.cs`: loaded selection-set choices and clash-pair preview records.
- `Services/ClashMatrixService.cs`: Manage 2024 API 21 discovery of models/sets/existing Clash Detective tests, pair QA and additive native test creation from saved Selection/Search Set sources. It creates test definitions but does not run Clash Detective.
- `Models/ClashReportModels.cs` and `Services/ClashReportExportService.cs`: read individual results on the Navisworks UI thread, generate result images, and write an image-enabled `.xlsx` workbook with clash metadata and center coordinates. It does not rerun clash tests.

Keep Windows Forms controls out of this layer. All Autodesk access remains on the host UI thread. Test pure policies and native adapters separately; doubles do not verify Navisworks runtime behavior.
