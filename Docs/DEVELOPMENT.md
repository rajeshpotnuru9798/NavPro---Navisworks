# NavPro development workspace

Open `NavPro.code-workspace` in VS Code, or `NavPro.sln` in Visual Studio. The VS Code workspace contains **only NavPro**, not Mytools. Terminal tasks start in the NavPro project folder. Existing Mytools source and installation remain separate and unchanged.

## Folder map

```text
NavPro/
  Frontend/       Navisworks plugin entry and Windows Forms dialogs
    Plugins/
    Dialogs/      SetCreator, Clash Matrix, Clash Approver, Status Import, Renamer and dashboard UI
  Backend/        Inspection, typed queries, naming and set creation
    Core/
    Models/
    Services/     Model inspection, search-set creation, Clash Matrix, result approval, clash renaming and dashboard operations
  Classes/        Shared host-independent classes and product identity
  Properties/     Assembly metadata
  Assets/         16px and 32px tool icons
  Tests/          Policy and criteria-adapter tests with explicit doubles
  Tools/          Icon, build, verification and backup-aware install scripts
  Deployment/     Source bundle manifest/template
  Docs/           Developer documentation
  .vscode/        VS Code build/test/package tasks
  bin/, obj/      Generated build intermediates/output (hidden in workspace)
  dist/           Generated bundle and shareable ZIP
  NavPro.csproj
  NavPro.sln
  NavPro.code-workspace
  README.md
  VERIFICATION.md
```

## Workflow

1. Use **Terminal > Run Build Task** for a Release build.
2. Use **Terminal > Run Task > NavPro: Run tests** to build and run the focused executable tests.
3. Use **NavPro: Build shareable package** for build, tests, metadata checks and bundle generation.
4. Close Navisworks before running `Tools/Install-NavPro.ps1`. Builds do not install automatically.
5. Follow the README's manual host checklist; compilation and test doubles are not native integration verification.

Future tools should add their plugin entry/dialog under Frontend, operation models/services under Backend, tests under Tests and icons under Assets. NavPro supports Manage 2024 API 21 and 2025 API 22 as separate compile targets; each reference points directly to its matching `Autodesk.Navisworks.Api.dll` and `Autodesk.Navisworks.Clash.dll`, with Autodesk binaries kept out of the bundle. `Release` targets 2024; `Release2025` targets 2025 and defaults to the supplied `Manage 2025 DLLs` folder. All clash-document operations stay on the host UI thread. Matrix preview is explicit; creating definitions does not run tests. Status Updater previews selected tests across the five standard ClashResultStatus values and requires explicit confirmation before bulk status changes; Approved by/time metadata is set only when the target is Approved. Renamer uses `DocumentClashTests.TestsEditDisplayName(SavedItem, string)` in a document transaction, re-resolves selected clash tests by GUID, validates unique target names and verifies displayed names after commit. Find & Replace should stage changes only on selected proposed names, expose case sensitivity, show match/no-match feedback and require review before applying. Dashboard summary visuals and the historical clustered columns and completion pie should derive from the same filtered result rows as the clash register so loaded-file, priority, discipline and status slicers remain coordinated. An optional `.xlsx` export can be created without an external spreadsheet library by writing the Open XML package with inline strings; provide summary, test and result sheets and validate the ZIP parts/XML. Preserve numeric distance and coordinate cells as numeric values. Add general shared classes only when there is a concrete shared responsibility. Keep tool names and registration IDs stable. No duplicate source tree, copied Autodesk SDK binaries, Mytools references or Mytools deployment scripts belong in this project.



Clash Review command order is Renamer, Tolerance Updater, Status Updater, Import Clash Status, then Refresh Models. Refresh Models calls `Document.UpdateFiles()` on the host UI thread to refresh changed files at their existing referenced paths. It does not discover or append newly named files, remap moved/renamed files, run clash tests, or save the document. The operation is synchronous; the command confirms with the user before starting and advises reviewing/rerunning clash tests afterward. The importer reads NavPro dashboard XML/CSV and standard Navisworks Clash Detective XML, allows a status subset, previews test/result matches, and only offers unique matches for import after explicit confirmation. Standard Navisworks XML may omit item names, so those rows match by test and clash names and are still rejected when that key is ambiguous. Ambiguous, unmatched, incomplete, invalid, conflicting, duplicate and unchecked-status rows remain unchanged. It updates statuses only (plus Approved by/time metadata when setting Approved), and API calls stay on the host UI thread. Tolerance Updater uses full editable copies with `TestsEditTestFromCopy`; its Composite Object Clashing selector supports Keep current, Enabled and Disabled, previews current/proposed values, validates selected snapshots and units before writing, and verifies requested values by GUID. **Re-Run Clashes** calls `TestsRunTest` only for checked tests, resolves a fresh live test by GUID before each synchronous run, and requires changed preview settings to be applied first. All API operations remain on the host UI thread. Preview invalidates on input/selection edits. See VERIFICATION.md for adapter, UI-fixture and native acceptance boundaries.

For Clash Matrix, Standard displays one loaded model root/file per side; Compact displays model-tree child nodes. Allocate enough height to the A/B lists for these model choices, and expose Navisworks distance units as a dropdown. Convert the selected input unit to document units before storing `ClashTest.Tolerance`; show both input and stored units in the preview summary. Autodesk Help describes Standard as the default hierarchy and selecting all loaded files as testing the entire model; root selection is compile-checked and still needs a native clash-result check.

SetCreator's preview counts should come from the typed property scan it already performs, rather than issuing an additional `Search.FindAll` for every distinct value. `Search.FindAll` has produced native exceptions on certain federated NWF/NWC models in both Option A and Option B. Count each typed value once per hierarchy item, grouping with the same canonical typed key used to create the dynamic Search. For Option B, scan all loaded models for preview counts because the saved query is document-wide even when value candidates came from selected source models. Native query semantics and counts still require checking against representative NWF/NWC files.

The HTML Clash Dashboard's test selector is dynamically populated from tests in the active report/snapshot context. Store selected test names as a nullable set (null means all; empty means none), apply it in the shared row filter, and rebuild every visual and cascading facet from the same filtered data. Select All/Select None controls belong beside the discipline summary heading where the user expects to scope its totals. Reset must restore the test facet along with the other filters.
