# NavPro project instructions

- This repository is the separate NavPro add-in. Do not modify the sibling Mytools project or its installed bundle while working here.
- Target the installed Navisworks Manage 2024 API (21.0), x64, .NET Framework 4.8. Check local API DLL/XML documentation before adding native calls. Never assume Civil 3D APIs apply to Navisworks.
- Preserve the exact UI tool spelling `SearchSetCreater`, stable plugin ID and shared identity constants in `Classes/NavProIdentity.cs`.
- Follow Frontend/Backend/Classes separation described in `Docs/DEVELOPMENT.md`; do not create duplicate implementations in parallel folders.
- Keep Autodesk API operations on the host UI thread. Preserve existing sets; do not add replacement/deletion behavior without explicit user direction. Do not claim transaction rollback, exact ETA or safe interruption of synchronous native calls unless documented.
- Run `Tools/Build-Package.ps1` after implementation changes. It builds the solution, runs policy/adapter tests, verifies registration metadata and produces the shareable bundle. Read scripts before changing their deployment behavior.
- Installation is separate from building. Use the backup-aware installer only for an authorized installation/update and with Navisworks closed. Never terminate the host to bypass this check.
- Report native host tests independently from builds, metadata checks and test doubles. Consult README.md and VERIFICATION.md before describing verification status.
