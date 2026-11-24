# NavPro verification — 2026-10-19

## Review ribbon black icon backgrounds — 2026-11-23

Changed the code-native icon generator for Smart Clash Grouper and the five Smart Review tools from purple tiles to black, retaining white/cyan symbols and transparent outer padding. The Smart Review dropdown uses the AI Clash Description icon, so its header is covered too. Regenerated all twelve 16/32px assets; checked dimensions, black background RGB and transparent corners, and visually reviewed the generated contact sheet at `Tests/Artifacts/Ribbon-Black-Icons.png`.

Build-Package completed with zero warnings/errors for API 21/22/23, 549 detached Smart Review checks, the tolerance/policy/adapter checks, registration and package validation. Native ribbon rendering of the updated icons remains unverified. Navisworks Manage 2024 was open, so this update was packaged but not installed; no host was terminated.

## Jacobs branding update — 2026-10-21

1.0.1 changes all product-owned dialogs via the shared theme: Jacobs blue-led palette, installed Chronos/Arial fallback, neutral work areas, consistent grid/tab/actions and progress styling. See Docs/Jacobs-Branding.md for source provenance and older-guide limitations. No proprietary fonts redistributed, native host UI changed, model operations added or installation performed by this branding change. Cross-product fixtures render Contact Info and a representative theme/progress form offscreen and check matching tokens, fonts and selected text contrast; this does not prove full native Manage dialog layout. Native host review at target DPI remains required.

## Clash Dashboard (Manage 2024 API 21)

- Replaced the Excel-only Clash Report action with **Clash Dashboard**, a self-contained HTML dashboard built from every saved Clash Detective test, including tests with no results, and their individual leaf results.
- Setup includes project name, output units, native New/Active/Reviewed/Approved/Resolved statuses, all kept versus selected-status results, and optional per-clash viewpoint images. Values are converted from document units. Image calls run synchronously on the host UI thread.
- Dashboard includes loaded model filenames and file dates when accessible, status/priority/discipline filters, AND/OR discipline logic, status KPI cards, a reviewed/approved/resolved KPI, discipline-pair table, test definitions, result register with names/coordinates/assignment/approval metadata, CSV, multi-dashboard loading, and browser print. Optional tabular HTML and NavPro XML companions are supported.
- Priority and assignment overrides are stored in the browser and do not update NWF/NWD. The API does not expose the screenshot's priority rating or viewpoint title. Priority and full Discipline A/B values are parsed from `Priority_DisciplineA_VS_DisciplineB` test names; invalid names are shown with a validation message and their result rows use Unclassified disciplines. Weekly historical change cannot be calculated from one saved report.
- Release build passed with 0 warnings/errors; 43 existing policy/adapter assertions, registration checks and package validation passed. Embedded dashboard JavaScript syntax check passed. These checks do not verify live Clash Detective counts, generated images, browser layout, print-to-PDF, or dashboard imports in a real browser.
- Host verification still required: compare dashboard totals against a test NWF, check units and image rendering, import another generated dashboard, exercise filters/CSV/print, and verify any actual status or assignment changes inside Navisworks.

## Clash Matrix selection views and profile

- 2026-10-28: The reported `unsupported type of SavedItem for this group` came from calling `TestsAddCopy(GroupItem, SavedItem)` with a ClashTest and ClashTestFolder parent. The API reference states ClashTests can only be added to `Value.Tests`; creation now uses the typed `TestsAddCopy(ClashTest)` root overload for every eligible preview row. The service verifies every expected name after the transaction. Existing folders/items are preserved, including any empty folder left by a prior failed attempt. Zero-result Search Set confirmation remains a create confirmation, not an error. Release/package checks passed; live Manage creation and model-specific selection persistence still require host verification.
- 2026-10-28: Clash Dashboard visuals now use the Jacobs blue P1 `#231EDC`, cyan P3 `#5AE6FF`, white/N4 surfaces and N1 text. Applied relevant Power BI guidance: top-level KPI hierarchy, direct bar comparison, cross-filtering flow/chart summaries, consistent color use, contrast, and text labels alongside color. Dashboard script syntax check and package checks passed; browser rendering/accessibility and real data interactions still require review.
- 2026-10-28: Added **Run Clashes** beside Create Previewed Tests. It retains names from successful creations in this dialog and invokes `DocumentClashTests.TestsRunTest(ClashTest)` only for those tests, resolving a live test again before each run. Completed names are removed from the pending list; remaining tests stay available if a later run fails. Status updates occur between synchronous native runs; no precise ETA or safe interruption is claimed. Build/package checks cover the API signature and UI wiring; an in-host test run remains unverified.
- 2026-10-28: Dashboard review markup: removed loaded model filenames from the main header, labeled the export timestamp as the dashboard creation date, changed Total and per-status cards to overall counts for selected dashboard sources independent of register slicers, and made completion percentage explicit with a labeled progress bar. Priority now defaults from the first character before `_` in the Clash Test name (`H_SL-...` → `H`); manual overrides remain browser-local. Script parsing and package checks required; browser/layout behavior still needs review.

- 2026-10-21: Clash Matrix now keeps a vertical scrollbar visible in Loaded Models and a horizontal scrollbar for long model names. Preview evaluates saved Search Sets through a copy of their stored `Search` and `Search.FindAll(document, false)` before marking a pair empty; explicit model-tree selections continue to use their item collection. This targets the reported screenshot state (16 pairs, all marked empty, Create disabled). API/package checks passed and the bundle was installed while Navisworks was closed; the model-specific Search Set counts and clash-test creation still require native Manage verification.
- 2026-10-22: Follow-up screenshot still showed zero matching Search Set items and a disabled create action. Matrix QA now keeps zero-result Search Sets visibly warned but allows their saved dynamic sources to be used after a confirmation that explains they may produce no results; empty explicit model-tree selections remain blocked. When the same selected sets occur on both sides, symmetric A/B pairs are deduplicated so one direction remains available and the reverse row is marked skipped. Loaded Models now shows a vertical scroll affordance without forcing a horizontal bar. Package build and 43 policy/adapter assertions passed; the updated 24-file bundle is installed with SHA256 verification. Native Manage preview counts and clash-test creation still need model-level verification.

- Clash Matrix definition import/export remains absent, per request. Clash Dashboard exports HTML and optional tabular HTML/XML companions; it does not create an Excel workbook.
- `Tools/Build-Package.ps1` passed on 2026-10-20 after the removal: 0 warnings/errors, 39 policy/adapter assertions, registration metadata, and package path/icon/hash checks passed. The backup-aware installer updated the bundle while Navisworks was closed; all 19 deployed files passed SHA256 verification. Native UI testing remains outstanding.
- Fixed a preview path that skipped Prefix because the naming helper returned while the dialog was busy. Preview now applies Prefix at the beginning of each proposed test name before rendering and validation. Build/package checks passed; the corrected bundle was installed while Navisworks was closed and all 19 payload files passed hash verification.
- Replaced independent A/B self-intersection controls with the requested mutually exclusive global modes (applied to both sides) and a Composite Object Clashing option. API 21 reflection confirmed `ClashSelection.SelfIntersect`, `ClashTest.MergeComposites`, and `ClashResult.CompositeItem1`; the last is result-only. Autodesk Help shows that Self-Intersect runs the pane selection against itself and describes Composite Object Clashing. The distinct term “Exclude Self-Self” is represented as the inverse of Self-Intersect because no separate native ClashTest property exists.
- `Tools/Build-Package.ps1` passed on 2026-10-20: 0 warnings/errors, 39 policy/adapter assertions, registration metadata, and package path/icon/hash checks passed. After Navisworks closed, the backup-aware installer updated the bundle; all 19 deployed files passed SHA256 verification. Native UI and clash-result checks are outstanding.
- Added Prefix and Separator controls beside the clash behavior options. Editing either regenerates proposed names from A name + separator + B name, then rechecks blank, existing, and duplicate names; row-level naming remains available. Excel plans include both settings and validate them as consistent on import.
- `Tools/Build-Package.ps1` passed on 2026-10-20: 0 warnings/errors, 39 policy/adapter assertions, registration metadata and package path/icon/hash checks passed. The updated bundle was not installed because Navisworks was running; native dialog and Excel round-trip checks remain outstanding.
- Added independent A/B view selectors for Standard, Compact, Properties and Sets. Standard uses loaded model roots; Compact uses model-root child items; Properties filters saved Search Sets; Sets shows saved Selection and Search Sets. Model-item clash assignments use `ClashSelection.Selection.CopyFrom(ModelItemCollection)`; set assignments use `DocumentSelectionSets.CreateSelectionSource`. Autodesk Developer Blog documents these explicit-model-item and set-source patterns: https://blog.autodesk.io/create-clash-selection-with-selection-set/ . Native view presentation and clash results require Manage verification.
- Added per-side surface/triangle, line and point primitive selection, one Self-Intersect / Exclude Self-Self mode applied to both sides, and Composite Object Clashing. The behavior maps to `ClashSelection.SelfIntersect` on A/B and `ClashTest.MergeComposites`; the result-only `ClashResult.CompositeItem1` API is not a test option. Excel export/import now carries both modes and composite setting. Autodesk Help describes Self-Intersect as testing a pane against itself and Composite Object Clashing as inclusion of composite results: https://help.autodesk.com/cloudhelp/2018/ENU/Navisworks/files/GUID-F72265A1-A934-4533-9377-87DC3DB1519A.htm .
- The existing-tests summary reads test LastRun and traverses nested ClashResultGroups to count leaf IClashResult statuses. It is read-only; it does not run tests or edit statuses. ClashTest creation uses the API's typed root overload; the generic group overload cannot insert ClashTest under a folder.
- Added dependency-free `.xlsx` export/import with stable selection keys, test names, types, tolerance, primitive flags, self-clash mode, composite setting, naming options and validation labels. Import re-resolves keys against the current document and validates before the separate create action. Workbook-level/Excel compatibility and native clash behavior still require Manage review.
- Final `Tools/Build-Package.ps1` passed after the API 21 folder-root correction: 0 warnings/errors, 39 policy/adapter assertions, registration metadata and package path/icon/hash checks passed. The backup-aware installer ran with Navisworks closed; all 18 deployed files passed SHA256 verification. This does not verify native host behavior.

## Target and SDK

NavPro targets Autodesk Navisworks Manage 2024, x64, .NET Framework 4.8. The installed `Roamer.exe.config` specifies .NET Framework 4.8. The installed `Autodesk.Navisworks.Api.dll` assembly version is 21.0.0.0; its local XML documentation and the `CommandHandlerPlugin`, ribbon attributes, and command-state metadata were inspected before using the custom ribbon API.

## Build, tests and package

- Latest `Tools/Build-Package.ps1` after converting the window to modeless and adding a Pin window toggle: Release solution build passed with **0 warnings / 0 errors**; all package checks passed.
- Latest `Tests/bin/Release/net48/NavPro.Tests.exe`: **35 assertions passed**, covering naming policies, legacy property-label ranking, typed values, missing/blank/unsupported properties and criteria-adapter calls. The label-ranking helper is not used to choose Option B's property now.
- Test adapter/API doubles verify application logic and call construction only; they do **not** prove native Navisworks search or model behavior.
- Windows PowerShell 5.1 metadata check passed: the public registration type derives from `CommandHandlerPlugin`; the stable `[Plugin]` identity remains `NavPro`; `RibbonLayout`, `RibbonTab`, and `Command` attributes match the XAML tab/command IDs; the API reference is 21.0.0.0.
- Package checks passed for manifest paths, custom ribbon XAML presence and IDs, 16×16/32×32 icon dimensions, and built/bundled DLL SHA256 equality. The shareable ZIP is generated under `dist`.
- The rounded progress-indicator build was previously installed with **10 files** passing SHA256 comparison and the prior bundle backed up under `%LOCALAPPDATA%\NavPro\Backups`.
- The current package fixes Option B to `Item / File Name` and was installed while Navisworks was closed. **10 files** passed SHA256 comparison; the previous bundle was backed up to `%LOCALAPPDATA%\NavPro\Backups\NavPro-20261016-194325-669.bundle`.
- The current multi-model modeless/pin-window package was installed while Navisworks was closed. Ten deployed files passed SHA256 comparison; the previous bundle is backed up under `%LOCALAPPDATA%\NavPro\Backups`. Native host testing remains separate from build, metadata and adapter checks.

## SetCreator UX update

- 2026-10-20 Option B creation fix: stopped building `SelectionSet` children under a detached `FolderItem` and copying that hierarchy as a single operation. The creator now adds the folder to `DocumentSelectionSets`, resolves the attached folder, then uses the API 21 documented `AddCopy(GroupItem, SavedItem)` overload for each prepared set. It verifies the saved folder and expected child names before reporting success. Local API XML documents this overload; Autodesk's Developer Blog also demonstrates adding a search set to `DocumentSelectionSets` before organizing sets into folders: https://blog.autodesk.io/insert-common-selectionset-to-folder/. Package checks passed and the revised bundle was installed with Navisworks closed; native Option B creation still needs an in-host model check.

- Removed prefix/suffix controls and generation. Default resulting set names are the model-number display value only; per-row **Rename / Edit** remains.
- Changed the dialog brand heading to **NavPro**, grouped criteria in a white panel, strengthened table headers and spacing, and added a top-right progress card.
- Known-total stages show a percentage and an approximate stage ETA clock time plus remaining duration after sufficient samples. Unknown-total work remains marquee/estimating. Native synchronous calls may pause updates; ETA is not a guarantee.
- The upper-right progress indicator is now a custom rounded circular ring with centered percentage text and an animated indeterminate arc for unknown totals. Visual rendering still requires Manage review.
- The preview grid now omits Stored type and Matching items, uses fixed readable column widths, and exposes horizontal/vertical scrollbars.
- SetCreator separates **Option A** (the project-specific category/property/value preview and rename flow) from **Option B** (select multiple loaded source models, read fixed `Item / File Name` values from each selected model's `Model.RootItem.DescendantsAndSelf`, merge duplicate typed values, preview, then explicitly create sets). All loaded models are checked by default and users can uncheck models. The property key uses the installed SDK's `PropertyCategoryNames.Item` and `DataPropertyNames.ItemFileName`; no property-name guessing occurs in Option B. This behavior is build-checked but still needs in-host model testing.
- The visible SetCreator title, ribbon text and command display name were updated while retaining the stable internal `SearchSetCreater` command ID and `NavPro` plugin identity.
- The dialog is modeless and has a **Pin window** toggle bound to WinForms `TopMost`, allowing host interaction while the dialog stays visible. Model/document changes invalidate the inspection session. Modeless host interaction and pin stacking have not been visually verified in Manage.
- A selected model scopes the values read from `Item / File Name`. The saved `SelectionSet(Search)` still uses `Search.Selection.SelectAll()` and therefore queries the whole current document; equal File Name values in another model can also match. This boundary is stated in README and must be considered during host review.
- Option B displays and names from the basename of full-path File Name values, independent of a user's drive/folder path; duplicate filenames across selected models are grouped. Search uses the documented `DisplayStringWildcard("*filename")` form to match full-path values by suffix. Adapter assertions verify construction; actual wildcard behavior requires Manage host testing.
- Option A applies the same filename-suffix wildcard to File Name and recognized model-number display-string properties when values are path strings. Other properties and non-path values keep their original typed equality condition. Adapter assertions cover construction; verify data semantics and native query results with project models in Manage.
- The criteria adapter is covered by test doubles; the fixed property constants compile against the installed API 21 assembly. Neither check proves that a real model exposes usable Item / File Name values or that native search results are correct.
- The UXPeak page was used as visual inspiration only; its course landing page is not a Navisworks or accessibility standard.
- Build/package and installation do not run Navisworks.

## Host checks still required

No native host verification is claimed. Open Navisworks Manage 2024 and verify plug-in discovery, the **NavPro** ribbon tab, **Search Sets** panel, **SetCreator** button/icons, modeless interaction with the host, the **Pin window** toggle, and dialog layout. Then follow the README's model-based functional checklist for selected-model property inspection, search counts, saved-set creation, persistence, and cancellation behavior. The backup-aware installer checks file hashes; that does not prove host loading or native API behavior.

The Navisworks 2024 API transaction documentation does not guarantee rollback. Preparation is detached and existing sets are never deliberately replaced or deleted; inspect the Sets tree after any unexpected commit error. No guaranteed speed, exact ETA or safe interruption of synchronous native calls is claimed.

## Clash Matrix Designer (Manage 2024 API 21)

- Added a second ribbon panel/command, **NavPro > Clash Matrix > Clash Matrix**. The SetCreator display name changed while the stable `NavPro` plug-in identity and internal `SearchSetCreater` command ID were preserved.
- Added local `Autodesk.Navisworks.Clash.dll` API 21 reference with `Private=false`; Autodesk assemblies are not copied into the bundle.
- Designer reads loaded model filenames, traverses all saved selection/search sets, counts selected items for checked sets during Preview, and reads existing clash tests and their type/tolerance profiles.
- Preview forms the selected A × B pairs and flags empty sets, a set paired with itself, symmetric duplicate pairs, existing test names, and repeated proposed test names. Existing mixed type/tolerance profiles are reported in the QA summary. A global type/tolerance profile keeps planned test settings consistent.
- The documented saved-set source pattern is used for each ClashTest: `DocumentSelectionSets.CreateSelectionSource(set)` added to `ClashTest.SelectionA/B.Selection.SelectionSources`. The action creates test definitions only; it does not run Clash Detective or produce results.
- Manage 2024 API 21 Release build passed with 0 warnings/errors; 39 existing policy/adapter assertions passed; metadata checks validated both command IDs and API 21 references; package checks validated both icons (16/32px), bundle paths and DLL hash.
- No Navisworks host test has been run. Verify ribbon discovery, set enumeration/contents, wildcard states, tolerance interpretation, native ClashTest creation, dynamic set source retention, and Manage UI display before using on project data. API tests and metadata checks do not prove those host behaviors.
## Clash Approver (Manage 2024 API 21)

- Added a modeless **NavPro > Clash Review > Clash Approver** ribbon command with a dedicated 16/32 px icon.
- The dialog reads saved Clash Detective tests, defaults all tests selected, previews leaf-result status counts, and defaults to approving New, Active and Reviewed statuses. Approved results are skipped. Resolved results are excluded unless explicitly checked.
- Bulk approval updates status to Approved plus approver name and approval time after an in-dialog confirmation. It does not rerun clash tests. Result groups are recursively traversed so group containers are not counted alongside their leaves.
- The Manage 2024 Clash API signatures were verified against the installed API 21 assembly; package checks cover command metadata/XAML/icon payload. Native host behavior, approval metadata display, and result group edge cases have not yet been verified in Manage.
- 2026-10-20 package build: 0 warnings/errors, 39 policy/adapter checks passed, registration and bundle checks passed. Navisworks was closed; the backup-aware installer updated the installed bundle and verified SHA256 for all 23 deployed files. The installed DLL hash matches the packaged DLL. Native UI/result behavior is still pending a Manage session.


## Clash Dashboard per-test Clash status (2026-10-28)

- Added a **Clash status** column to the Clash Tests dashboard table. Each test shows counts for the native statuses represented by its included Clash Results; tests with no included results are labeled accordingly.
- The aggregation uses each `IClashResult.Status`, with API enum values New, Active, Reviewed, Approved and Resolved. Status filters are applied before aggregation so summaries align with the exported report's included results.
- Dashboard JavaScript syntax check passed. Build/package checks and native host review are pending for this change. No Manage UI test is claimed.

## Clash Dashboard per-test Clash status (2026-10-28)

- Added a **Clash status** column to the Clash Tests dashboard table. Each test shows counts for the native statuses represented by its included Clash Results; tests with no included results are labeled accordingly.
- Aggregation uses each `IClashResult.Status`, with API enum values New, Active, Reviewed, Approved and Resolved. Status filters are applied before aggregation so summaries align with the exported report's included results.
- Dashboard JavaScript syntax check passed. Build/package checks and native host review are pending for this change. No Manage UI test is claimed.
- 2026-10-28 package/install: Release build completed with 0 warnings/errors; 43 policy/adapter checks passed; API 21 registration and package payload checks passed. With Navisworks closed, the backup-aware installer replaced the NavPro bundle and verified hashes for all 24 files. Native dashboard rendering and live Clash Result status behavior still require Manage review.

## Separate Clash Dashboard ribbon tool (2026-10-28)

- Moved the Clash Dashboard button out of Clash Matrix and into a dedicated **Export** ribbon panel as **Clash Dashboard**.
- Preserved the existing dashboard export workflow in a separate modeless dialog with its settings, save path, HTML generation, and browser open behavior. Added a dedicated report icon and package payload checks.
- Build/package and native UI verification are pending for this change.
- 2026-10-28 separate Export tool: Release build completed with 0 warnings/errors; 43 policy/adapter assertions passed; metadata checks confirm `ClashDashboard` is registered in the Export ribbon panel; package paths, new 16/32px icon assets and DLL hash passed. Navisworks was confirmed closed before installation; installer backed up the prior bundle and hash-verified all 28 deployed files. Native ribbon loading and dashboard dialog interaction still need Manage review.

## Dashboard markup revisions (2026-10-28)

- Removed the Date and Viewpoint filter panels and the Clashes by test chart from the interactive HTML dashboard.
- The Total Clashes and status KPI cards now use all five statuses across all Clash Tests in each selected dashboard source, regardless of the current register filters or the export's selected-status detail scope. The register can still honor the report's result scope and its remaining filters.
- Dashboard JavaScript syntax passed. Release build/package checks are pending; Manage rendering remains unverified.
- Dashboard markup deployment (2026-10-28): build 0 warnings/errors; 43 policy/adapter assertions, command registration, package asset/path and DLL hash checks passed. JavaScript syntax passed, and removed Date/Viewpoint filters plus Clashes by test renderer are absent. With Manage closed, installer backed up the previous bundle and verified all 28 files. Native browser/host rendering remains unverified.

## Clash test name parsing (2026-10-28)

- Priority and complete A/B discipline names now come from the saved Clash Test name, using the first underscore and `_VS_` delimiter. Hyphens and descriptions are preserved; `VS` is excluded from all extracted fields and dynamic discipline dropdowns.
- Invalid names display the expected pattern and example in the Clash Tests table. Invalid result rows remain visible through the Unclassified discipline option.
- Added parser assertions for both user examples, hyphen/description preservation, missing/repeated separators and invalid names. Build and package checks pending.
- 2026-10-28 clash-name parsing deployment: 0 build warnings/errors; 48 policy/parser assertions passed, including both provided examples, hyphen retention, invalid/repeated separators and no extracted VS token. Dashboard JavaScript syntax and example probes passed; registration/package checks passed. Navisworks was closed before installation; installer backed up prior NavPro and verified 28 deployed files. Native dashboard rendering remains unverified.

## Dashboard priority/discipline extraction from Clash Test names (2026-10-28)

- Added a shared parser for `Priority_DisciplineA_VS_DisciplineB`: the first underscore ends Priority and the unique `_VS_` token separates the full discipline strings. Hyphens and descriptions are retained.
- Priority and A/B discipline filters are populated dynamically from test names, including tests without results. `VS` is rejected as a segment and excluded from extracted values and dropdowns.
- The Clash Tests table shows a format validation message for invalid names. Their result rows stay accessible under Unclassified. Added parser tests for both examples, missing/repeated separators and separator leakage.
- Release build/package and JavaScript checks pending; native dashboard display not yet verified.
- 2026-10-28 parsing update completed: Release build 0 warnings/errors; 48 policy/parser assertions passed; metadata/package checks and dashboard JavaScript syntax/probe passed. Navisworks was closed before installation; installer backed up the prior bundle and hash-verified all 28 files. Native Manage display remains unverified.

## Clash status history ribbon chart (2026-10-28)

- Added a **Status of Clashes and Date** ranked ribbon chart above the KPI cards. It plots each loaded report's export timestamp on the X axis, stacks the five statuses by count on the Y axis, reorders ribbons by status rank at each report point, and exposes point counts/ranks in SVG tooltips.
- Added a user-selected reports-folder picker. In browsers supporting the File System Access API, recursively scans compatible NavPro HTML dashboards every 15 seconds while the dashboard is open, reads only changed HTML files, and remembers the selected directory handle where IndexedDB supports it. A `webkitdirectory` picker provides a manual folder-load fallback; it cannot auto-monitor.
- Timestamp and status aggregation, two-report chart rendering, HTML JSON payload extraction and JavaScript syntax probes passed. Release build/package checks pending; host/browser permission flow and visual rendering still require review.
- 2026-10-28 ribbon chart deployment: Release build 0 warnings/errors; 48 policy/parser assertions passed; chart status/timestamp aggregation, embedded report parsing, SVG generation and JavaScript syntax probes passed; metadata and package checks passed. Navisworks was closed; installer backed up the previous bundle and hash-verified all 28 files. Browser folder-permission persistence and visual/chart interpretation remain unverified in the target browser.

## Dynamic dashboard visual interactions (2026-10-28)

- Added a report snapshot timeline slider and **Play history** control. Changing the selected time point updates the KPI cards, status flow, discipline summary, Clash Tests table and Clash Register to the current snapshot while the ribbon retains the full selected history.
- Ribbon points are clickable and keyboard-selectable; KPI status cards, status-flow stages, discipline pairs and Clash Tests cross-filter the result register. Added a **Clear visual selections** control and current snapshot/date context.
- Power BI-style interaction principles adapted from Microsoft's cross-filter guidance: selecting one visual filters related detail visuals, controls stay visible and selections can be reset. The snapshot grouping/ribbon chart passed Node probes; JavaScript syntax check pending after final edits. No native browser rendering has been performed.

## Clash Renamer and dashboard slicer coordination (2026-10-28)

- Added **Renamer** to the **Clash Review** ribbon panel. It loads all Clash Detective tests, supports find-next/previous navigation, inline bulk name edits and prefixing checked tests. The service uses API 21 `TestsEditDisplayName`, checks non-empty and unique target names, re-resolves tests by GUID, asks for confirmation, and verifies names after the transaction commits.
- Dashboard KPI cards, status flow, discipline summary, Clash Tests table, result register and historical ribbon now recalculate from the selected loaded files, priority, discipline and status filters.
- The `TestsEditDisplayName` signature was inspected by read-only reflection against the installed `Autodesk.Navisworks.Clash.dll`. Build/package verification and native Manage review are pending for this update.
- Verification completed: Release build succeeded with 0 warnings/errors; all 48 policy/adapter assertions passed; registration verified the `ClashRenamer` command in the Clash Review panel; 16/32 px icon, bundle paths and DLL hash checks passed. Dashboard JavaScript syntax passed and focused Node probes confirmed Loaded Files, Priority, Discipline A, Discipline B and Clash Status filtering. These checks do not verify native rename behavior or dashboard rendering in Navisworks/browser.

## Renamer Find & Replace, dashboard dialog wrap and Excel export (2026-10-28)

- Renamer now stages case-configurable Find & Replace across checked proposed test names, retains next/previous navigation and prefixing, and adds Select All/None. Replacement changes remain editable until confirmed and applied.
- The Clash Dashboard intro label wraps within the dialog width, and the unused expanding row was removed to reduce whitespace.
- Added an optional `.xlsx` companion with Dashboard Summary, Clash Tests and Clash Results worksheets. The result sheet contains clash statuses, disciplines, items, assignments, distance and coordinates; the workbook summary includes all-status totals and completion percent. HTML remains the interactive dashboard, and the optional HTML-only viewpoint images are not embedded in the workbook.
- Workbook output was generated from sample data; required Open XML package entries were present and each XML part parsed successfully. Release build/package checks and native dialog review pending.
- Verification completed: Release build succeeded with 0 warnings/errors; all 48 policy/adapter assertions passed; plugin metadata and package payload checks passed; sample workbook Open XML parts parsed. With Navisworks closed, the backup-aware installer replaced the installed bundle and hash-verified all 32 files. Native dialog layout, Find & Replace behavior and Excel opening still need review in Navisworks/Excel.

## Cascading dashboard filters and Loaded Reports list (2026-10-29)

- Replaced the Loaded Files heading and model/source filename listing with **Loaded Reports**, showing each report's project name and creation timestamp only.
- Priority, Discipline A/B, Clash Status and Loaded Reports facets now cascade from the active filter context and search text. Their selected values stay synchronized as available options change. KPIs, flow, discipline summaries, test rows, register and historical ribbon derive from the active filtered records. **Reset Filters** clears facets, search, timeline and visual selections.
- When an imported compatible dashboard lacks individual result rows, charts and summaries use per-test `ClashStatusCounts`; when only report-level `AllStatusCounts` exist, they remain aggregate and cannot be accurately divided by test, priority or discipline. The detailed register requires individual result rows.
- Validation completed: embedded dashboard JavaScript passed Node syntax check; Release build succeeded with 0 warnings/errors; all 48 policy/adapter tests passed; registration metadata and package paths/assets/DLL hash passed. With Navisworks closed, the backup-aware installer installed 32 hash-verified files. These checks do not verify actual browser rendering, filter interactions at scale, or Navisworks host behavior.

## Dashboard interaction reliability, brown palette and render performance (2026-10-29)

- Replaced repeated per-control event binding with one delegated `change` listener for sidebar facets, reports and logic controls. Search, timeline, visual selections, imports and priority edits now request a coalesced `requestAnimationFrame` refresh; dependent facets rebuild from current report, timeline, visual and search context.
- Added per-report normalized-row caching, set-based test/result matching, one-pass status aggregation, and a 100-row paged Clash Register. KPI and chart counts still use the full filtered data set. Brown replaces the dashboard's blue primary accents; status teal/green semantics remain distinct.
- Added opt-in `?navproDebug=1` console diagnostics with refresh duration/context, required-control validation, incompatible-report warnings and a visible error alert that clears after a successful refresh.
- Validation completed: embedded JavaScript syntax and representative cascade/summary fallback probes passed; Release build/package checks and all 48 policy/adapter tests passed; final installation verified 32 copied file hashes with Navisworks closed. Browser rendering and real large-project timing need target-user review.

## Dashboard title, extracted priorities, discipline shortcuts and bulk assignment (2026-10-29)

- The fallback report title is **Clash Dashboard**. Priority facets and per-row priority choices are populated from the first segment of valid `Priority_DisciplineA_VS_DisciplineB` test names, with `Unassigned` for invalid names and result rows without a valid test prefix. The `VS` delimiter is excluded from extracted values.
- Added **Select All** and **Select None** actions to Discipline A and Discipline B facets; the delegated sidebar click handler immediately updates all dependent visuals and facets.
- Added row selection, page/filtered selection, member-name validation, bulk assignment feedback, and clearing selected clashes. Assignment overrides use browser local storage; the dashboard does not write assignments to Navisworks files.
- Removed the Clash Tests section as marked in the supplied screenshot. The Clash Register remains the review and assignment surface.
- Build/package and script-level checks are still pending. Browser interaction and in-host Navisworks review are not established by source inspection alone.
- Final verification: Release build succeeded with 0 warnings/errors; all 48 policy/adapter assertions and registration checks passed; bundle metadata/assets/DLL hash checks passed. The backup-aware installer ran with Navisworks Manage closed and installed 32 hash-verified files. Browser rendering and click-through interaction still require review in the user's browser; native Navisworks assignments are not modified by Bulk Assign.

## Dashboard status initialization error repair (2026-10-29)

- Fixed the refresh failure caused by calls to a missing `refreshStatuses` function. Status facets now derive their distinct values from loaded reports' overall status counts, per-test status counts, and clash result rows, while ordering the standard five statuses first when present.
- The user screenshot confirmed the failure banner `refreshStatuses is not defined`. Build, JavaScript syntax and package checks pending; reinstallation must wait until Navisworks Manage is closed.
- Verification completed: embedded dashboard JavaScript passed `node --check`; Release build succeeded with 0 warnings/errors; all 48 policy/adapter assertions, registration and package checks passed. Navisworks Manage was running during this repair, so installation is pending the user closing the host; no process was terminated.
- Final install completed after Navisworks closed: backup-aware installer copied and hash-verified 32 files. The updated dashboard is ready for review in Manage 2024.

## Clash Register pageRows initialization error repair (2026-10-29)

- Fixed the `pageRows is not defined` dashboard error by deriving the current page slice from the filtered register rows on every render, clamping the requested page when filters shrink the result set, and updating range text plus Previous/Next disabled states.
- Build and embedded JavaScript checks pending; installation requires Navisworks Manage to be closed.
- Final verification: embedded JavaScript passed `node --check`; Release build succeeded with 0 warnings/errors; 48 policy/adapter assertions, registration and package checks passed. The backup-aware installer completed after Navisworks Manage closed and verified 32 copied files. Native host review remains separate.

## Historical chart changed to clustered columns (NavPro, 2026-10-29)

- Replaced the historical status ribbon bands with a clustered column chart: each report timestamp is a selectable group and each available status is a side-by-side column. Status colors, report counts, date labels, keyboard selection, slider/playback synchronization and filtered counts are retained.
- Added Priority facet guidance that values are parsed from the test-name prefix before the first underscore and selecting a priority filters clash data.
- Embedded JavaScript syntax and chart/priority markup checks passed; Release build, all 48 policy/adapter assertions, registration and package checks passed. Install only after Navisworks closes; actual chart appearance still needs host browser review.

## Priority auto-fill from test-name prefix (NavPro dashboard, 2026-10-29)

- Priority values now come directly from the segment before the first underscore, independently of discipline-name validation. Fixed the local-storage fallback helper argument mismatch that left the register dropdown without its parsed default. Moved explicit manual priority overrides to a versioned storage key so old inconsistent cached values do not replace the extracted prefix.
- A Node probe confirmed `M_SL-Street Lighting_VS_CS-Storm` defaults to `M` even with a legacy cached `H` override; script syntax passed. Release build, 48 policy/adapter assertions, registration and package checks passed. Installation is pending because Navisworks Manage is currently running.

## Dashboard header mark, Priority facet cleanup and clash snapshots (NavPro, 2026-10-29)

- Added an original, accessible BIM clash coordination SVG mark at the upper-right of the dashboard hero. It uses NavPro/Jacobs warm-brown supporting colors and is not a Jacobs corporate logo.
- Removed the explanatory sentence beneath the Priority facet while retaining dynamically parsed prefix values and filtering.
- Clash Dashboard setup now enables saved-viewpoint images by default. The register renders available embedded snapshots as lazy-loaded thumbnails in the Viewpoint / image column and shows the API/export note or viewpoint state when no image was generated. The export remains slower when image generation is active; results without a saved viewpoint cannot produce a snapshot through the current exporter.
- Embedded JavaScript, markup checks, Release build, 48 policy/adapter assertions, registration and bundle checks passed. Native image generation and target-browser layout remain unverified.

## Completion status pie (NavPro dashboard, 2026-10-29)

- Added a responsive completion pie beside the historical status columns. Completed is Reviewed + Approved + Resolved; Remaining includes all other available statuses. Counts and percentage derive from the same filtered records as KPI visuals. Selecting a slice cross-filters the register, and slices support keyboard activation. Empty filtered data displays an explicit empty state.
- Node probe verified the 50% example and completed-slice filter selection; embedded script syntax, Release build, all 48 policy/adapter assertions, registration and package checks passed. Browser visual layout remains to review.

## Status Updater and standard clash status changes (NavPro, 2026-10-29)

- Visible ribbon/dialog title is now **Status Updater**; the registered `ClashApprover` command ID remains stable. Removed the Resolved-only checkbox. The target dropdown is populated from Manage 2024's `ClashResultStatus` enum, confirmed locally as New, Active, Reviewed, Approved and Resolved.
- Preview reports current status counts, changes needed to the chosen target and results already there. Explicit confirmation precedes status writes; approval by/time fields are written only for Approved. All leaf status transitions are eligible, and result tests are not rerun.
- Fixed the screenshot's handle exception by suppressing ItemCheck callbacks during list refresh and calling BeginInvoke only after the dialog handle exists; Shown ensures preview initialization.
- Dashboard theme tokens now centralize foreground/surface and status-series colors; Pending is labeled explicitly, colored red, and pie tooltips/legend show counts and percentages. This follows Microsoft's recommendations to choose visuals by analytical goal, use pie/donut for a small part-to-whole set, maintain consistent theme/data colors, and enable cross-filtering.
- Official references: [Power BI visualizations overview](https://learn.microsoft.com/en-us/power-bi/visuals/power-bi-visualizations-overview) and [Power BI dashboard themes](https://learn.microsoft.com/en-us/power-bi/create-reports/service-dashboard-themes). Build/runtime validation pending.
- Final verification: Release build succeeded (0 warnings/errors), 48 policy/adapter assertions passed, embedded dashboard script syntax and Pending pie interaction/percent probe passed, registration and package checks passed. The status-updater BeginInvoke path was source-checked; native UI interaction remains for Manage review.

## Tolerance Updater — 2026-11-03

Added **Clash Review > Tolerance Updater**, with checked multi-test selection, Select all/none, explicit input units, four standard clash types and Keep current type, a before/after preview, confirmation and unchanged-test skipping. Numeric tolerances are user-defined; no universal project standards are implied. Existing simulation link/step, geometry selections, rules and composite settings are retained through a full current-test copy. Settings edits can make saved results stale; the tool does not run tests or save documents.

The installed Manage 2024 Clash assembly (21.0) was inspected for `ClashTest.CreateCopy`, `Tolerance`, `TestType` and `DocumentClashTests.TestsEditTestFromCopy(ClashTest, ClashTest)`. The core API XML documents `Document.Units` and `UnitConversion.ScaleFactor`. The separate Clash assembly has no adjacent XML. Autodesk Support documents the [editable-copy pattern for existing test tolerances](https://forums.autodesk.com/t5/navisworks-api-forum/how-to-set-the-tolerance-for-existing-clash-tests/td-p/11835427), and Autodesk Help describes the [four standard test types](https://help.autodesk.com/cloudhelp/2022/ENU/Navisworks/files/GUID-B13377D4-8AFA-4E95-8435-C6DC4DF26AF4.htm). The updater's **Re-Run Clashes** action uses the already-used API 21 signature `DocumentClashTests.TestsRunTest(ClashTest)` and is limited to checked test GUIDs.

Focused tests link the real updater service to explicit API doubles to check grouped-test discovery, conversion, selected-only edits, all four types, zero/invalid values, unchanged skipping, stale/deleted tests, duplicate/empty selections, document-unit/document switches and UI-thread guards. Detached WinForms tests render default/minimum sizes and exercise preview invalidation, empty selections, pinning and modeless Close. These do not establish native result-history preservation, Undo behavior, dirty-result flags or transaction behavior.

Native Manage acceptance still required: open a disposable model copy with mixed test types/tolerances and grouped results; compare selections/rules/simulation/composite settings before and after selected-only updates; confirm test GUIDs and existing result history; verify 30 mm converts correctly in metric and imperial documents; inspect all four native types, refresh, stale previews, native Undo and ribbon rendering at target DPI. For **Re-Run Clashes**, verify only checked tests run, results update, a stale/deleted test is rejected before the first run, and partial completion is reported if a native test fails. Native reruns are synchronous and cannot be safely interrupted. This change has not been natively exercised in Manage.

Completed checks before the Re-Run addition: Release build against installed Manage 2024 references passed with zero warnings/errors; 39 updater adapter/UI assertions and 48 policy/adapter assertions passed. Default and minimum-size dialog renders were visually inspected. Registration metadata, Clash Review placement, root/en-US package assets, 16/32px icon dimensions and packaged DLL hash checks passed. The shareable ZIP is `dist/NavPro-1.0.1-Navisworks2024.zip`. Native Manage acceptance remains pending.

### Re-Run Clashes and Clash Review order (NavPro, 2026-11-03)

- Added **Re-Run Clashes** beside **Preview changes**. It reruns only checked test GUIDs, rejects empty/duplicate/stale selections before running, resolves each live `ClashTest` immediately before `TestsRunTest`, and reports partial completion if a synchronous native run fails. A changed tolerance preview must be applied first; rerun requires explicit confirmation and warns that the host operation cannot be safely cancelled.
- Changed ribbon order to **Renamer → Tolerance Updater → Status Updater**. Build packaging asserts this order in both ribbon layouts.
- Focused adapter/UI checks verify selected-only run routing, empty/duplicate/missing test validation, action availability and minimum-size layout. Release build, all existing policy/adapter checks, metadata and package verification passed. Test doubles cannot establish real Clash Detective result updates; native Manage acceptance remains outstanding.

### Dashboard multi-test selector (NavPro, 2026-11-03)

- Added a report/snapshot-aware multi-select clash-test dropdown beside **Overall Clash Report / Discipline**, with **Select All** and **Select None**. The selected test names constrain the shared row filter, so KPIs, status totals, historical chart, discipline table, register, and cascading facets stay coordinated. Reset restores all tests.
- The embedded JavaScript passed `node --check`; static source assertions confirmed selector controls, filter-state behavior and reset handling. Release build, 48 policy/adapter checks, registration and package checks passed. Browser interaction and rendering in the target environment remain for user review.

## Clash Dashboard theme refinement (NavPro, 2026-11-03)

- Consolidated plot/surface/grid/focus colors into shared dashboard CSS tokens and applied the same status-series colors to result status badges using readable light tints and dark text. Explicit status labels remain visible. Desktop dialogs continue using the separate blue-led theme.
- Applied Microsoft Power BI theme guidance as a CSS token system for the self-contained HTML output; Power BI JSON themes are not loaded by this dashboard. Reference: https://learn.microsoft.com/en-us/power-bi/create-reports/service-dashboard-themes.
- Release/package checks and policy/adapter tests passed. Browser theme/contrast review and native Navisworks rendering remain unverified.

### Clash Tests column checkboxes in discipline summary (NavPro dashboard, 2026-11-04)

- Replaced single-click test links with a checkbox for every Clash Test name in each discipline-pair row. Checkboxes synchronize with the shared multi-test filter, support multiple selections, stay visible while a test is unchecked, and refresh all dashboard visuals and facets. The existing Select All/Select None test picker remains synchronized.
- Embedded JavaScript syntax and checkbox/filter source assertions passed. The dual-target Release build, 48 policy/adapter checks, API 21/22 registration checks and multi-version package validation passed. Browser interaction and Navisworks-host testing are not covered by these checks.

## Navisworks Manage 2025 API 22 compatibility

- The user-provided `Manage 2025 DLLs` folder contains both `Autodesk.Navisworks.Api.dll` and `Autodesk.Navisworks.Clash.dll`, each version 22.0.0.0. NavPro compiles against those references in an isolated Release2025 configuration; the 2024 Release build remains independently bound to API 21.
- The bundle manifest registers `Contents/v21/NavPro.dll` only for Nw21 and `Contents/v22/NavPro.dll` only for Nw22. Autodesk API DLLs remain reference-only and are not packaged. Each target uses direct file-path references so same-named DLLs elsewhere in the workspace cannot cause MSBuild to bind a target to the wrong major version.
- The Manage 2025 binary and registration metadata checks passed. The full package build produced `dist/NavPro-1.0.3-Navisworks2024-2025.zip`; both release builds had zero warnings/errors, 48 policy/adapter assertions passed, each plug-in's API and Clash references matched the requested major, and bundle runtime entries, paths, assets and DLL hashes passed.
- This machine has no Navisworks Manage 2025 host, so loading the bundle, ribbon rendering and native clash workflows still require acceptance in Manage 2025. These checks prove compile-time and package compatibility, not native runtime behavior.

### Loaded Reports shortcuts, dashboard identity and contact link (NavPro 1.0.4 source, 2026-11-05)

- Changed the Loaded Reports header shortcuts to **All** and **None**, styled to fit beside the heading. Their event handler is bound directly to that control group and changes only `filterState.report` plus the Loaded Reports checkboxes; it does not change clash-result selections or Priority, Discipline A/B, Clash Status, or test filters.
- Replaced the previous generic coordination badge with a custom inline SVG emblem showing intersecting model elements and an impact point in the dashboard's Jacobs brown/gold palette with a restrained red impact accent.
- Added a sidebar **Contact Info** mail link with the support address already used by NavPro. The address is stored once in `NavProIdentity.ContactEmail` and consumed by both the desktop Contact Info dialog and the HTML report.
- No installation was performed. The `dotnet` CLI and unsandboxed MSBuild startup returned access denied in the default execution context; direct Visual Studio MSBuild with approved elevated execution compiled both targets successfully (2024/API 21 and 2025/API 22). Metadata-only registration checks passed for both binaries. The versioned bundle and `dist/NavPro-1.0.4-Navisworks2024-2025.zip` were assembled and manifest/runtime declarations plus binary copy hashes were checked. The policy/adapter test executable was built but not run. Native dashboard/browser interaction and Navisworks host rendering were not tested. Existing installed versions are unchanged.
### NavPro magenta theme from supplied swatch (1.0.6 source, 2026-11-05)

- Replaced the previous blue desktop brand and warm brown dashboard brand with the latest user swatch, sampled at approximately `#A500A4`. Shared dialog tokens now drive owned tool headers, primary actions, grid headings, selected rows/tabs and focus accents. The standalone HTML dashboard uses the same brand plus a matching header gradient. The small tabular HTML companion also uses the new brand header. Semantic clash statuses remain distinct; Reviewed changed from brown to mauve.
- Updated theme documentation, README and package metadata to version 1.0.6. The 2024/API 21 and 2025/API 22 binaries compiled successfully with Visual Studio MSBuild. Metadata-only registration checks passed for both targets. The test executable was built but not run. Bundle assembly completed as `dist/NavPro-1.0.6-Navisworks2024-2025.zip`; the source manifest, both runtime/module mappings, binary copy hashes and ZIP creation passed.
- No installation or native Manage/browser visual review has been performed.
### NavPro 1.0.6 installation (2026-11-05)

- Installed with `Tools/Install-NavPro.ps1` to `%APPDATA%\Autodesk\ApplicationPlugins\NavPro.bundle`. The prior bundle was preserved at `%LOCALAPPDATA%\NavPro\Backups\NavPro-20261105-173729-159.bundle`.
- Installer reported 67 hash-verified files. Follow-up checks confirmed manifest version 1.0.6 and matching installed v21/v22 plug-in and ribbon XAML hashes. Navisworks was closed during installation. The dashboard/dialog appearance still needs review in a live Navisworks session; compilation and file hashes are not host UI verification.
- 2026-11-05: Dashboard output now embeds the user-provided Jacobs logo, hides the folder-refresh details, dated-snapshot count, and current snapshot summary visually while keeping their live text screen-reader accessible, and always shows New, Active, Reviewed, Approved and Resolved KPI cards (zero-filled if absent). Build/package validation required; installation and native browser/Manage verification are pending.

- 2026-11-06: Replaced the dashboard emblem with the user-selected Option A: a bounded faceted hexagon, crossed structural beams, and coral impact spark. SVG and CSS constrain the desktop emblem to 88 px and mobile to 68 px. Build/package and registration checks required; host rendering not yet verified.

## Navisworks 2026 support

- Added a separate Release2026 build targeting API 23.0.0.0 from Manage 2026 DLLs, and registered it for Navisworks series Nw23. The existing API 21/Nw21 and API 22/Nw22 binaries remain separate.
- The backup-aware installer now requires v21, v22 and v23 payloads. Build-Package validates all three API assemblies and registrations; ZIP creation is opt-in with -CreateZip, and -SkipTests supports builds without running the test executable.
- 2026-11-10: `Tools/Build-Package.ps1 -SkipTests -CreateZip` completed successfully. Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds each passed with zero warnings/errors; registration metadata passed for all three; v21/v22/v23 manifest mappings, locale assets, icon sizes and payload hashes passed. The test executable was skipped. Native Manage 2026 loading and dashboard output remain unverified.

- 2026-11-10: Removed the dashboard header SVG/logo per user markup. The earlier Option A mark is not present in the 1.0.11 HTML output.

## Import Clash Status (NavPro 1.0.12, 2026-11-10)

- Added **Import Clash Status** after Status Updater in the Clash Review ribbon. It accepts NavPro Dashboard XML and CSV exports, lists the report's standard statuses for selection, previews match outcomes, and requires explicit confirmation before status updates.
- Result matching uses test name, clash name, and both item names; CSV model columns further disambiguate repeated item names. Only unique matches with a changed status are eligible. Conflicting duplicate targets, ambiguous/unmatched/incomplete rows and statuses excluded by selection remain unchanged. Approved updates set the current updater name and approval time, following Status Updater behavior. Assignments and test runs are not imported.
- Built Manage 2024/API 21, Manage 2025/API 22 and Manage 2026/API 23 with zero warnings/errors. Registration metadata checks passed for all three, and the v21/v22/v23 bundle, ribbon assets, icon sizes and DLL hashes passed. Policy/adapter tests were skipped; no native Navisworks host interaction was run. No installation or ZIP creation was performed.

## Standard Navisworks XML import compatibility correction (2026-11-10)

- The user-provided sample is a standard Navisworks Clash Detective exchange XML (`exchange/batchtest/clashtests/clashtest/clashresults/clashresult`), with per-result status in `resultstatus` and no exported item names. The importer now parses both this structure and NavPro Dashboard XML. Where item names are absent, it matches by test and clash name, and leaves multiple matches ambiguous rather than guessing.
- All three binaries and registration/bundle checks passed after the parser change. Policy tests were skipped. Navisworks Manage was open during the follow-up, so installation is pending closure; no host test has been run.

## Clash Dashboard dark analytics styling (NavPro 1.0.13, 2026-11-10)

- Adapted the supplied dashboard screenshot to the existing HTML dashboard without adding unsupported tabs or changing filter/data interactions. The self-contained report now uses dark slate surfaces, compact left filter rail, elevated cards, dark table surfaces, magenta active/focus accents and consistent distinct clash status colors. Added responsive layout adjustments, keyboard focus visibility, reduced-motion handling and light print styling. The header remains without a logo/icon.
- Manage 2024/API 21, Manage 2025/API 22 and Manage 2026/API 23 builds passed with zero warnings/errors; metadata and v21/v22/v23 bundle/icon/hash checks passed. Policy tests were skipped. No installation, ZIP creation, browser rendering review, or in-host test was performed.

## Tolerance Updater test filter (NavPro 1.0.14, 2026-11-11)

- Added a name filter in the settings row beside tolerance units. Matching is case-insensitive and updates visible rows immediately. Filtering preserves checkbox selections; bulk selection, preview, and rerun continue to use checked tests across the full list.
- Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds passed with zero warnings/errors. Registration and v21/v22/v23 bundle, locale asset, icon size and DLL hash checks passed. Policy/adapter tests were skipped; the dialog was not rendered in Navisworks. No installation or ZIP creation was performed.

## Dashboard Priority All/None controls (NavPro 1.0.15, 2026-11-11)

- Added **All** and **None** controls beside the HTML dashboard's Priority heading. They use the existing priority facet state and only change priority selection; loaded reports and other facets are unaffected.
- Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds passed with zero warnings/errors. Registration and bundle checks passed. Policy/adapter tests were skipped; dashboard browser rendering was not reviewed. No installation or ZIP creation was performed.

## Manage 2026 DLL folder review (2026-11-11)

- Inspected assembly metadata across 606 DLL files in `Manage 2026 DLLs`: 198 managed .NET assemblies and 408 native/non-.NET DLLs. All 11 `Autodesk.Navisworks.*` managed assemblies found report version 23.0.0.0; `Autodesk.Navisworks.Api.dll` and `Autodesk.Navisworks.Clash.dll` are present at the required 2026 API version. No duplicate managed assembly identity with conflicting versions was found.
- Existing Release2026 configuration references this folder and enforces API/Clash assembly major version 23. NavPro 1.0.15's 2026 build and metadata/package checks passed with zero warnings/errors. No source compatibility changes were needed. This does not verify plugin loading or tool behavior in a live Manage 2026 session. No installation or ZIP creation was performed.

## Status Updater filter and Clash Matrix model list layout (NavPro 1.0.16, 2026-11-11)

- Added a case-insensitive Status Updater test-name filter. The complete test list and checked GUIDs are retained independently of visible rows; filtering rebuilds the CheckedListBox under a loading guard so hidden checks persist. Select all/none keep their prior all-tests scope, and preview/update use the authoritative checked GUID set.
- Expanded the Clash Matrix Loaded Models layout share from 18% to 30%, rebalanced adjacent panels, and enabled a horizontal scrollbar with extent measured from the longest displayed model label.
- Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds passed with zero warnings/errors. Registration and bundle checks passed. Policy/adapter tests were skipped; dialogs were not visually rendered or tested in Navisworks. No installation or ZIP creation was performed.

## NavPro dashboard logo option 1 (NavPro 1.0.17, 2026-11-11)

- Added the selected angular BIM-beam logo and NavPro wordmark to the top-right of generated HTML dashboards. The PNG is embedded into the plug-in and emitted as a data URI, so each report remains a single portable HTML file.
- The desktop header reserves space to prevent overlap with long report names; narrow layouts place the brand beneath the report metadata.
- Manage 2024, 2025 and 2026 builds passed with 0 warnings/errors. Bundle registration and packaging checks passed for v21/v22/v23. Policy/adapter tests were skipped. No Navisworks host test or installation has been performed for this change.

## Weekly analytics dashboard (NavPro 1.0.18, 2026-11-11)

- Moved NavPro branding into a persistent top-left dashboard bar with the embedded Option 1 mark and wordmark.
- Replaced the daily clustered chart with a weekly status trend line and absolute stacked weekly status columns. For each project and calendar week, the latest dated report is used; weeks without loaded snapshots have no data point. The timeline slider/playback now navigates weekly points.
- Updated KPI cards to Total, New, Active, Approved, Resolved and Resolution Rate. Resolution Rate is Resolved divided by Total.
- Changed the completion visual to a current-week five-status donut; slices remain clickable status filters.
- Added a top-six discipline-pair clash-count heatmap whose cells set both discipline filters, and a selected-week vs previous available weekly snapshot comparison table.
- Preserved the existing filterable/searchable, paged Clash Register and other dashboard sections. Analytics use the same report, priority, discipline, status, test and search filters as the register.
- JavaScript syntax check passed. Manage 2024, 2025 and 2026 builds passed with 0 warnings/errors. Registration and bundle path/hash checks passed for v21/v22/v23. Policy/adapter tests were skipped. No native Navisworks host test has been run.

## Dashboard empty-data rendering fix (NavPro 1.0.19, 2026-11-11)

- Restored report loading, row normalization, filter, and selection helpers that were dropped during the weekly analytics edit. Fixed selected-report construction to compare report IDs rather than report objects; this prevented the charts, KPIs, and register from receiving any selected reports.
- Ran the embedded dashboard script against the user's exported HTML report: parsed all 32,300 clash results and 46 tests; verified non-empty KPI cards, weekly snapshot data, discipline heatmap and Clash Register markup. Node syntax and runtime initialization passed. The report's generated HTML must be re-exported with this build to see the fix.
- Manage 2024, 2025 and 2026 builds completed with 0 warnings/errors; registration metadata and package path, locale asset, icon-size and binary-hash checks passed. Policy/adapter tests were skipped. With Navisworks closed, the backup-aware installer installed 111 hash-verified files to `%APPDATA%\Autodesk\ApplicationPlugins\NavPro.bundle`; prior bundle backed up at `%LOCALAPPDATA%\NavPro\Backups\NavPro-20261111-140101-305.bundle`. No native Navisworks host test or real browser visual review has been run.

## Team Manage 2026 ribbon report (NavPro 1.0.19, 2026-11-11)

- Inspected the team-share artifacts. The newest ZIP present was NavPro 1.0.15; it includes the v23 DLL, ribbon XAML/assets, and Nw23 runtime targeting, so the archive itself has a Manage 2026 component but is older than the current release. The current bundle folder is 1.0.19. Its top-level AppVersion was 1.0.19 while each ComponentEntry Version remained 1.0.16; corrected those entries to 1.0.19 and rebuilt the bundle folder.
- All three target builds, metadata registration, package paths, v21/v22/v23 targets, locale assets and DLL hashes passed. No ZIP was generated and no installation was performed for this metadata-only refresh. The teammate's installation path and Navisworks App Manager state were not available, so the actual missing-ribbon cause is not confirmed.

## Week-over-Week value alignment (NavPro 1.0.20, 2026-11-11)

- Center-aligned all numeric cells in the Week-over-Week comparison table (previous week, selected week, change count, and change percent) to match the markup. This is a dashboard style change; it does not alter the light/dark theme or calculations.
- Manage 2024, 2025 and 2026 builds completed with 0 warnings/errors. Registration and bundle checks passed for v21/v22/v23. Policy/adapter tests were skipped. No installation or ZIP creation was performed; a native browser review is pending.

## Dashboard heading wording (NavPro 1.0.21, 2026-11-11)

- Renamed **Clash Intensity by Discipline Pair** to **Clash Intensity by Discipline** and **Weekly Status Mix** to **Weekly Clash Status** to match the markup. Chart data and behavior are unchanged.
- Manage 2024, 2025 and 2026 builds completed with 0 warnings/errors. Registration and package checks passed for v21/v22/v23. Policy/adapter tests were skipped. Installation was attempted but the installer detected Navisworks running and refused to replace the bundle; installation and in-host/browser verification remain pending.

## Dashboard headings, register tolerance and report notes (NavPro 1.0.22, 2026-11-12)

- Dashboard headings now follow the latest markup: **Clash Intensity by Discipline** and **Weekly Status**.
- Added **Clash Tolerance** between Coordinates and Assigned To in the Clash Register. Values come from each result's parent clash test, use the report's display units, and are included in CSV exports. Older imported reports without a tolerance field show a blank value.
- Replaced the weekly snapshot caption below the project title with an editable report remarks/contact-notes field. Text is saved in the browser's local storage for that report; report timestamps remain in the underlying snapshot data and timeline but are omitted from the project-title area.
- Manage 2024, 2025 and 2026 package builds completed with 0 warnings/errors. Registration metadata and v21/v22/v23 package path, locale asset and binary hash checks passed. Policy/adapter tests were skipped. No installation was performed; dashboard rendering, remarks persistence and tolerance display still need review in a real browser or Navisworks session.


## Dashboard header timestamp and units removed (NavPro 1.0.23, 2026-11-12)

- Removed the generated report timestamp and display units line from below the project name, leaving the editable remarks/contact-notes field. Snapshot timestamps and display-unit data remain available to dashboard timeline and report calculations; this change only removes the header text.
- Manage 2024, 2025 and 2026 builds completed with 0 warnings/errors. Registration and package checks for v21/v22/v23 passed; policy/adapter tests were skipped. No installation or browser/native host review was performed.

## Refresh Models command (NavPro 1.0.24, 2026-11-17)

- Added **Clash Review > Refresh Models** using the public `Document.UpdateFiles()` API. Autodesk documents this operation as updating loaded files to match changed files on disk; the local Manage 2024/2025/2026 API assemblies expose the method in API 21/22/23.
- The command refreshes existing file references only. It does not discover or append new files, remap moved/renamed paths, rerun clash tests, or save the NWF. The UI confirms before invoking the synchronous host API and advises reviewing/rerunning clashes. Native refresh behavior, file-lock handling, and result freshness still require a live Navisworks review.
- Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds completed with 0 warnings/errors. Registration metadata passed for all three; v21/v22/v23 runtime mappings, root/en-US ribbon assets, 16/32 px icon dimensions and payload hashes passed. Policy/adapter tests were skipped.
- Policy/adapter test executable: skipped. No installation or native Navisworks host test performed.

## Tolerance Updater Composite Object Clashing (NavPro 1.0.25, 2026-11-17)

- Added a control in the marked area with **Keep current**, **Enabled** and **Disabled** choices. Preview rows show current and proposed composite settings, and a choice change invalidates the current preview.
- The service reads/writes `ClashTest.MergeComposites` using a full copy of each existing test. Keep current preserves mixed existing settings; explicit changes are applied only to checked tests. Existing selections, rules, and clash results are not intentionally replaced; tests are not run or saved automatically.
- Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds completed with 0 warnings/errors. Registration metadata passed for all three; v21/v22/v23 runtime mappings, ribbon assets, icon dimensions and payload hashes passed. Policy/adapter tests were skipped. No installation or native Navisworks dialog/application test performed.

## SetCreator count failure and Clash Matrix model/units controls (NavPro 1.0.26, 2026-11-18)

- Replaced the per-value `Search.FindAll` calls used only for SetCreator preview counts with occurrence counts accumulated during the existing typed model-property scan. Option B performs a whole-document count scan for selected filename keys because created Search Sets remain document-wide. This addresses reported native exceptions (`Object reference not set` and `arrayIndex` out of range) from the count stage; dynamic query counts still need comparison against representative user NWF/NWC files.
- Clash Matrix Standard now presents one loaded model root per row, named from the model filename; Compact retains child model-tree nodes. Increased the A/B list allocation from 31% to 40% of the flexible dialog height while keeping the loaded-model and preview regions usable.
- Added all API 21 `Units` distance-unit enum values to the Clash Matrix tolerance dropdown. The chosen input tolerance is converted to document units before building/saving tests, and preview summary displays both units.
- Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds completed with 0 warnings/errors. Registration metadata passed for all three; v21/v22/v23 runtime mappings, root/en-US ribbon assets, 16/32 px icon dimensions and binary hashes passed. Policy/adapter tests were skipped. No installation or native Navisworks model/clash verification performed.

## Colorizer (NavPro 1.0.27, 2026-11-18)

- Added **Clash Review > Colorizer** for loaded Models / Files or saved Selection / Search Sets.
- Random Colors assigns separated HSL hues and applies public Navisworks permanent color overrides. Excel export creates an editable `.xlsx` with target type/name/key and `Color Hex` plus RGB columns; import validates target names, keys when supplied, duplicate rows and complete coverage of the selected scope before applying.
- With no targets checked, the operation scopes to every current target in the selected mode. The dialog explains workbook matching, saving the NWF/NWD, overlapping-set last-color behavior, and render modes that may hide appearance overrides. Autodesk API operations remain on the host UI thread; failures report that partial native appearance changes may have occurred.
- Verification completed: 2024 / 2025 / 2026 builds succeeded with zero compiler warnings/errors; registration metadata and bundle/ribbon/icon/payload checks passed. Policy/adapter tests were skipped. Native color appearance, Excel opening/editing/import round trip, and saved NWF/NWD persistence have not been verified in a live Navisworks session.

## Clash Matrix tolerance unit interaction (NavPro, 2026-11-19)

- Changing the tolerance input unit converts the numeric value to preserve the same physical distance, using API `UnitConversion.ScaleFactor`; the input displays six decimal places. Existing preview rows and summary are refreshed in document units without recounting selected sets. If conversion exceeds the input range or displayed precision, the previous unit/value remain selected and the status explains why.
- `Document.Units` is get-only in the installed Manage 2024/API 21 assembly. This workflow changes the Clash Matrix input units and tolerance, not the host document's unit setting.
- Build verification pending. No installation or native Navisworks interaction performed.

## Smart Review and grouping — NavPro 1.0.28 (2026-11-23)

Added six commands: Smart Clash Grouper in Clash Review; Chainage-Based Clash Locator, AI Clash Description Assistant, Utility Separation Checker, Access and Maintenance Clearance Review and Coordination Change Log in the new Smart Review panel. Existing IDs/layout membership are preserved. See Docs/Smart-Review.md for scope and source references.

Installed API 21 metadata confirms TestsAddCopy(GroupItem,SavedItem), TestsMove(GroupItem,int,GroupItem,int), ClashResult composite items, TestsEditResultDescription, TestsViewpointForResult, document path IDs and model/clash/set change events. Native operations are on the host UI thread. Async AI calls receive detached limited metadata, have cancellation and stale-snapshot checks, and never run automatically. No provider calls or model mutations were performed during development.

Focused detached checks cover exact line/arc chainage/offset, LandXML axis order and invalid geometry rejection; grouping preservation/selection; stale signatures and revision events; description-only edits; spatial rule validation and created-GUID run scope; local/cloud protocol payloads and cancellation using HTTP mocks; CSV quoting/formula mitigation; per-document memory journal; and six offscreen dialog variants at default/minimum sizes. The first focused run passed 244 assertions. Final release/package results are recorded after the complete verification run.

Native Manage acceptance remains separate: review real result/group history, approval metadata, saved viewpoints, Undo and persistence; compare known alignment locations/units and loaded model transforms; run native utility/envelope checks on representative geometry including containment cases; verify the full ribbon and all dialogs at workstation DPI. AI service authentication/output, local model availability, provider privacy/limits and real endpoints remain untested. Maintenance is a surface-intersection workflow and does not certify full envelope occupancy. Installation does not establish these host checks.

Final verification: Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds passed with zero warnings/errors; 246 focused Smart Review geometry/adapter/HTTP-mock/UI assertions and the existing tolerance plus 48 policy/adapter assertions passed. Registration, six new command labels/placement, root/en-US assets, icon dimensions and payload hashes passed. Six dialog variants were rendered and visually inspected at minimum/default sizes. No live model or AI provider tests were performed.


Installation completed 2026-11-23T10:45:09.0786615Z UTC: NavPro 1.0.28 installed to C:\Users\RAJESHP\AppData\Roaming\Autodesk\ApplicationPlugins\NavPro.bundle with Navisworks closed. Previous bundle backup: C:\Users\RAJESHP\AppData\Local\NavPro\Backups\NavPro-20261123-144459-230.bundle. Installed payloads were hash-verified. Installation is separate from native project-model acceptance; that acceptance remains pending.


## Smart Review dropdown — NavPro 1.0.29 (2026-11-23)

The Smart Review panel now contains one menu-only NWRibbonSplitButton with five compact horizontal command entries. Command IDs, labels, icons and order remain unchanged. The parent keeps its own Smart Review label after selection. The installed Manage 2024 VaultTab.xaml confirms the native NWRibbonSplitButton pattern; reflection-only API inspection confirms IsSplit, IsSynchronizedWithCurrentItem, ListStyle and ListImageSize and their enum values. Package checks now enforce one dropdown, its menu settings, exact membership/order, compact item presentation and unique command IDs. Native ribbon rendering and menu dispatch remain host acceptance checks.

Manage 2024/API 21, 2025/API 22 and 2026/API 23 builds passed with zero warnings/errors. Existing tolerance and 48 policy/adapter assertions plus 246 Smart Review checks passed. Command registration, dropdown structure/membership, locale assets and payload hashes passed. Native ribbon rendering/dispatch have not been tested. Installation completed with Navisworks closed: 208 installed files matched the packaged SHA256 hashes. Previous bundle backup: C:\Users\RAJESHP\AppData\Local\NavPro\Backups\NavPro-20261123-145827-469.bundle. Navisworks Manage 2024 was then launched for user acceptance testing.

## Smart Review dropdown visibility — NavPro 1.0.30 (2026-11-23)

The 1.0.29 dropdown header was absent from Command attributes and CanExecuteCommand returned IsVisible=false for its ID. Registered the existing SmartReviewMenu ID, added explicit enabled/visible state and a no-op header dispatch, preserving all child commands. Registration verification now rejects any native ribbon button/split-button without exactly one matching Command attribute. Native Manage 2024 confirmation: opened the NavPro tab, observed the Smart Review panel/header, opened the dropdown and visually confirmed all five correctly labeled menu entries. Child tool execution and model operations were not tested in this acceptance check. Manage 2024/API21, 2025/API22 and 2026/API23 builds passed with zero warnings/errors. Existing tolerance and 48 policy/adapter assertions plus 246 Smart Review checks passed, as did registration and bundle checks. Installed with Navisworks closed; 208 files hash-verified. Previous bundle backup: C:\Users\RAJESHP\AppData\Local\NavPro\Backups\NavPro-20261123-151305-582.bundle. Manage 2024 launched and its Smart Review dropdown was visually verified.

## Description assistant simplified controls — NavPro 1.0.31 (2026-11-23)

Removed the marked Mode, Endpoint, Model ID and API key fields, plus the cloud-consent checkbox that no longer applies. The dialog explicitly uses offline templates without service setup/network calls. Style now selects concise, detailed or review-note formats; edits clear drafts. Reviewed-description-only apply, snapshots, confirmation and explicit generation remain. Offline generation checks cancellation and yields between detached rows to allow cancellation. Added checks for offline formats, no credentials, cancellation, removed controls and style invalidation. Manage 2024/API21, 2025/API22 and 2026/API23 builds passed with zero warnings/errors. Existing tolerance checks, 48 policy/adapter assertions and 244 Smart Review checks passed, including new offline workflow checks. Rendered 1060px and 1200px dialog previews were visually inspected; marked controls are absent and remaining controls fit. Registration, dropdown structure, locale assets and payload verification passed. No native host description application test was performed. Installation completed 2026-11-23T15:29:49.5081569+04:00 with Navisworks closed. All 208 installed files were hash-verified; prior bundle retained in the backup directory. Native description application remains untested.

## Resizable tool regions — NavPro 1.0.32 (2026-11-23)

Added shared draggable split regions for Clash Matrix loaded models, selections A/B and preview; SetCreator Option B models/preview; Colorizer instructions/targets; Status Updater tests/summary; Import Clash Status settings/results; and shared Smart Review tests/results (Grouper, Chainage Locator, Description Assistant). Single-grid tools retain their existing fill/resize behavior. Model lists support horizontal scrolling. Window expansion retains divider proportions; double-click resets a divider. Existing controls, event handlers, checked items and preview data are retained. A nested-layout fixture verifies divider movement, expansion/shrink restoration, retained selection/data, containment and fixed footer visibility; shared Smart Review dialogs were rendered at minimum, default and 1600px sizes. No new Autodesk API calls or model changes were added. Manage 2024/API21, 2025/API22 and 2026/API23 builds passed with zero warnings/errors. Existing tolerance and 48 policy/adapter assertions plus 337 Smart Review assertions passed. Metadata, dropdown layout, root/en-US assets and bundle hashes passed. Minimum/default/expanded shared dialogs and the nested resizing fixture were rendered and inspected. Native interaction with these new dividers in Navisworks remains untested. Installed 1.0.32 with Navisworks closed; all 208 payload files were hash-verified. Prior bundle backup: C:\Users\RAJESHP\AppData\Local\NavPro\Backups\NavPro-20261123-153806-308.bundle.

## Renamer, Contact Info and persistent windows — NavPro 1.0.33 (2026-11-23)

Added naming presets, token patterns and checked-row numbering with configurable start/step/padding. Preview computes the complete selected pattern batch before staging any rows, reads original names and does not rename model data. Current/proposed names and a pair of read-only rich-text panes expose changed spans. Duplicate/empty/invalid targets block Apply; the service additionally rejects stale original names before its existing native rename calls. No new Autodesk API members were introduced.

Contact Info now displays plug-in file version, supported releases, built-in release notes and visible, explicitly copied diagnostics without model paths/names, usernames or credentials. Shared UI preferences remember normal window bounds/maximized state, split ratios, existing filter text and visible grid widths, with monitor/DPI handling, atomic file replacement, bounded input and corrupt-file fallback. Utility/maintenance profiles are distinct. DialogTheme attaches once per window; dashboard setup attaches directly. Tests use isolated temporary preference roots rather than the user's preferences.

Focused pattern, collision, before/after highlighting, stale rename adapter, original child preservation, preference persistence and corrupted-file checks were added. Minimum/default/expanded layouts are rendered for Renamer and Contact Info in addition to existing review forms. Manage 2024/API21, 2025/API22 and 2026/API23 builds passed with zero warnings/errors. Existing tolerance tests and 48 policy/adapter assertions plus 449 focused review/pattern/support/preferences checks passed. Registration, locale ribbon assets, dropdown membership and payload hashes passed. Renamer and Contact Info default/minimum/expanded layouts were rendered; Contact Info release-note and diagnostics tabs were also rendered. Preference checks include atomic updates, restored size/filter/column/divider/maximized state, removed-monitor positioning and corrupt-file fallback. Native host renaming and cross-session/DPI behavior remain separate acceptance tests. Installation completed with Navisworks closed; all 208 files were hash-verified. Previous bundle backup: C:\Users\RAJESHP\AppData\Local\NavPro\Backups\NavPro-20261123-191611-066.bundle.

## Native acceptance — 1.0.33 / 1.0.34 (2026-11-23)

Using an isolated copy of Autodesk's gatehouse sample under Tests/Artifacts/Native-1.0.33, not the user's production model. In installed Manage 2024/API21, Contact Info displayed installed file version 1.0.33.0, both API versions 21.0.0.0, supported releases, release notes and diagnostics. Copy diagnostics displayed success. Maximizing, closing and reopening restored maximized state and produced a preferences file; normal state was restored after this test.

Native Clash Matrix inspection found the fixed-height profile could exhaust the lower split pane, leaving the preview too short to see. Version 1.0.34 moves that profile into a dedicated scroll viewport and keeps model/selection/preview splits independent. Verification of this correction and the remaining native workflows is in progress.

## 1.0.35 review enhancements — 2026-11-23
See [the tool-check summary](Docs/Tool-Checks-2026-11-23.md) and [workflow scope](Docs/Review-Enhancements-1.0.35.md). Review fixtures pass 546 checks; policy/tolerance checks pass. Native checks on the supplied 47 MB federation remain pending because a Jacobs IT security prompt blocks desktop input. Initial 1.0.35 installation was hash-verified while Roamer was closed; final package/install results are recorded in the session. Geometry limits are explicit; unsupported directional utility and spiral inputs are not treated as verified.

## Live federation checks and 1.0.36 fixes — 23 November 2026

All 16 tool windows received live checks in Manage 2024/API 21 using copies of the supplied federation (46 baseline tests, 34,662 leaf results, 53 sets). This supersedes the earlier desktop-security blocker as the current status. Read [the detailed tool-check report](Docs/Tool-Checks-2026-11-23.md) for per-tool scope, limitations and outstanding acceptance.

Native creation/rerun of one QA test, its tolerance update/rerun, creation of one search set and one eight-result group completed. Description/template validation, Renamer and Status previews, input guards, published-NWD refresh, persistent history and dashboard export/browser checks were exercised. Status import loaded 34,662 rows and eventually previewed 28,623 already-current/6,039 incomplete rows; its prolonged full-list search is replaced with indexed matching in 1.0.36. No status import or description writes were performed.

Initial dashboard export has 9,001 native images/34,662 rows, then contiguous native state errors; complete image coverage did not pass. First export host later exited unexpectedly; cause unconfirmed. Revision capture exceeded the old 8 MB JSON limit. The existing journal remained readable. Version 1.0.36 adds a history-specific 128 MB bound, UTF-8 byte-size validation before replacement, and omits unused item metadata during revision capture. It also fixes dashboard launcher clipping, empty palette guidance, transform-validation wording and grouper completion feedback. The final package build passed all three API-matched release builds with zero warnings/errors, tolerance plus 48 policy checks, 549 Smart Review fixture checks, registration and payload verification. These fixture checks use doubles/offscreen forms and are separate from native regression after installation.

No approved alignment/control points, utility rules or maintenance envelopes were supplied. Native geometry acceptance for those tools is partial; spirals/decreasing station progression and directional/crossing utility geometry remain unsupported. Original federation hash was rechecked unchanged.

Installation: 1.0.36.0 installed with all Roamer processes closed; 208 payload hashes verified. Backup: `%LOCALAPPDATA%/NavPro/Backups/NavPro-20261123-211159-811.bundle`. QA review copy saved independently of the supplied original. Installed regression remains in progress.

Installed 1.0.36 native regression: full 34,662-row status matching returned the same classifications within an approximately 3.34-second click/capture observation. Two native revisions of 34,662 results each persisted to a 21,704,913-byte history and compared unchanged. Dashboard Export/Close visible at normal/maximized sizes. Single-host New-only export produced 23/23 images, including 17 previously failed rows. Full 34,662-row image coverage remains uncertified. All 16 tools received scoped native checks; partial coverage and input/geometry limits are detailed in the report.

## Clash Dashboard image resource correction — 1.0.37 (24 November 2026)

Read-only inspection of the installed API 21 `TestsImageForResult` IL found an internal `LcOwDIB` local passed to bitmap conversion without explicit disposal. NavPro already disposed the returned managed bitmap. A single-host 1.0.36 full-image reproduction shows the process GDI count rising with renders (1,898 → 5,096 in the initial monitor samples). This supports deferred native-wrapper accumulation as the failure mechanism; the reproduction and corrected native coverage are recorded below when complete. No Windows graphics quota or registry setting is changed.

The scoped workaround forces collection/finalization before image capture starts, after each 128 results and when its capture scope closes, including exceptional exits. An invalid native image state gets one cleanup/retry of the same clash and style; persistent failures retain an explicit row note. All native image calls stay synchronous on the host UI thread. Progress reports processed/image counts without DoEvents or claiming safe interruption. The launcher blocks close during export and retains the final image summary.

The review fixture passes 558 assertions, including 34,662 simulated opaque finalizable DIB handles with bounded process GDI usage, JPEG decoding, returned-bitmap disposal, bounded retries, persistent failure notes and final-batch cleanup. The simulation does not call Autodesk or prove native image coverage. Full package and installed native verification are pending at this point in the record.

## Vertical Clearance Checker — 1.0.38

Added one registered compact command under Smart Review, with a public COM geometry extraction adapter and a detached vertical-gap engine. Source selection supports current model selections and existing saved sets; no native clash tests or model data are created, edited or saved. The engine uses matching XY footprints and Z differences, transformed triangles, non-overlapping TIN validation, closed pipe-mesh checks, coverage states and a decision-tolerance band. See [the workflow and limits](Docs/Vertical-Clearance-Checker.md).

The package build passed release-matched API 21/22/23 compilation with zero warnings/errors, policy/tolerance checks, 672 Smart Review checks, command/XAML metadata, root/localized icon/layout assets and SHA256 validation. New numerical controls include sloped surfaces, interior valleys, full/partial/no coverage, negative cover, open/stacked/duplicate/vertical geometry, survey coordinates, fragment rotation/translation and feet/metre conversion. Populated dialog renders were inspected at 1100, 1360 and 1600 pixels; controls and the scrollable profile fit. These are numerical/adapter/offscreen WinForms checks, not native COM geometry acceptance.

Manage 2024 and the supplied 2026 references include matching COM bridges. The supplied 2025 references do not, so vertical checking is disabled with an explicit availability message in that target. Existing tools remain compiled against API 22. Native project geometry/control measurements and full corrected 34,662-image dashboard coverage are not yet certified. Installation is recorded separately after the final package check.

Installation completed 24 November 2026 with all Roamer processes closed: NavPro 1.0.38.0, 221 installed files matched the packaged SHA256 hashes. Prior bundle retained at %LOCALAPPDATA%/NavPro/Backups/NavPro-20261124-133921-571.bundle. Installed Manage 2024 ribbon XAML contains Vertical Clearance Checker under SmartReviewMenu; installed and packaged API 21 DLL hashes match. This confirms deployment/registration payloads, not native ribbon rendering or project geometry acceptance.


## Manage 2026 compatibility audit — 1.0.39 (24 November 2026)

Inventoried and SHA256 hashed all 607 supplied reference-folder files (198 managed assemblies, 408 native binaries, one other file). Core, Clash, ComApi and Interop.ComApi identify as API 23.0.0.0. Kept host references private=false and separate API 21/22/23 binaries; changed bundle registration to one Components scope per release, with a packaging guard against mixed scopes. Build/metadata guards check matching COM bridge identity and require its presence in the 2026 binary. Stable plug-in and command identities remain unchanged.

The full 1.0.39 package run passed all three release builds with zero warnings/errors, tolerance and 48 policy/adapter checks, 672 Smart Review fixture checks, all three metadata registration checks, release-specific runtime/module validation, locale ribbon/icon assets and SHA256 checks. Fixtures and metadata checks do not execute Autodesk host workflows. See [the compatibility audit](Docs/Navisworks-2026-Compatibility.md).

Manage 2026 is not installed here, so native 2026 discovery, geometry, clash writes and rendering remain unverified. Installation of 1.0.39 is deferred because Manage 2024 is open; the installed bundle remains 1.0.38. No host process was terminated. The distributable includes the v23 binary for installation with Navisworks closed.


Installation follow-up, 24 November 2026: Navisworks was closed at the installer check. The backup-aware installer installed 1.0.39.0 with all 222 payload hashes verified. Previous bundle: `%LOCALAPPDATA%/NavPro/Backups/NavPro-20261124-135556-394.bundle`. Installed v21/v22/v23 binaries all report 1.0.39.0. API 23 DLL SHA256: `BD7B65E32D48A0796F1FE5FE5A04F90909E61FC5FAEF069D0B9AD2D9F1E4F612`. This supersedes the installation deferral above; native Manage 2026 testing remains pending.

## Vertical Clearance source UI — 1.0.40 (24 November 2026)

Both pipe/surface inputs now have independent Sets/Standard/Compact modes, a stretchable source dropdown, screen-bounded full-name dropdown width, full-path tooltips and a draggable pipe/surface divider. All eleven result columns explicitly allow manual resizing without automatic width replacement. Mode changes clear the corresponding loaded source and invalidate earlier results. The source modes reuse Clash Matrix discovery: saved sets, model roots, immediate model-root children respectively; this is not an embedded native Selection Tree and does not mirror native Compact detail preferences. Load current selection supports selections from the native tree.

Full package build passed API 21/22/23 builds with zero warnings/errors, tolerance/48 policy checks, 692 detached Smart Review checks, all registration checks, scoped release manifests, ribbon/icon validation and payload hashes. UI controls fit populated offscreen forms at 1100/1360/1600 widths; minimum-width preview visually inspected. Native interaction with these updated selectors has not been tested. Manage 2024 remains open (two Roamer processes); installation is deferred, installed version remains 1.0.39.0.

## Approved ribbon graphics — 1.0.41 (24 November 2026)

Applied the user's approved CivilPro-inspired engineering icon designs to all 17 NavPro tools plus a separate Smart Review dropdown header. Versioned path-only SVG masters under Assets/IconSources are rendered to transparent 16/32 px PNGs by the production generator in Windows PowerShell STA/WPF. All 36 generated PNG hashes exactly match the approved preview samples. Command IDs, panel membership and tool behavior are preserved. This release also includes the 1.0.40 Vertical Clearance source UI improvements.

The full package run passed API 21/22/23 builds with zero warnings/errors, tolerance/48 policy checks, 692 detached Smart Review checks, all three registration checks, scoped manifests, 18 icon pairs in every root/localized release and payload hashes. Approved preview sheet visually inspected on light/dark samples; native Navisworks ribbon rendering is not established by these checks. Installation follows only while all Roamer processes are closed.

Installation 1.0.41: completed with all Roamer processes closed. All 234 installed payload hashes matched the package, and v21/v22/v23 report 1.0.41.0. Previous bundle retained at `%LOCALAPPDATA%/NavPro/Backups/NavPro-20261124-142137-419.bundle`. Native ribbon/selector interaction after restart remains pending.

## Ribbon capture for GitHub — 24 November 2026

Observed the installed 1.0.41 NavPro ribbon in Manage 2024 and opened its six-entry Smart Review menu. Approved tool/menu icons and labels were visible. Saved cropped ribbon/menu images under Docs/Showcase, excluding model geometry and title-bar project names. This verifies visible ribbon registration only; no tool/model mutation or geometry accuracy check was performed during capture.
