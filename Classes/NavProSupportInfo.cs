using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NavPro.Classes
{
    public static class NavProSupportInfo
    {
        public static string Version => typeof(NavProSupportInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";
        public const string SupportedReleases = "Navisworks Manage 2024, 2025 and 2026 (Windows x64)";
        public const string ReleaseNotes = "1.0.41 — Approved NavPro ribbon icons\r\nCivilPro-inspired engineering graphics for all 17 tools and a distinct Smart Review menu icon, with separate 16/32-pixel assets. Includes the Vertical Clearance source layout improvements from 1.0.40.\r\n\r\n" + "1.0.40 — Vertical Clearance source selection\r\nStretchable pipe/surface selectors, Sets/Standard/Compact source views, full-name dropdowns and resizable result columns.\r\n\r\n" + "1.0.39 — Navisworks Manage 2026 compatibility\r\nVerified supplied API 23 references, isolated release-specific bundle registration and added matching COM bridge validation. Native Manage 2026 acceptance remains pending.\r\n\r\n" + "1.0.38 — Vertical Clearance Checker\r\nSmart Review: vertical surface-to-pipe cover on model triangles; minimum cover, coverage flags, section schematic and CSV export. See the workflow guide for geometry and release limits.\r\n\r\n" + "1.0.37 — Clash image export resource fix\r\nReclaim deferred native image resources between batches; retry transient image failures once; display live image counts and retain per-row failure details.\r\n\r\n" + "1.0.36 — Native-check fixes\r\nIndexed clash-status matching for large reports; dashboard window clipping fix; clearer empty-palette guidance; retain grouping completion feedback. See verification record for native results and outstanding image coverage.\r\n\r\n" + "1.0.35 — Review workflow enhancements\r\nProject description templates and protected drafts; distance/level/zone grouping; saved palette previews; set presets/match previews; alignment transforms, station formatting/equations; approved rule tables; persistent coordination history/revision comparisons; all-result dashboard image requests.\r\n\r\n" + "1.0.34\r\n• Clash Matrix preview layout fix (native acceptance pending): scrollable profile with independent model, selection and preview areas.\r\n\r\n1.0.33\r\n• Renamer: naming presets, editable token patterns, automatic numbering and highlighted text differences.\r\n• Contact Info: installed version, supported releases, release notes and explicit diagnostics copy.\r\n• Tool windows: restore size/state, divider proportions, filter text and grid column widths between sessions.\r\n\r\n1.0.32\r\n• Resizable model lists, A/B selectors and review panels.\r\n\r\n1.0.31\r\n• Simplified offline description drafting with three styles.\r\n\r\n1.0.30\r\n• Fixed Smart Review dropdown registration and visibility.\r\n\r\nBuild/package checks and native Navisworks acceptance are separate; consult the supplied verification record for test coverage.";
        public static string Diagnostics()
        {
            var assembly = typeof(NavProSupportInfo).Assembly;
            var target = assembly.GetReferencedAssemblies().FirstOrDefault(a => a.Name == "Autodesk.Navisworks.Api");
            var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName()).FirstOrDefault(a => a.Name == "Autodesk.Navisworks.Api");
            return new StringBuilder().AppendLine("NavPro diagnostics").AppendLine("Installed plug-in version: " + Version)
                .AppendLine("Supported: " + SupportedReleases).AppendLine("Target API: " + (target?.Version.ToString() ?? "Unavailable"))
                .AppendLine("Loaded Navisworks API: " + (loaded?.Version.ToString() ?? "Not loaded"))
                .AppendLine("64-bit process: " + Environment.Is64BitProcess).AppendLine("CLR: " + Environment.Version)
                .AppendLine("Windows: " + Environment.OSVersion.VersionString)
                .AppendLine("Contains no model names, file paths, account names or credentials.").ToString();
        }
    }
}

