Current enhancement details and limits: [Review workflow enhancements 1.0.35](Review-Enhancements-1.0.35.md). This supersedes older workflow details below where noted.

# NavPro 1.0.35 — Smart Review and grouping

Open **NavPro > Smart Review > Smart Review ▼** to choose any of the six tools. The panel uses one large dropdown button with compact, labeled menu entries. See the [Vertical Clearance Checker guide](Vertical-Clearance-Checker.md) for the new vertical-only surface-to-pipe cover review.

## Ribbon placement

- **Clash Review:** Smart Clash Grouper, following the existing tools.
- **Smart Review:** Chainage-Based Clash Locator; AI Clash Description Assistant; Utility Separation Checker; Access and Maintenance Clearance Review; Coordination Change Log; Vertical Clearance Checker.

The original plug-in identity and existing command IDs remain unchanged. Each supported release uses its matching API binary. Windows are modeless with an optional Pin window toggle; Close disposes their document subscriptions. Checked tests remain selected when hidden by the test-name filter. All/None applies to the whole test list.

## Smart Clash Grouper

1. Check tests and click **Load results**.
2. Choose shared composite element A/B, model pair, a property on A/B, or a 3D spatial grid. Properties come from the returned composite item (or original item if unavailable); missing or ambiguous property labels are excluded.
3. Enter a group prefix; grid size is in document units. Grid cells are anchored at global XYZ origin. Spatial Grid keeps separate cells. Distance Clusters instead connect neighbours within the radius; diameter can exceed the radius. Level and Zone use actual metadata.
4. **Preview groups** shows names and counts. Existing groups, singletons, missing-key results and (by default) resolved results are left unchanged.
5. Check groups and **Apply checked groups**. The public `TestsMove` API moves original results into newly created `ClashResultGroup` containers. No result copies, deleted groups, automatic reruns or document saves occur. Identity, status, description and location checks run before committing; native metadata/Undo behavior still needs host acceptance.

Group plans are per test; results are never grouped across different tests. If a proposed name exists, change the prefix and preview again. Show first clash navigates to a representative result.

## Chainage-Based Clash Locator

Supply alignment geometry through **Import alignment**. Civil 3D can export LandXML; this version uses that supplied file and does not create a live Civil 3D connection.

- **LandXML:** explicit Northing Easting [Elevation] coordinates, known linear units, continuous lines and circular arcs. Circular arcs use Start/Center/End and cw/ccw rotation; lengths/radii/stations are cross-checked. Spirals, decreasing station progression, unsupported geometry, coordinate references and internal geometry discontinuities are rejected for the whole import instead of being ignored or approximated.
- **CSV:** `Alignment,Station,Easting,Northing`, in the selected CSV units. Rows define a continuous straight-segment alignment in supplied order. Station differences must equal plan lengths. Use LandXML for circular geometry. Save CSV template provides an example.

Select an alignment, verify the common XY origin/axes/coordinate system, set the maximum horizontal search distance in alignment units, check tests and click **Locate selected tests**. The model coordinates are converted from document units to alignment units. The explicit XY transform is applied after unit conversion; CRS conversion is not supplied.

Chainage is measured in horizontal plan geometry from the supplied starting station. Offset is positive to the right and negative to the left, facing increasing chainage. Clash-centre Z is reported separately; it is not a vertical alignment offset or an equipment underside elevation. Locations beyond search distance or with multiple equally near stations are flagged; ambiguous/out-of-range stations are withheld. Endpoint projections are explicitly labelled. CSV exports reflect the current displayed snapshot; Show clash navigates to the native result.

## AI Clash Description Assistant

Select tests → **Load clashes** → check rows → **Generate checked drafts** → inspect/edit proposed text → **Apply checked descriptions**. Generation batches allow 1–200 clashes. Application changes only the native Description field for checked rows. It does not change names, statuses, assignments, geometry or saved files. Existing descriptions appear beside drafts, and unselected clashes remain unchanged.

The simplified dialog uses **built-in offline drafting**. Mode, Endpoint, Model ID, API key and cloud-consent controls have been removed. No AI service, model download, credentials or network connection is used by this tool.

**Style** offers three supported formats: concise factual descriptions, detailed coordination descriptions (test, clash and native distance), and coordination review notes. Changing style retains existing drafts; manually edited drafts are protected from generation. Offline descriptions use available item names, native test type, clash coordinates and units; they do not infer engineering causes, required clearances or solutions.

**View available data** shows the available clash metadata for review. Drafts stay editable, and applying checked descriptions still requires confirmation and a current model snapshot. Cancelled/failed generation retains existing and completed drafts. Native clash distance is not automatically reinterpreted as a code-required clearance or penetration depth.

## Utility Separation Checker

1. Create or select saved utility Selection/Search Sets representing physical surfaces.
2. Choose A/B sets, enter a rule name and a positive **required 3D gap** with units. Confirm that requirements are project-approved; the tool supplies no universal standard values.
3. **Add rule** for each utility pair; inputs affect the next added rule. Remove selected rule to correct queued entries.
4. **Preview checks** validates current sets, evaluates nonempty source scopes, checks distinct A/B pairs and unique test names, and converts gaps to document units.
5. **Create previewed checks** creates native **Clearance** definitions with dynamic saved-set selection sources through the existing Clash Matrix adapter. This does not execute them.
6. **Run created checks** executes only the newly created tests by GUID, synchronously. There is no safe cancellation inside an individual native run. Review result counts in the table and actual results in Clash Detective.

This first version is a 3D surface-clearance workflow, including hard clashes. It does not implement horizontal-only, vertical-only, crossing-specific or centreline-based engineering rules. Use physical solids with required sleeves/insulation included. An empty source is blocked rather than reported clear. Zero current findings applies only to the checked model scope and engine behavior; it is not a compliance certification.

## Access and Maintenance Clearance Review

Uses the same queued-rule, preview, create and run workflow, with an explicitly modeled **envelope set** on A and a selected **surroundings set** on B. The user confirms that the envelopes represent approved access/maintenance requirements. Native **Hard** surface-intersection tests use zero tolerance.

The tool does not infer servicing direction or envelope dimensions from equipment bounding boxes. It does not generate door sweeps, calculate removal routes or certify access. Exclude the envelope's owning equipment from the surrounding scope when appropriate. Fully contained geometry may not intersect envelope surfaces: an absence of surface findings is not a containment/occupancy clearance result. Output labels retain this limitation.

## Coordination Change Log

Refresh to see explicitly recorded NavPro actions for the current document instance, including UTC time, Windows user, tool, scope and recorded before/after values. Filter the visible rows or **Export visible log CSV**. Entries are held in memory for this application session; export before closing Navisworks. No file is written automatically.

Recorded actions include successful grouping, description application, spatial test creation/runs, Clash Matrix creation, Tolerance Updater changes, Renamer, Status Updater, status import and applied Colorizer overrides. Colorizer records its target/new RGB and states that prior appearance was not captured. This is not a complete/reconstructable audit of every model action: earlier sessions, manual native edits, model refresh changes and failed/partially completed operations are not a complete transaction history. It does not provide undo or rollback.

## Source and verification boundaries

- Installed Manage 2024/API 21 metadata was inspected for result grouping/moving, descriptions, path IDs, result viewpoints and change events; builds also target supplied 2025/API 22 and 2026/API 23 references.
- [Autodesk SDK resources](https://aps.autodesk.com/developer/overview/navisworks-api)
- [Civil 3D LandXML coordinate/station handling](https://help.autodesk.com/cloudhelp/2023/ENU/Civil3D-UserGuide/files/GUID-4D10ABA5-5EA0-41A8-BB61-C3F446CE7C6B.htm)
- [Ollama chat endpoint](https://docs.ollama.com/api/chat)
- [Chat Completions schema](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create)

Detached tests cover planners/geometry, supplied-data validation, real review orchestration with explicit native doubles, HTTP requests with mocks, and offscreen WinForms layout/lifecycle. They do not prove Autodesk transaction/Undo semantics, real geometry detection, installed host rendering, provider authentication or live AI output. Representative project-model testing remains necessary.
