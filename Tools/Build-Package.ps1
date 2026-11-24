param(
    [string]$NavisworksInstallDir = 'C:\Program Files\Autodesk\Navisworks Manage 2024',
    [string]$Navisworks2025ApiDir = '',
    [string]$Navisworks2026ApiDir = '',
    [switch]$SkipTests,
    [switch]$CreateZip
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Navisworks2025ApiDir)) { $Navisworks2025ApiDir = Join-Path $projectRoot 'Manage 2025 DLLs' }
if ([string]::IsNullOrWhiteSpace($Navisworks2026ApiDir)) { $Navisworks2026ApiDir = Join-Path $projectRoot 'Manage 2026 DLLs' }
$Navisworks2025ApiDir = [IO.Path]::GetFullPath($Navisworks2025ApiDir)
$Navisworks2026ApiDir = [IO.Path]::GetFullPath($Navisworks2026ApiDir)
$builds = @(
    [pscustomobject]@{ Year = 2024; ApiMajor = 21; ApiDir = [IO.Path]::GetFullPath($NavisworksInstallDir); Configuration = 'Release'; Folder = 'v21'; Series = 'Nw21'; Assembly = (Join-Path $projectRoot 'bin/Release/net48/NavPro.dll') },
    [pscustomobject]@{ Year = 2025; ApiMajor = 22; ApiDir = $Navisworks2025ApiDir; Configuration = 'Release2025'; Folder = 'v22'; Series = 'Nw22'; Assembly = (Join-Path $projectRoot 'bin/Release2025/net48/NavPro.dll') },
    [pscustomobject]@{ Year = 2026; ApiMajor = 23; ApiDir = $Navisworks2026ApiDir; Configuration = 'Release2026'; Folder = 'v23'; Series = 'Nw23'; Assembly = (Join-Path $projectRoot 'bin/Release2026/net48/NavPro.dll') }
)
foreach ($build in $builds) {
    $bridge = Join-Path $build.ApiDir 'Autodesk.Navisworks.ComApi.dll'
    if (Test-Path -LiteralPath $bridge) {
        if ([Reflection.AssemblyName]::GetAssemblyName($bridge).Version.Major -ne $build.ApiMajor) { throw "COM geometry bridge does not match Navisworks $($build.Year)." }
        if (!(Test-Path -LiteralPath (Join-Path $build.ApiDir 'Autodesk.Navisworks.Interop.ComApi.dll'))) { throw "COM geometry interop reference missing for Navisworks $($build.Year)." }
    } else { Write-Output "Navisworks $($build.Year): matching COM geometry bridge absent; Vertical Clearance Checker is explicitly unavailable in this target." }
    foreach ($name in @('Autodesk.Navisworks.Api.dll','Autodesk.Navisworks.Clash.dll')) {
        $path = Join-Path $build.ApiDir $name
        if (!(Test-Path -LiteralPath $path)) { throw "Navisworks $($build.Year) API reference missing: $path" }
        $major = [Reflection.AssemblyName]::GetAssemblyName($path).Version.Major
        if ($major -ne $build.ApiMajor) { throw "Expected Navisworks $($build.Year) API major $($build.ApiMajor), found $major in $path" }
    }
}
& (Join-Path $PSScriptRoot 'Create-Icons.ps1')
Push-Location $projectRoot
try {
    & dotnet build NavPro.sln -c Release -t:Rebuild --nologo "-p:NavisworksInstallDir=$($builds[0].ApiDir)" -p:NavisworksApiAssemblyVersion=21.0.0.0
    if ($LASTEXITCODE -ne 0) { throw 'Navisworks 2024 build failed' }
    if (-not $SkipTests) {
        & './Tests/bin/Release/net48/NavPro.Tests.exe'
        if ($LASTEXITCODE -ne 0) { throw 'Policy/adapter tests failed' }
        & dotnet build './Tests/ReviewTools/NavPro.ReviewTools.Tests.csproj' -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Smart Review fixture build failed' }
        & './Tests/ReviewTools/bin/Release/net48/NavPro.ReviewTools.Tests.exe'
        if ($LASTEXITCODE -ne 0) { throw 'Smart Review geometry/adapter/UI tests failed' }
    } else { Write-Output 'Policy/adapter tests skipped by request.' }
    & dotnet build NavPro.csproj -c Release2025 -t:Rebuild --nologo "-p:NavisworksInstallDir=$($builds[1].ApiDir)" -p:NavisworksApiAssemblyVersion=22.0.0.0
    if ($LASTEXITCODE -ne 0) { throw 'Navisworks 2025 build failed' }
    & dotnet build NavPro.csproj -c Release2026 -t:Rebuild --nologo "-p:NavisworksInstallDir=$($builds[2].ApiDir)" -p:NavisworksApiAssemblyVersion=23.0.0.0
    if ($LASTEXITCODE -ne 0) { throw 'Navisworks 2026 build failed' }
    foreach ($build in $builds) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File './Tools/Verify-Registration.ps1' -NavisworksInstallDir $build.ApiDir -PluginAssemblyPath $build.Assembly
        if ($LASTEXITCODE -ne 0) { throw "Navisworks $($build.Year) registration metadata check failed" }
    }

    $dist = Join-Path $projectRoot 'dist'
    $bundle = Join-Path $dist 'NavPro.bundle'
    $contents = Join-Path $bundle 'Contents'
    New-Item -ItemType Directory -Path $dist,$bundle,$contents -Force | Out-Null
    Copy-Item -LiteralPath './Deployment/NavPro.bundle/PackageContents.xml' -Destination $bundle -Force
    foreach ($build in $builds) {
        $version = Join-Path $contents $build.Folder
        $languageVersion = Join-Path $version 'en-US'
        $assets = Join-Path $version 'Assets'
        $languageAssets = Join-Path $languageVersion 'Assets'
        New-Item -ItemType Directory -Path $assets,$languageAssets -Force | Out-Null
        Copy-Item -LiteralPath $build.Assembly -Destination $version -Force
        Copy-Item -LiteralPath './Frontend/Plugins/SearchSetCreaterRibbon.xaml' -Destination $version -Force
        Copy-Item -LiteralPath './Frontend/Plugins/SearchSetCreaterRibbon.xaml' -Destination $languageVersion -Force
        $icons = @('SmartReviewMenu16.png','SmartReviewMenu32.png','SmartClashGrouper16.png','SmartClashGrouper32.png','ChainageClashLocator16.png','ChainageClashLocator32.png','AiClashDescription16.png','AiClashDescription32.png','UtilitySeparationChecker16.png','UtilitySeparationChecker32.png','MaintenanceClearanceReview16.png','MaintenanceClearanceReview32.png','CoordinationChangeLog16.png','CoordinationChangeLog32.png','VerticalClearanceChecker16.png','VerticalClearanceChecker32.png','SearchSetCreater16.png','SearchSetCreater32.png','ClashMatrix16.png','ClashMatrix32.png','ClashApprover16.png','ClashApprover32.png','ClashRenamer16.png','ClashRenamer32.png','ClashTolerance16.png','ClashTolerance32.png','ClashStatusImport16.png','ClashStatusImport32.png','ClashRefreshModels16.png','ClashRefreshModels32.png','ClashColorizer16.png','ClashColorizer32.png','ContactInfo16.png','ContactInfo32.png','ClashDashboard16.png','ClashDashboard32.png')
        foreach ($destination in @($assets,$languageAssets)) { Copy-Item -LiteralPath ($icons | ForEach-Object { Join-Path './Assets' $_ }) -Destination $destination -Force }
        foreach ($size in @(16,32)) {
            foreach ($icon in @('SmartReviewMenu','SmartClashGrouper','ChainageClashLocator','AiClashDescription','UtilitySeparationChecker','MaintenanceClearanceReview','CoordinationChangeLog','VerticalClearanceChecker','SearchSetCreater','ClashMatrix','ClashApprover','ClashRenamer','ClashTolerance','ClashStatusImport','ClashRefreshModels','ClashColorizer','ContactInfo','ClashDashboard')) {
                $image = [Drawing.Image]::FromFile((Join-Path $assets "$icon$size.png"))
                try { if ($image.Width -ne $size -or $image.Height -ne $size) { throw "Incorrect $icon icon size in $($build.Folder)" } } finally { $image.Dispose() }
            }
        }
        if ((Get-FileHash -LiteralPath $build.Assembly).Hash -ne (Get-FileHash (Join-Path $version 'NavPro.dll')).Hash) { throw "Payload hash mismatch for $($build.Folder)" }
    }
    Copy-Item -LiteralPath './Deployment/NavPro.bundle/PackageContents.xml' -Destination $bundle -Force
    Copy-Item -LiteralPath './README.md','./VERIFICATION.md' -Destination $contents -Force
    $docs = Join-Path $contents 'Docs'
    New-Item -ItemType Directory -Force -Path $docs | Out-Null
    Copy-Item -LiteralPath './Docs/Jacobs-Branding.md','./Docs/Smart-Review.md','./Docs/Vertical-Clearance-Checker.md','./Docs/Navisworks-2026-Compatibility.md' -Destination $docs -Force

    [xml]$manifest = Get-Content (Join-Path $bundle 'PackageContents.xml') -Raw
    foreach ($relative in @($manifest.ApplicationPackage.HelpFile,$manifest.ApplicationPackage.Icon)) {
        if (!(Test-Path -LiteralPath (Join-Path $bundle $relative))) { throw "Missing payload: $relative" }
    }
    $entries = @($manifest.SelectNodes("//*[local-name()='Components']/*[local-name()='ComponentEntry']"))
    $runtimeRequirements = @($manifest.SelectNodes("//*[local-name()='Components']/*[local-name()='RuntimeRequirements']"))
    if ($entries.Count -ne $builds.Count -or $runtimeRequirements.Count -ne $builds.Count) { throw 'Bundle must declare exactly one runtime and component for each supported major version.' }
    foreach ($build in $builds) {
        $module = "./Contents/$($build.Folder)/NavPro.dll"
        $scopes = @($manifest.SelectNodes("//*[local-name()='Components'][*[local-name()='RuntimeRequirements' and @SeriesMin='$($build.Series)' and @SeriesMax='$($build.Series)']]"))
        if ($scopes.Count -ne 1 -or $scopes[0].SelectNodes("*[local-name()='RuntimeRequirements']").Count -ne 1 -or $scopes[0].SelectNodes("*[local-name()='ComponentEntry']").Count -ne 1 -or $scopes[0].ComponentEntry.ModuleName -ne $module) { throw "Each supported release needs its own Components scope and matching module: $($build.Series)" }
        if (!(Test-Path -LiteralPath (Join-Path $bundle $module))) { throw "Missing module: $module" }
        if (!$manifest.SelectSingleNode("//*[local-name()='Components']/*[local-name()='ComponentEntry' and @ModuleName='$module']")) { throw "Package manifest does not register $module" }
        if (!$manifest.SelectSingleNode("//*[local-name()='Components']/*[local-name()='RuntimeRequirements' and @Platform='NAVMAN' and @SeriesMin='$($build.Series)' and @SeriesMax='$($build.Series)']")) { throw "Package manifest runtime does not target $($build.Series)" }
        foreach ($locale in @($build.Folder, "$($build.Folder)/en-US")) {
            $ribbonPath = Join-Path $contents "$locale/SearchSetCreaterRibbon.xaml"
            if (!(Test-Path -LiteralPath $ribbonPath)) { throw "Missing ribbon layout: $ribbonPath" }
            [xml]$ribbon = Get-Content -LiteralPath $ribbonPath -Raw
            if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonTab' and @Id='NavProTab']")) { throw "NavPro ribbon tab missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='SearchSetCreater' and @Text='SetCreator']")) { throw "SetCreator button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Search Sets']")) { throw "Search Sets ribbon panel missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='ClashMatrix']")) { throw "Clash Matrix button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='ClashApprover' and @Text='Status Updater']")) { throw "Status Updater button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashRenamer' and @Text='Renamer']")) { throw "Renamer button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Export']/*[local-name()='NWRibbonButton' and @Id='ClashDashboard' and @Text='Clash Dashboard']")) { throw "Clash Dashboard button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='ContactInfo' and @Text='Contact Info']")) { throw "Contact Info button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashTolerance' and @Text='Tolerance Updater']")) { throw "Tolerance Updater button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashStatusImport' and @Text='Import Clash Status']")) { throw "Import Clash Status button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashRefreshModels' and @Text='Refresh Models']")) { throw "Refresh Models button missing in $locale" }
            if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashColorizer' and @Text='Colorizer']")) { throw "Colorizer button missing in $locale" }
            $smartPanel = $ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Smart Review']")
            $smartControls = @($smartPanel.SelectNodes('*'))
            if ($smartControls.Count -ne 1 -or $smartControls[0].LocalName -ne 'NWRibbonSplitButton') { throw "Smart Review must contain one dropdown in $locale" }
            $smartMenu = $smartControls[0]
            if ($smartMenu.Id -ne 'SmartReviewMenu' -or $smartMenu.Text -ne 'Smart Review' -or $smartMenu.IsSplit -ne 'False' -or $smartMenu.IsSynchronizedWithCurrentItem -ne 'False' -or $smartMenu.ListStyle -ne 'List' -or $smartMenu.ListImageSize -ne 'Standard') { throw "Smart Review dropdown configuration mismatch in $locale" }
            $smartItems = @($smartMenu.SelectNodes("*[local-name()='NWRibbonButton']"))
            foreach ($item in $smartItems) {
                if ($item.Size -ne 'Standard' -or $item.Orientation -ne 'Horizontal' -or $item.ShowText -ne 'True') { throw "Smart Review menu item presentation mismatch in $locale" }
                if ($ribbon.SelectNodes("//*[@Id='$($item.Id)']").Count -ne 1) { throw "Duplicate Smart Review command in $locale" }
            }
            $smartButtons = @($smartItems | ForEach-Object { $_.Id })
            if (($smartButtons -join ',') -ne 'ChainageClashLocator,AiClashDescription,UtilitySeparationChecker,MaintenanceClearanceReview,CoordinationChangeLog,VerticalClearanceChecker') { throw "Unexpected Smart Review button order in $locale" }
            foreach ($tool in @('SmartClashGrouper','ChainageClashLocator','AiClashDescription','UtilitySeparationChecker','MaintenanceClearanceReview','CoordinationChangeLog','VerticalClearanceChecker')) {
                if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='$tool']")) { throw "Missing new review tool $tool in $locale" }
            }
            $reviewButtons = @($ribbon.SelectNodes("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton']") | ForEach-Object { $_.Id })
            if (($reviewButtons -join ',') -ne 'ClashRenamer,ClashTolerance,ClashApprover,ClashStatusImport,ClashRefreshModels,ClashColorizer,SmartClashGrouper') { throw "Unexpected Clash Review button order in $locale" }
        }
    }
    Write-Output 'PASS package paths, v21/v22/v23 runtime targets, locale ribbon assets, 16/32px icons and per-binary SHA256.'
    if ($CreateZip) {
        $packageName = 'NavPro-' + $manifest.ApplicationPackage.AppVersion + '-Navisworks2024-2025-2026.zip'
        Compress-Archive -LiteralPath $bundle -DestinationPath (Join-Path $dist $packageName) -Force
        Write-Output "Shareable package: $(Join-Path $dist $packageName)"
    }
} finally { Pop-Location }
