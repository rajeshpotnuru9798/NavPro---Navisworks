# NavPro change log

This summarizes the recorded development history. Detailed technical checks, installation receipts and historical limitations remain in [VERIFICATION.md](VERIFICATION.md). Build/fixture success does not certify native host operations.

## 1.0.41 — Approved ribbon graphics (24 November 2026)

- Applied approved CivilPro-inspired engineering icons to all 17 tools plus the Smart Review menu.
- Added versioned vector masters and reproducible, transparent 16/32-pixel assets; all 36 images match the approved samples.
- Updated menu metadata, ribbon image paths and package checks for all supported releases.
- Includes the Vertical Clearance source UI improvements from 1.0.40.
- All API 21/22/23 builds passed with zero warnings/errors; tolerance/48 policy checks, 692 detached Smart Review checks and package validation passed. Installation verified 234 files and retained a backup.
- Subsequent Manage 2024 observation confirmed the ribbon and six-item Smart Review menu render with the approved icons. Screenshots are in [the showcase](Docs/Showcase/README.md); tool execution is a separate check.

## 1.0.40 — Vertical Clearance source selection (24 November 2026)

- Stretchable pipe/surface selectors, draggable divider, full-name dropdowns and path tooltips.
- Independent Sets, Standard and Compact source modes using existing saved-set/model discovery. These are source lists rather than the native configurable Selection Tree; native tree selections can be loaded explicitly.
- All eleven result columns resize manually; source-mode changes invalidate earlier results.
- Minimum/default/expanded populated UI checks passed. Included in the installed 1.0.41 update.

## 1.0.39 — Manage 2026 compatibility (24 November 2026)

- Audited all 607 supplied SDK-folder files; public core, Clash and COM bridge assemblies identify as API 23.
- Isolated API 21/22/23 runtime requirements and modules into separate bundle component groups.
- Added matching COM bridge guards and a [2026 compatibility audit](Docs/Navisworks-2026-Compatibility.md).
- Kept Autodesk DLLs reference-only. Manage 2026 native testing remains pending because that host is not installed locally.

## 1.0.38 — Vertical Clearance Checker (24 November 2026)

- Added vertical surface-to-pipe-crown cover review under Smart Review.
- World +Z gaps at matching XY, transformed mesh extraction, full-footprint coverage and explicit incomplete/ambiguous geometry states.
- Project minimum cover, units, decision tolerance, schematic section, model selection/zoom and CSV export.
- Matching geometry bridge available for 2024/2026; the supplied 2025 SDK lacks its bridge and the tool is disabled there. Native geometry accuracy remains pending acceptance against known controls.
- See [workflow and limitations](Docs/Vertical-Clearance-Checker.md).

## 1.0.37 — Dashboard image resource correction (24 November 2026)

- Reclaim deferred native image wrappers between bounded batches and at capture scope exit.
- One cleanup/retry for transient image-state errors; preserve explicit per-row failures and image counts.
- Detached resource simulations passed. Complete native image coverage across the 34,662-result federation remains uncertified.

## 1.0.36 — Native-check fixes (23 November 2026)

- Indexed clash-status matching for large reports, bounded UTF-8 revision history and persistent partial outcomes.
- Fixed dashboard action clipping, empty-palette guidance, transform wording and grouping completion feedback.
- Scoped Manage 2024 follow-up covered large status matching, two persisted revisions and a 23/23-image export; full tool/mutation coverage remains partial.

## 1.0.35 — Review workflow enhancements (23 November 2026)

- Refresh Models: reference availability/file information, refresh summaries and potentially outdated-result guidance.
- Description Assistant: editable project templates, missing-token flags and protected manual drafts.
- Colorizer: saved palettes, legend and preview; restore behavior remains limited to supported recorded overrides.
- Smart Clash Grouper: distance clusters, level/zone grouping and membership explanations.
- SetCreator: reusable property/naming presets, matching-object previews and empty/overlap reporting.
- Renamer: naming presets, automatic numbering and highlighted before/after differences.
- Chainage Locator: alignment preview, coordinate transform setup, station formatting/equations with geometry validation; unsupported spirals remain rejected.
- Utility Separation: approved Excel/CSV rule tables with source/revision. Directional/crossing-specific geometry remains outside certified scope.
- Coordination Change Log: persistent history, partial failures and revision comparisons, distinct from Undo.
- Dashboard: all-result image requests and per-row image outcomes.
- See [the enhancement guide](Docs/Review-Enhancements-1.0.35.md) and [scoped native checks](Docs/Tool-Checks-2026-11-23.md).

## 1.0.34 — Clash Matrix layout (23 November 2026)

- Scrollable profile settings and independent model, selection and preview space following native acceptance observations.

## 1.0.33 — Renamer, support and preferences (23 November 2026)

- Naming-pattern presets, numbering and text differences in Renamer.
- Contact Info version, supported releases, release notes and explicit Copy diagnostics.
- Retain window sizes, divider positions, filters and column widths; normalize offscreen/corrupt preferences.

## 1.0.32 — Resizable tool regions (23 November 2026)

- Shared draggable model/list/preview dividers, enlarged windows and horizontal model-list scrolling.

## 1.0.31 — Description controls (23 November 2026)

- Simplified Description Assistant to offline drafting with three styles; removed provider, endpoint and credential setup from its UI.

## 1.0.30 — Smart Review visibility (23 November 2026)

- Registered and enabled the dropdown header command; corrected missing Smart Review panel discovery.

## 1.0.29 — Smart Review dropdown (23 November 2026)

- Consolidated Smart Review tools into a compact ribbon dropdown.

## 1.0.28 — Smart Review tools (23 November 2026)

- Added Smart Clash Grouper, Chainage-Based Clash Locator, AI Clash Description Assistant, Utility Separation Checker, Access and Maintenance Clearance Review and Coordination Change Log.
- Separate modeless review/session guards, preview-first edits and distinct coordination history.

## Earlier recorded releases

| Version | Recorded changes |
|---|---|
| 1.0.27 | Colorizer workflow. |
| 1.0.26 | SetCreator count failure correction; Clash Matrix model and unit controls. |
| 1.0.25 | Composite Object Clashing control in Tolerance Updater. |
| 1.0.24 | Refresh Models command. |
| 1.0.23 | Dashboard header timestamp and units removed. |
| 1.0.22 | Dashboard headings, register tolerance and report notes. |
| 1.0.21 | Dashboard heading wording. |
| 1.0.20 | Week-over-Week value alignment. |
| 1.0.19 | Empty-data dashboard rendering correction; 2026 ribbon report investigated. |
| 1.0.18 | Weekly analytics dashboard. |
| 1.0.17 | Dashboard logo option 1. |
| 1.0.16 | Status Updater filtering and Clash Matrix model-list layout. |
| 1.0.15 | Dashboard Priority All/None controls. |
| 1.0.14 | Tolerance Updater test filtering. |
| 1.0.13 | Dashboard dark analytics styling. |
| 1.0.12 | Import Clash Status and standard Navisworks XML compatibility work. |
| 1.0.6 | User-requested magenta application theme and installation. |
| 1.0.4 | Loaded Reports shortcuts, dashboard identity and contact link. |
| 1.0.1 | Shared branded product dialogs and theme verification. |

Earlier features and unversioned iterations include SetCreator, Clash Matrix, Status Updater, Renamer, Tolerance Updater, report/dashboard exports, priority/discipline parsing, cascading filters, completion/history charts and release-specific builds. Their dated technical records are preserved in [VERIFICATION.md](VERIFICATION.md); version numbers are not inferred where the record does not establish them.

## Current verification limits

Manage 2024 received scoped native checks; Manage 2025/2026 runtime acceptance remains pending. Vertical cover accuracy, full-register image coverage, the observed native Ctrl+A conflict and tool-specific partial workflows require further native verification. SDK files, build outputs, model files and native test artifacts are not distributed as repository source.
