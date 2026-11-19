param()
$ErrorActionPreference = 'Stop'
if (Get-Process -Name Roamer -ErrorAction SilentlyContinue) { throw 'Close Navisworks before installing NavPro.' }
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../dist/NavPro.bundle'))
foreach ($version in @('v21','v22','v23')) {
    if (!(Test-Path -LiteralPath (Join-Path $source "Contents/$version/NavPro.dll"))) { throw "Build both supported NavPro versions first with Tools/Build-Package.ps1. Missing $version binary." }
}
$parent = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Autodesk/ApplicationPlugins'
$target = Join-Path $parent 'NavPro.bundle'
New-Item -ItemType Directory -Path $parent -Force | Out-Null
if (Test-Path -LiteralPath $target) {
    # Keep old bundle outside discovery, so old/new copies cannot both load.
    $backupRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'NavPro/Backups'
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    $backup = Join-Path $backupRoot ('NavPro-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.bundle')
    Move-Item -LiteralPath $target -Destination $backup
    Write-Output "Previous NavPro backed up to $backup"
}
Copy-Item -LiteralPath $source -Destination $target -Recurse
$files = Get-ChildItem -LiteralPath $source -File -Recurse
foreach ($file in $files) {
    $relative = $file.FullName.Substring($source.Length + 1)
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $relative)).Hash) { throw "Installed file mismatch: $relative" }
}
Write-Output "Installed $($files.Count) hash-verified files at $target. The bundle targets Navisworks Manage 2024 (v21), 2025 (v22) and 2026 (v23); open the intended release and verify NavPro > Search Sets > SetCreator, Clash Matrix > Clash Matrix, Clash Review tools, Export > Clash Dashboard and Help > Contact Info. This is not an in-host test."
