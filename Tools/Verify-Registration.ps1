param([string]$NavisworksInstallDir = 'C:\Program Files\Autodesk\Navisworks Manage 2024', [string]$PluginAssemblyPath = '')
# Run in Windows PowerShell 5.1. Metadata-only: this does NOT launch/initialize Navisworks.
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run this metadata check with powershell.exe (Windows PowerShell 5.1).' }
$apiPath = Join-Path $NavisworksInstallDir 'Autodesk.Navisworks.Api.dll'
$api = [Reflection.Assembly]::ReflectionOnlyLoadFrom($apiPath)
$apiMajor = $api.GetName().Version.Major
if ([string]::IsNullOrWhiteSpace($PluginAssemblyPath)) { $PluginAssemblyPath = Join-Path $PSScriptRoot '../bin/Release/net48/NavPro.dll' }
$PluginAssemblyPath = [IO.Path]::GetFullPath($PluginAssemblyPath)
$resolve = [ResolveEventHandler] {
    param($sender,$eventArgs)
    $dependencyName = ([Reflection.AssemblyName]::new($eventArgs.Name)).Name + '.dll'
    $dependencyPath = Join-Path $NavisworksInstallDir $dependencyName
    if (Test-Path -LiteralPath $dependencyPath) { [Reflection.Assembly]::ReflectionOnlyLoadFrom($dependencyPath) }
    else { [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name) }
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolve)
try {
    $plugin = [Reflection.Assembly]::ReflectionOnlyLoadFrom($PluginAssemblyPath)
    $type = $plugin.GetType('NavPro.Frontend.Plugins.SearchSetCreaterPlugin',$true)
    if ($type.BaseType.FullName -ne 'Autodesk.Navisworks.Api.Plugins.CommandHandlerPlugin') { throw 'Incorrect plugin base class' }
    $attributes = [Reflection.CustomAttributeData]::GetCustomAttributes($type)
    $registration = $attributes | Where-Object { $_.AttributeType.FullName -eq 'Autodesk.Navisworks.Api.Plugins.PluginAttribute' }
    if ($registration.ConstructorArguments[0].Value -ne 'NavPro') { throw 'Unexpected registered name' }
    $name = $registration.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($name.TypedValue.Value -ne 'NavPro - SetCreator') { throw 'Unexpected plug-in display name' }
    $layout = $attributes | Where-Object { $_.AttributeType.FullName -eq 'Autodesk.Navisworks.Api.Plugins.RibbonLayoutAttribute' }
    if (!$layout -or $layout.ConstructorArguments[0].Value -ne 'SearchSetCreaterRibbon.xaml') { throw 'Custom ribbon layout missing' }
    $tab = $attributes | Where-Object { $_.AttributeType.FullName -eq 'Autodesk.Navisworks.Api.Plugins.RibbonTabAttribute' }
    if (!$tab -or $tab.ConstructorArguments[0].Value -ne 'NavProTab') { throw 'NavPro ribbon tab metadata missing' }
    $command = $attributes | Where-Object { $_.AttributeType.FullName -eq 'Autodesk.Navisworks.Api.Plugins.CommandAttribute' }
    if (!$command -or !($command | Where-Object { $_.ConstructorArguments[0].Value -eq 'SearchSetCreater' })) { throw 'SearchSetCreater command metadata missing' }
    $setCreatorCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'SearchSetCreater' }
    $setCreatorDisplay = $setCreatorCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($setCreatorDisplay.TypedValue.Value -ne 'SetCreator') { throw 'SetCreator visible command name missing' }
    if (!($command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ClashMatrix' })) { throw 'Clash Matrix command metadata missing' }
    $approverCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ClashApprover' }
    if (!$approverCommand) { throw 'Clash Approver command metadata missing' }
    $statusImportCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ClashStatusImport' }
    if (!$statusImportCommand) { throw 'Import Clash Status command metadata missing' }
    $statusImportDisplay = $statusImportCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($statusImportDisplay.TypedValue.Value -ne 'Import Clash Status') { throw 'Import Clash Status display name mismatch' }
    $refreshModelsCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ClashRefreshModels' }
    if (!$refreshModelsCommand) { throw 'Refresh Models command metadata missing' }
    $refreshModelsDisplay = $refreshModelsCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($refreshModelsDisplay.TypedValue.Value -ne 'Refresh Models') { throw 'Refresh Models display name mismatch' }
    $colorizerCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ClashColorizer' }
    if (!$colorizerCommand) { throw 'Colorizer command metadata missing' }
    $colorizerDisplay = $colorizerCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($colorizerDisplay.TypedValue.Value -ne 'Colorizer') { throw 'Colorizer display name mismatch' }
    $toleranceCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ClashTolerance' }
    if (!$toleranceCommand) { throw 'Tolerance Updater command metadata missing' }
    $toleranceDisplay = $toleranceCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($toleranceDisplay.TypedValue.Value -ne 'Tolerance Updater') { throw 'Tolerance Updater display name mismatch' }
    $renamerCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ClashRenamer' }
    if (!$renamerCommand) { throw 'Clash Renamer command metadata missing' }
    $renamerDisplay = $renamerCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($renamerDisplay.TypedValue.Value -ne 'Renamer') { throw 'Clash Renamer display name mismatch' }
    $approverDisplay = $approverCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($approverDisplay.TypedValue.Value -ne 'Status Updater') { throw 'Status Updater display name mismatch' }
    $contactCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ContactInfo' }
    if (!$contactCommand) { throw 'Contact Info command metadata missing' }
    $contactDisplay = $contactCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($contactDisplay.TypedValue.Value -ne 'Contact Info') { throw 'Contact Info display name mismatch' }
    $dashboardCommand = $command | Where-Object { $_.ConstructorArguments[0].Value -eq 'ClashDashboard' }
    if (!$dashboardCommand) { throw 'Clash Dashboard command metadata missing' }
    $dashboardDisplay = $dashboardCommand.NamedArguments | Where-Object { $_.MemberName -eq 'DisplayName' }
    if ($dashboardDisplay.TypedValue.Value -ne 'Clash Dashboard') { throw 'Clash Dashboard display name mismatch' }
    [xml]$ribbon = Get-Content (Join-Path $PSScriptRoot '../Frontend/Plugins/SearchSetCreaterRibbon.xaml') -Raw
    if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonTab' and @Id='NavProTab']")) { throw 'Ribbon XAML tab ID mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='SearchSetCreater']")) { throw 'Ribbon XAML command ID mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='SearchSetCreater' and @Text='SetCreator']")) { throw 'SetCreator ribbon label mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='ClashMatrix']")) { throw 'Ribbon XAML Clash Matrix command ID mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='ClashApprover' and @Text='Status Updater']")) { throw 'Ribbon XAML Status Updater button mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashStatusImport' and @Text='Import Clash Status']")) { throw 'Ribbon XAML Import Clash Status button mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashRefreshModels' and @Text='Refresh Models']")) { throw 'Ribbon XAML Refresh Models button mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashColorizer' and @Text='Colorizer']")) { throw 'Ribbon XAML Colorizer button mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashRenamer' and @Text='Renamer']")) { throw 'Ribbon XAML Clash Renamer button mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='NWRibbonButton' and @Id='ContactInfo' and @Text='Contact Info']")) { throw 'Ribbon XAML Contact Info button mismatch' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Export']/*[local-name()='NWRibbonButton' and @Id='ClashDashboard' and @Text='Clash Dashboard']")) { throw 'Clash Dashboard is not in the Export panel' }
    if (!$ribbon.SelectSingleNode("//*[local-name()='RibbonPanelSource' and @Title='Clash Review']/*[local-name()='NWRibbonButton' and @Id='ClashTolerance' and @Text='Tolerance Updater']")) { throw 'Tolerance Updater ribbon button mismatch' }
    $reference = $plugin.GetReferencedAssemblies() | Where-Object { $_.Name -eq 'Autodesk.Navisworks.Api' }
    $clashReference = $plugin.GetReferencedAssemblies() | Where-Object { $_.Name -eq 'Autodesk.Navisworks.Clash' }
    if (!$reference -or $reference.Version.Major -ne $apiMajor) { throw 'Plug-in API reference does not match the supplied Navisworks API.' }
    if (!$clashReference -or $clashReference.Version.Major -ne $apiMajor) { throw 'Plug-in Clash API reference does not match the supplied Navisworks API.' }
    Write-Output "PASS metadata registration: $($type.FullName) derives from CommandHandlerPlugin; SearchSetCreater, ClashMatrix, ClashApprover (Status Updater), ClashStatusImport, ClashRefreshModels, ClashColorizer, ClashRenamer, ClashTolerance, ContactInfo and ClashDashboard command IDs match XAML; API=$($reference.Version), Clash=$($clashReference.Version)"
    Write-Output 'Host discovery, icon rendering and native searching are NOT verified by this check.'
} finally { [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolve) }
