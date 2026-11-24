# Approved NavPro icon masters

The user approved these 18 vector designs on 24 November 2026 after reviewing `Docs/IconReview/NavPro-Icon-Review.png`. There is one design for each of the 17 tools and a separate Smart Review dropdown icon. The engineering symbols and blue/cyan/green/amber palette take visual cues from CivilPro, while retaining NavPro tool identities.

`Tools/Create-Icons.ps1` renders the path-only SVG masters into separate transparent 16/32-pixel PNGs using Windows PowerShell STA/WPF. The generated production PNGs were SHA256-compared with all 36 approved preview PNGs and matched exactly. Build packaging distributes only the runtime PNGs beside each release/localized ribbon; SVG masters are development sources. Native host icon rendering is checked separately from file dimensions and package registration.

Do not overwrite this set with the earlier tile generator. Keep stable command IDs and the `SearchSetCreater` spelling. The existing dashboard logo is a separate asset.
