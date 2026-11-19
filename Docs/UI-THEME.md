# NavPro application theme

NavPro dialogs follow the shared EPD Gas Application Theme referenced by the AEC Development Skills guide. The supplied presentation has a dark teal-blue brand background (`#245473`); its theme palette also supplies navy (`#0E2841`), blue (`#156082`), cyan (`#0F9ED5`) and orange (`#E97132`).

For readable application dialogs, NavPro uses a cool near-white canvas (`#F4F7F9`), white control surfaces, navy text and blue primary actions. Cyan is reserved for progress/focus emphasis; orange is a secondary emphasis color. `Segoe UI` is used because the presentation's New Murabba font was not installed on the reviewed workstation. Keep Autodesk and operating-system-owned dialogs native.

Use `Frontend/Ui/DialogTheme.cs` for new NavPro dialogs and update the shared AEC skill reference if future design decisions intentionally change this standard. Theme code changes presentation only; it must not alter command registration, Navisworks API call threading, selection semantics or model operations.
