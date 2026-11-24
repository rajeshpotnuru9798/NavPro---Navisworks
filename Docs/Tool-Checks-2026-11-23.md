# NavPro tool-check report — 23 November 2026

**16 of 16 tools have received live checks in Navisworks Manage 2024/API 21. This is not a full acceptance pass.** Several checks cover loading, preview and validation only. Version 1.0.35 was used for the initial native run; 1.0.36 contains fixes found during that run. Version 1.0.36.0 is installed; large-import matching, large revision capture/comparison and dashboard launcher/sample-image regressions passed. Full all-status image coverage and other omissions below remain outstanding.

## Model and preservation

- Tests use copies of the supplied 47,007,856-byte federation under [native test artifacts](../Tests/Artifacts/Native-1.0.35/).
- Baseline: **46 tests, 34,662 leaf results, 53 saved sets (44 search sets)**; statuses: New 23, Active 7,895, Reviewed 0, Approved 34, Resolved 26,710.
- Original SHA256 rechecked unchanged: `D676AB5E0096E4558D8B97F6B4D750B0B53D37E6ADFA56523551376AA6FBA1CC`.
- The supplied original was not edited or saved. Mutations occurred only in the disposable review copy. No approved alignment/control points, separation rules or maintenance-envelope designations were supplied.

## Live checks by tool

| Tool | Observed native result | Coverage still outstanding |
|---|---|---|
| Contact Info | **Passed checked workflow:** displayed 1.0.35/API 21, supported releases and diagnostics; Copy diagnostics displayed success. | New-version display after installation; exhaustive DPI/keyboard coverage. |
| SetCreator | **Passed checked workflow:** scanned 30,527 hierarchy items; found 78 categories/4,517 properties. Chosen property yielded one typed value and one directly matching object. Explicit match/overlap action selected it; warned overlap comparison covers only first 50 saved sets. Created one new set; existing sets preserved. | Preset save/reload, filename workflow and overlap/empty-set edge cases in host. |
| Clash Matrix | **Passed checked workflow:** corrected split layout visible; loaded baseline sets/counts. Created and verified one Hard test `NavProQA_RAD vs SAD`; ran only that new test. | Broader pair/type combinations and independent clash-geometry validation. |
| Renamer | **Partial:** numbering/pattern preview showed 46 changed, zero invalid names; before/after highlight visible. | Native Apply and saved/custom presets not exercised. |
| Tolerance Updater | **Passed checked workflow:** scoped to QA test; previewed/applied 0 to 0.984251968503937 Feet, preserving Hard/composite setting. Reran only the checked QA test. | Multi-test mixed-type native coverage; detached adapter tests cover these combinations. |
| Status Updater | **Partial:** loaded 47 tests after QA creation; preview accurately showed baseline statuses and 34,628 changes proposed for Approved, 34 already current. | No approval/status writes or metadata roundtrip performed. |
| Import Clash Status | **Issue found:** loaded all 34,662 XML rows. Preview eventually completed: 28,623 already current, 6,039 incomplete identities, zero changes/ambiguous/not found. No import performed. Full-list matching caused prolonged UI blocking. | Installed indexed matching regression passed with identical counts within the approximately 3.34-second observation. Status-write roundtrip still outstanding. |
| Refresh Models | **Partial:** confirmed refresh; published NWD returned no changed native references. Summary showed file observations, published-source caveat and each test's last run/source-revision review guidance. | Changed/missing external references require a representative NWF and its source files. |
| Colorizer | **Partial:** one model target loaded; random-colour legend preview opened with RGB/hex colour. Empty saved-palette picker opened. Preview cancelled without applying overrides. | Native palette save/reuse/import and model-colour mutation not tested. Empty-library guidance improved in 1.0.36. |
| Smart Clash Grouper | **Passed core workflow:** loaded 136 results; excluding resolved yielded none. Including resolved with 5-Foot connected radius proposed 27 groups/132 results, four unchanged. Explanations showed actual neighbour distances. Applied one 8-result group; service verified membership/fields and wrote journal entry. | Completion text was overwritten by the model-change watcher; fixed in source. Level/zone and saved review-data/Undo behavior need broader native checks. |
| Chainage-Based Clash Locator | **Partial:** loaded all 46 tests; transform controls visible; calculation without alignment correctly blocked. | Supplied project alignment, control-point accuracy, plan visualization and station equations not exercised in host. Spirals/decreasing station progression remain unsupported. |
| AI Clash Description Assistant | **Partial:** loaded 136 results; project-template generation flagged missing data and Apply correctly blocked. Built-in factual style generated 136 drafts. | No descriptions applied. Native manual-edit protection inconclusive: Ctrl+A during attempted grid editing opened Navisworks Append; cancelled without append. Automated dialog fixture verifies protection separately. |
| Utility Separation Checker | **Partial:** loaded 53 sets; source/revision/import UI visible. Add rule without source acknowledgement correctly blocked. | No approved rule input supplied; native rule import/create/run and known geometry not tested. Horizontal/vertical/crossing geometry not implemented. |
| Access and Maintenance Clearance Review | **Partial:** loaded 53 sets; envelope/surroundings controls visible; unconfirmed envelope source correctly blocked. | Approved envelopes/owner exclusions required for meaningful run. Surface-intersection/containment limitations remain. |
| Coordination Change Log | **Persistence passed; revision issue found:** native action and stopped-operation entries persisted to disk; Refresh Models entry was visible after restarting the host. Federation revision capture failed at the previous 8 MB JSON limit; prior journal retained. | Installed 1.0.36 capture/compare passed: two 34,662-record revisions, 21.7 MB persisted; no differences as expected. Changed/add/remove cases remain detached tests. Not Undo. |
| Clash Dashboard | **Partial/failing full image coverage:** all 46 definitions/34,662 rows exported to HTML/XML. 9,001 images, 25,661 native state errors; 8,969 images generated without saved viewpoints, 7,333 distinct image payloads. Browser showed correct totals; first page has 100 image elements and sampled image decoded 420×240. | Initial failure sequence is contiguous after row 9,001; cause unproven. Installed launcher regression passed at normal/maximized sizes; controlled New-only export produced 23/23 images. Full all-status coverage remains uncertified. |

## Stability observations

- The full status-import preview remained busy for a prolonged period but eventually completed; the host was not terminated. Source inspection identified repeated full-list matching and it was replaced with an index preserving match/ambiguity criteria.
- The first export host exited unexpectedly after export/refresh, during a subsequent ribbon check. Cause is unconfirmed; do not label this a proven NavPro exception or a successful stability check.
- Concurrent host activity occurred during the first image export. A graphics-context effect is a hypothesis, not an established explanation for missing images.

## Build and detached verification

- 1.0.35: 546 Smart Review checks passed using API doubles, geometry fixtures, HTTP mocks and offscreen forms; tolerance checks and 48 policy/criteria checks passed. Matching API 21/22/23 release builds and package/registration checks passed.
- Initial 1.0.36 fixes passed the same 546 review checks and all three release builds/metadata/package verification. Final rebuild also passed **549** review checks, including a history snapshot over 8 MB and UTF-8 oversize rejection preserving prior data. All three release builds completed with zero warnings/errors; registration/package verification passed.
- These checks do not prove native geometry, complete manual-edit behavior, approval metadata, Undo or Navisworks 2025/2026 runtime behavior.

## Fixes prepared in 1.0.36

- Index status-import matching rather than scanning every native result for every report row; retain incomplete/ambiguous identity exclusions.
- Resize dashboard launcher and bound status text so actions remain accessible.
- Keep grouper completion feedback after its own mutation while continuing to watch external changes.
- Show actionable guidance for an empty saved palette library.
- Correct transform-validation wording.
- Allow bounded history files up to 128 MB (ordinary presets remain 8 MB); reject oversize UTF-8 output before replacing readable data. Revision capture omits unused item-property traversal.

## Installation / regression

**1.0.36.0 installed successfully** with all Navisworks processes closed. All 208 installed payloads matched package SHA256 hashes. Previous bundle backed up outside Autodesk discovery at `%LOCALAPPDATA%/NavPro/Backups/NavPro-20261123-211159-811.bundle`. Installed native regression is in progress. The QA review copy was saved (47,021,018 bytes); original source federation remains unchanged.



### Installed 1.0.36 regression: status-import preview

Passed on the unchanged baseline copy: all 34,662 XML rows loaded and preview completed within the approximately 3.34-second click/capture observation, returning exactly 28,623 already-current and 6,039 incomplete identities, zero ready/ambiguous/not-found. This verifies native matching performance and unchanged classification, not a native status-write roundtrip.

### Installed 1.0.36 regression: coordination revisions

Passed: native capture of two unchanged federation revisions, each with **34,662 result GUID records**, succeeded. The persisted JSON is **21,704,913 bytes** and comparison displayed no captured result-field differences. This verifies large capture/storage/read/unchanged comparison; changed/add/remove cases remain detached-fixture coverage. Keyboard Ctrl+A again opened native Append during text entry; it was cancelled without appending. Context-menu editing worked. Keyboard shortcut behavior remains an observed usability issue, not a claimed passed check.

### Installed 1.0.36 regression: dashboard controls and images

Passed launcher layout at normal restored size (approximately 744×354 captured pixels) and maximized size: Export and Close remain visible. Controlled single-host export of New results retained all 46 test definitions and produced **23 rows / 23 native images / zero unavailable notes**. This includes 17 New results whose images failed in the initial full export, so those initial failures are not evidence of permanently missing result geometry. A transient native/context problem is suggested, but its cause remains unconfirmed. Complete all-status image coverage is still not certified; the initial 34,662-row export remains partial.

## Final review position

All **16 tools received native checks**, with the scope and omissions listed above. Version **1.0.36.0** is installed and its large-import matching, large revision capture/comparison, dashboard launcher and controlled image-export regressions passed. Several tools remain partial because only preview/validation was exercised or approved project input is absent. The report does **not** certify the application error-free, every native mutation, all geometry checks, all clash thumbnails, Undo, or 2025/2026 host execution.

