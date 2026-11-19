# Frontend

- `Plugins/SearchSetCreaterPlugin.cs`: Navisworks `CommandHandlerPlugin` registration, custom ribbon command, active-document checks and modeless dialog lifecycle.
- `Plugins/SearchSetCreaterRibbon.xaml`: custom NavPro ribbon tab and Search Sets panel. Keep its tab/command IDs aligned with the plug-in attributes.
- `Dialogs/ContactInfoDialog.cs`: read-only NavPro support email with an explicit Copy email action; does not open a mail client or send messages.
- `Dialogs/SearchSetCreaterDialog.cs`: Option A project-specific property/value setup, Option B multi-model `Item / File Name` preview then create workflow, modeless host interaction, Pin window toggle, scrollable grids, rename action, upper-right progress/ETA card and user-facing errors.
- `Dialogs/ClashMatrixDialog.cs`: modeless Clash Matrix Designer; Standard lists each loaded model root, Compact lists model-tree child nodes, Properties lists saved Search Sets, and Sets lists saved Selection/Search Sets. A/B panels provide expanded list space; tolerance input supports all Navisworks distance units and converts to document units before preview. Preview rows allow test-name edits and validated explicit test creation. Its **Clash Report** action exports current clash results with center coordinates and embedded snapshots.
- `Dialogs/ClashApproverDialog.cs`: modeless bulk result-status update workflow for all five standard statuses, selected-test preview, Approved-only approval metadata, and explicit confirmation.
- `Dialogs/ClashToleranceDialog.cs` and `Backend/Services/ClashToleranceService.cs`: tolerance/type updater with a Composite Object Clashing choice (Keep current, Enabled, Disabled), preview of current/proposed values, and full-test-copy updates through `ClashTest.MergeComposites`.
- `Plugins/SearchSetCreaterPlugin.cs` registers the **Refresh Models** Clash Review command; `Backend/Services/ClashModelRefreshService.cs` calls `Document.UpdateFiles()` on the host UI thread to refresh changed files already referenced by the open document. It does not append files or remap paths.
- `Dialogs/ClashRenamerDialog.cs`: modeless test-name editor with find-and-replace, case matching, find-next/previous, editable bulk names, prefixing for checked rows, select-all/none controls, and explicit confirmation.
- `Controls/RoundedProgressIndicator.cs`: custom anti-aliased circular progress ring with a rounded accent arc, centered percentage and animated indeterminate state. Keep unknown-total work indeterminate; do not infer percentage or ETA from item counts when no total is known.

Put future reusable controls in `Frontend/Controls` and shared styling in `Frontend/Ui` when they are implemented. Keep model inspection and search-set mutations in backend services. UI events must not send Navisworks API work to background threads.

