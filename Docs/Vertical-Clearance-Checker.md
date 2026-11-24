# Vertical Clearance Checker

Open **NavPro → Smart Review ▼ → Vertical Clearance Checker** in Manage 2024.

1. Select complete pipe elements in the model, then **Load current selection** in the pipe section. Alternatively choose **Sets**, **Standard** or **Compact**, pick an entry and **Load source**. Sets lists saved Selection/Search Sets; Standard lists loaded model files; Compact lists immediate model-root child nodes, reusing the Clash Matrix adapter. These are source lists rather than an embedded native Selection Tree; for deeper hierarchy or native Compact detail settings, select items in Navisworks and use **Load current selection**.
2. Select the ground/top surface and load it in the surface section. Use a single-valued triangulated surface, excluding sides/undersides of a road solid, duplicate faces and stacked layers.
3. Enter the project minimum cover and its units. World **+Z** is vertical. Confirm the federated models have the correct units and orientation.
4. **Check vertical cover**. Review minimum cover, required cover, shortfall, surface/pipe-top elevations, critical X/Y and coverage. Select a result to see a schematic vertical section, or select/zoom to its pipe.
5. **Export results CSV** exports all review rows in metres, including source names, method and incomplete-coverage notes. Filtering changes only the visible list.

Drag the divider between pipe and surface inputs to give either selector more room. Selectors stretch with their panels, dropdowns expand to fit names within the screen, and hovering shows the full selected path. All result columns support dragging the header boundary and double-clicking it to fit content; horizontal scrolling retains access to other columns. Window/divider/column preferences use the shared session persistence. **Refresh sources** reloads the current source modes and requires loading review inputs again. Changing a mode clears that side's loaded geometry and invalidates earlier results.

Navisworks' native tree semantics are described in [Autodesk's Selection Tree guide](https://help.autodesk.com/cloudhelp/2023/ENU/Navisworks/files/GUID-D8850A84-2FCA-4AC8-8588-8CCBA981C9DD.htm). Its configurable Compact tree is richer than NavPro's immediate-child source list.

## Method

This is a separate, read-only geometry review, not a renamed native Clearance clash test. It compares Z elevations at matching X/Y positions; horizontal or nearest-3D separation is never used. The mesh triangles are extracted through the public Navisworks COM bridge on the host UI thread. Each fragment's local-to-world matrix is applied, then world units are converted to metres. Extracted bounds are checked against the .NET model bounds; a mismatch is unverified rather than corrected with a guessed scale.

The engine clips projected pipe/surface triangle pairs in XY, calculates the linear vertical gap at intersection-polygon vertices, and retains the minimum over the complete footprint. This finds interior valleys and edge crossings without interval sampling. Since all pipe faces are considered, the minimum comes from the upper pipe envelope. Closed-mesh checks prevent an isolated lower face or incomplete pipe shell from being treated as the outer pipe top.

## Result meanings and limits

- **Meets minimum (mesh):** complete footprint covered and minimum gap above the requirement plus decision tolerance.
- **Insufficient cover:** minimum below the requirement outside the decision-tolerance band.
- **Borderline:** within the decision-tolerance band, including equality; review against design geometry.
- **Exposed / intersects surface:** negative vertical cover.
- **Partial coverage / Unverified:** gaps, no surface at matching XY, open/non-manifold/flat pipe mesh, unsupported primitives, invalid transforms or extraction/complexity limits. These are never passes.

Results describe the model tessellation, not reconstructed analytic pipe geometry or a regulatory certification. The pipe mesh edge validation welds vertices at a one-micron quantization. Surface faces must form a non-overlapping 2.5D TIN. No surface elevation is extrapolated outside its footprint. Source/model/units changes invalidate the review. Existing native clash tests, sets, colours and model files are not modified or saved.

Coverage percentage measures projected pipe-facet area, including repeated projections of closed-mesh faces. Completeness checks use numerical area tolerances of 1e-12 square metres or 1e-8 of the accumulated projected area. The extraction bounds comparison allows 2 mm or one-millionth of the item extent; it validates coordinate consistency and is not a guarantee of the original model's accuracy.

The supplied SDK references enable native extraction in the 2024 and 2026 builds. The supplied 2025 reference folder lacks the matching COM bridge: the 2025 tool displays an availability message and disables checking, rather than loading a different release's API. Actual geometry acceptance requires a native host and known project controls; detached numerical/UI tests are separate.

## API references

- [Autodesk: geometry primitives through COM](https://blog.autodesk.io/get-primitive-from-solid-of-navisworks-by-native-com/).
- [Autodesk Support: fragment local-to-world matrix](https://forums.autodesk.com/t5/navisworks-api-forum/geometry-in-wrong-location-when-accessing-in-api/td-p/5896205).
- [Autodesk: world, file and display units](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/After-changing-the-model-tree-in-the-NWD-files-all-issue-pins-shifted-to-one-model-s-origin-in-Navisworks.html).

The requested 2018 ApiDocs Clash namespace page was unavailable during research. Installed API 21 DLL/XML signatures were checked; a general native clearance tolerance is not used for this directional calculation.
