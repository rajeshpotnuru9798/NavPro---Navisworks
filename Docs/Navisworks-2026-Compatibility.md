# Navisworks Manage 2026 compatibility — NavPro 1.0.39

## Reference audit

On 24 November 2026, all 607 files in the supplied `Manage 2026 DLLs` folder were inventoried and SHA256 hashed: 198 managed assemblies, 408 native binaries and one other file. The machine-readable inventory is retained in `Tests/Artifacts/navisworks-2026-sdk-inventory.json`. Binary inventory and metadata inspection do not constitute execution or semantic review of every Autodesk implementation.

The public core, Clash, ComApi and Interop.ComApi assemblies identify as version 23.0.0.0. NavPro references the public APIs needed by its tools, with host-provided references excluded from the distributable. The other Autodesk/internal/native files are not copied into the plug-in bundle.

## Compatibility changes

- Keep distinct x64 .NET Framework 4.8 binaries for Manage 2024/API 21, 2025/API 22 and 2026/API 23.
- Give each release a separate bundle `Components` scope containing its matching runtime requirement and module. Manage 2026 selects `Contents/v23/NavPro.dll` using `Nw23`.
- Reject mismatched core, Clash or COM bridge reference versions during builds/metadata checks. Require the supplied 2026 COM geometry bridge so Vertical Clearance Checker is included.
- Preserve API 23-specific clash assignee/approval handling, stable command identities and the Smart Review dropdown.

The 2025 build retains its documented geometry limitation because its supplied reference folder lacks the matching public COM bridge. The 2024 and 2026 builds include geometry extraction.

## Verification boundary

Consult [VERIFICATION](../VERIFICATION.md) for the completed build, fixture, registration, package and installation results. No Manage 2026 host is installed on this computer; ribbon loading, model interaction, native geometry extraction, image export and mutation workflows in that host remain unverified. Earlier scoped Manage 2024 tests cannot certify Manage 2026.

Before production use in 2026, open a copy of a federation and check ribbon discovery, each tool's preview/read workflow, controlled write workflows, dashboard image coverage, and vertical cover against independently known geometry. Existing tool-specific limitations remain applicable.

## Sources

- [Autodesk Navisworks publisher guidance](https://aps.autodesk.com/app-store/publisher-center/navisworks): supported runtime and release-specific API binaries.
- [Autodesk Components element reference](https://help.autodesk.com/cloudhelp/2023/ENU/AutoCAD-Customization/files/GUID-3C25E517-8660-4BB7-9447-2310462EF06F.htm): component-group runtime scoping in the Autodesk bundle format.
