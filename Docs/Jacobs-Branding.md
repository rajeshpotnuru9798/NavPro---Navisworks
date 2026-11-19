# Jacobs desktop theme — 2026-10-21

Applies to Mytools and NavPro owned dialogs, not Autodesk/Windows dialogs, ribbon chrome, exported project data or logos. This replaces the earlier EPD Gas adaptation. It is a desktop interpretation, not corporate certification.

## Source and scope

- [User-supplied primary-theme module](https://comms.foleon.com/document-template-system-training-v4-phase-2/jacobs-color-themes-v4-copy-1/jacobs-primary-color-themes): consistent main headline/table/keyline colour, with white, black, N1 and N4. Extended palettes/tints are for supporting graphics.
- [Linked styles module](https://comms.foleon.com/document-template-system-training-v4-phase-2/jacobs-custom-tables-v4-copy-1-copy-1/styles-panel-and-other-tools): Jacobs Chronos default; Arial and Arial Narrow alternatives.
- Exact RGB values and contrast context: Jacobs-authored Brand Guide v8.1, March 2020, pp.48–50,95, available from a [third-party mirror](https://diguru-uk.com/diguruwp/wp-content/uploads/2025/07/Jacobs-Brand-Guide-v8-1-min.pdf). This is older supporting evidence, not proof of current corporate approval. The live training page does not publish numeric swatches. Recheck against current internal Brand Assets when available.

## Implementation tokens

| Role | Value |
| --- | --- |
| Header, primary action, selected tab, table heading | Blue P1 `#231EDC`, white text |
| Body text | Neutral N1 `#333333` |
| Neutral surfaces/read-only fields | White / N4 `#E6E6E6` |
| Canvas | `#F9F9F9`, UI-derived N4 25% tint |
| Selection | `#C8C7F6`, UI-derived Blue P1 25% tint, N1 text |
| Decorative/progress accent | Blue P3 `#5AE6FF`; not small text on white |
| Supporting warning accent | Yellow P1 `#FFA014`; pair with dark text and a written warning |

Use locally installed Jacobs Chronos, falling back to Arial, then the system UI font. Keep existing point sizes and bold hierarchy; do not force Arial Narrow into dense forms. No proprietary font is copied, installed or bundled. Chronos was detected on this workstation, but recipients can use fallback fonts.

Preserve keyboard focus, Accept/Cancel, read-only state, disabled actions, progress/ETA and semantic status labels. Never communicate safety by colour alone. Keep white space and primary-action hierarchy; do not imitate document-cover layouts inside forms. Check contrast (normal text at least 4.5:1), minimum-size clipping, long names and font fallback. Built-in OS progress bars remain native. Do not create/recolour a Jacobs logo or add claims of brand approval.

Both projects keep independent shared theme classes with matching tokens. New dialogs must use their project's theme. Offscreen fixtures check visual controls only, not native Civil 3D/Navisworks integration. Installation is a separate step.

## Clash Dashboard theme application

The NavPro HTML Clash Dashboard uses the user's selected dark slate analytics style with the established magenta brand accent above; desktop dialogs retain their own theme. Keep dashboard colors centralized in the writer's CSS custom properties and consume the same status tokens in historical columns, completion visuals and result badges. Pair status hues with readable dark-surface tints and light text; retain visible status words so color is not the only cue. Use shared foreground, background, surface, line, plot-grid and focus tokens for dashboard cards, slicers, tables and keyboard focus. Avoid per-chart palettes that make a status change meaning between visuals.

This adapts Microsoft's Power BI dashboard-theme model: one theme coordinates foreground/background, chart data colors, tile surfaces and visual formatting. Microsoft theme JSON isn't imported by this self-contained HTML dashboard. Sources: [Power BI dashboard themes](https://learn.microsoft.com/en-us/power-bi/create-reports/service-dashboard-themes) and [Power BI visualization overview](https://learn.microsoft.com/en-us/power-bi/visuals/power-bi-visualizations-overview). Validate contrast, print/narrow layouts, and cross-filter interactions in a browser; source-level token checks are not an accessibility certification.

## NavPro supplied-color override — 2026-11-05

The latest user-supplied NavPro swatch supersedes the earlier Jacobs Blue P1 desktop palette and warm dashboard palette **for NavPro only**. It is a solid deep magenta-purple sampled at approximately `#A500A4` (image samples ranged from `#A200A1` to `#A600A5`). This is a user-selected UI theme, not a Jacobs corporate color or approval claim.

| Role | NavPro value |
| --- | --- |
| Owned dialog headers, primary actions and dashboard brand | `#A500A4` |
| Hover/pressed action | `#7C007B` |
| Main canvas | `#FAF7FA` |
| Selection surface | `#F4DDF3` |
| Default text | `#332B37` |
| Dashboard header gradient | `#B741B7` → `#A500A4` → `#A200A1` |

Use white text on the saturated brand color and light text on dark dashboard surfaces. Keep Navisworks/Windows-owned chrome unchanged and preserve meaningful clash-status colors and written status labels. The dashboard's Reviewed series is mauve rather than brown so the former brown brand treatment does not conflict with the new theme. Check contrast, compact layouts and keyboard focus in a browser and Navisworks before claiming host-level visual verification.
