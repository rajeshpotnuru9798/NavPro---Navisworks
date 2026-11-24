param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../Assets'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# WPF renders the approved path-only SVG masters using the same pipeline as the review sheet.
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File $PSCommandPath -OutputDirectory $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Approved icon rendering failed.' }
    return
}
Add-Type -AssemblyName PresentationCore,WindowsBase
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$masters = Join-Path $PSScriptRoot '../Assets/IconSources'
$keys = @('ContactInfo','SearchSetCreater','ClashMatrix','ClashRenamer','ClashTolerance','ClashApprover','ClashStatusImport','ClashRefreshModels','ClashColorizer','SmartClashGrouper','ChainageClashLocator','AiClashDescription','UtilitySeparationChecker','MaintenanceClearanceReview','CoordinationChangeLog','VerticalClearanceChecker','ClashDashboard','SmartReviewMenu')
function Get-IconBrush([string]$value) {
    if ($value -eq 'none') { return $null }
    return [Windows.Media.BrushConverter]::new().ConvertFromString($value)
}
foreach ($key in $keys) {
    [xml]$svg = Get-Content -LiteralPath (Join-Path $masters ($key+'.svg')) -Raw
    if ($svg.DocumentElement.viewBox -ne '0 0 32 32') { throw "Unexpected icon canvas: $key" }
    foreach ($size in @(16,32)) {
        $visual = [Windows.Media.DrawingVisual]::new()
        $drawing = $visual.RenderOpen()
        try {
            $drawing.PushTransform([Windows.Media.ScaleTransform]::new($size/32.0,$size/32.0))
            foreach ($path in $svg.DocumentElement.ChildNodes) {
                if ($path.LocalName -ne 'path') { throw "Unsupported SVG element in $key" }
                $pen = $null
                if ($path.stroke -ne 'none') {
                    $width = [double]::Parse($path.GetAttribute('stroke-width'),[Globalization.CultureInfo]::InvariantCulture)
                    $pen = [Windows.Media.Pen]::new((Get-IconBrush $path.stroke),$width)
                    $pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round
                    $pen.LineJoin = [Windows.Media.PenLineJoin]::Round
                }
                $drawing.DrawGeometry((Get-IconBrush $path.fill),$pen,[Windows.Media.Geometry]::Parse($path.d))
            }
            $drawing.Pop()
        } finally { $drawing.Close() }
        $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($visual)
        $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream = [IO.File]::Create((Join-Path $OutputDirectory ($key+$size+'.png')))
        try { $encoder.Save($stream) } finally { $stream.Dispose() }
    }
}
Write-Output 'Rendered 18 approved icons at 16px and 32px from versioned vector masters.'
