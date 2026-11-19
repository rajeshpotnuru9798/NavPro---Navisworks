param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../Assets'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach ($size in @(16,32)) {
    # Code-native icon: blue model stack + mint search lens. No external image dependency.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(24,111,189))
    $light = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(73,161,226))
    $mint = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(15,205,161),3)
    $white = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.3)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($blue,3,3,18,23)
        $graphics.FillRectangle($light,3,3,18,5)
        foreach ($y in @(12,17,22)) { $graphics.DrawLine($white,6,$y,16,$y) }
        $graphics.DrawEllipse($mint,16,14,11,11)
        $graphics.DrawLine($mint,25,24,30,30)
        $bitmap.Save((Join-Path $OutputDirectory "SearchSetCreater$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $white.Dispose(); $mint.Dispose(); $light.Dispose(); $blue.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Clash Matrix icon: four coordination cells with two highlighted intersecting disciplines.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $teal = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0,130,153))
    $pink = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230,80,170))
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.5)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($navy,3,3,26,26)
        $graphics.FillRectangle($teal,8,8,7,7)
        $graphics.FillRectangle($pink,17,8,7,7)
        $graphics.FillRectangle($pink,8,17,7,7)
        $graphics.FillRectangle($teal,17,17,7,7)
        $graphics.DrawRectangle($whitePen,7,7,18,18)
        $graphics.DrawLine($whitePen,16,7,16,25)
        $graphics.DrawLine($whitePen,7,16,25,16)
        $bitmap.Save((Join-Path $OutputDirectory "ClashMatrix$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $whitePen.Dispose(); $pink.Dispose(); $teal.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Contact Info icon: clear support card with a teal envelope mark.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $tealPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0,190,177),2.2)
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.8)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillEllipse($navy,2,2,28,28)
        $graphics.DrawRectangle($tealPen,7,9,18,14)
        $graphics.DrawLine($whitePen,7,10,16,17)
        $graphics.DrawLine($whitePen,25,10,16,17)
        $bitmap.Save((Join-Path $OutputDirectory "ContactInfo$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $whitePen.Dispose(); $tealPen.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Clash Approver icon: approval check over a compact clash-result list.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $teal = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0,130,153))
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,2)
    $checkPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,2.8)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($navy,3,3,26,26)
        foreach ($y in @(9,15,21)) { $graphics.DrawLine($whitePen,7,$y,10,$y) }
        $graphics.DrawLine($whitePen,13,9,23,9)
        $graphics.DrawLine($whitePen,13,15,23,15)
        $graphics.DrawLine($whitePen,13,21,18,21)
        $graphics.FillEllipse($teal,16,16,15,15)
        $graphics.DrawLine($checkPen,20,23,23,26)
        $graphics.DrawLine($checkPen,23,26,28,20)
        $bitmap.Save((Join-Path $OutputDirectory "ClashApprover$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $checkPen.Dispose(); $whitePen.Dispose(); $teal.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Clash Dashboard icon: report page with a compact status bar chart.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(35,30,220))
    $cyan = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90,230,255))
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.4)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($navy,4,2,24,28)
        $graphics.DrawLine($whitePen,8,8,22,8)
        $graphics.DrawLine($whitePen,8,12,18,12)
        $graphics.FillRectangle($blue,8,21,4,6)
        $graphics.FillRectangle($cyan,14,17,4,10)
        $graphics.FillRectangle($blue,20,14,4,13)
        $bitmap.Save((Join-Path $OutputDirectory "ClashDashboard$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $whitePen.Dispose(); $cyan.Dispose(); $blue.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Tolerance Updater: three adjustable distance sliders.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $cyan = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90,230,255))
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.7)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($navy,3,3,26,26)
        foreach ($y in @(9,16,23)) { $graphics.DrawLine($whitePen,7,$y,25,$y) }
        $graphics.FillEllipse($cyan,10,6,6,6)
        $graphics.FillEllipse($cyan,18,13,6,6)
        $graphics.FillEllipse($cyan,8,20,6,6)
        $bitmap.Save((Join-Path $OutputDirectory "ClashTolerance$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $whitePen.Dispose(); $cyan.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Clash Renamer icon: test-name lines with an editing pencil.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $cyan = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90,230,255),2.2)
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.7)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($navy,3,3,26,26)
        $graphics.DrawLine($whitePen,7,9,20,9)
        $graphics.DrawLine($whitePen,7,15,17,15)
        $graphics.DrawLine($whitePen,7,21,13,21)
        $graphics.DrawLine($cyan,17,24,26,15)
        $graphics.DrawLine($cyan,22,13,28,19)
        $graphics.DrawLine($cyan,16,25,15,29)
        $bitmap.Save((Join-Path $OutputDirectory "ClashRenamer$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $whitePen.Dispose(); $cyan.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Import Clash Status icon: status rows flowing into the model with an import arrow.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $magenta = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(165,0,164))
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.8)
    $accentPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220,120,220),2.2)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($navy,3,3,26,26)
        foreach ($y in @(9,15,21)) { $graphics.DrawLine($whitePen,6,$y,15,$y) }
        $graphics.DrawLine($accentPen,17,16,25,16)
        $graphics.DrawLine($accentPen,21,12,25,16)
        $graphics.DrawLine($accentPen,21,20,25,16)
        $graphics.FillEllipse($magenta,20,20,9,9)
        $graphics.DrawLine($whitePen,22,24,24,26)
        $graphics.DrawLine($whitePen,24,26,27,22)
        $bitmap.Save((Join-Path $OutputDirectory "ClashStatusImport$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $accentPen.Dispose(); $whitePen.Dispose(); $magenta.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Refresh Models icon: linked model tile with a two-way refresh arrow.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $cyan = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90,230,255),2.2)
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.4)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($navy,3,5,18,22)
        $graphics.DrawRectangle($whitePen,7,9,10,13)
        $graphics.DrawLine($whitePen,9,13,15,13)
        $graphics.DrawLine($whitePen,9,17,15,17)
        $graphics.DrawArc($cyan,13,3,16,16,205,230)
        $graphics.DrawLine($cyan,13,8,13,13)
        $graphics.DrawLine($cyan,13,13,18,13)
        $graphics.DrawArc($cyan,3,15,16,16,25,230)
        $graphics.DrawLine($cyan,19,27,19,22)
        $graphics.DrawLine($cyan,19,22,14,22)
        $bitmap.Save((Join-Path $OutputDirectory "ClashRefreshModels$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $whitePen.Dispose(); $cyan.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }

    # Colorizer icon: palette dots beside a model sheet.
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0),($size / 32.0))
    $navy = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,39,58))
    $magenta = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(165,0,164))
    $cyan = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0,190,177))
    $gold = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,190,45))
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,1.5)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($navy,3,3,26,26)
        $graphics.DrawRectangle($whitePen,6,6,12,19)
        $graphics.DrawLine($whitePen,9,11,15,11)
        $graphics.DrawLine($whitePen,9,15,15,15)
        $graphics.FillEllipse($magenta,16,7,10,10)
        $graphics.FillEllipse($cyan,19,17,10,10)
        $graphics.FillEllipse($gold,10,21,7,7)
        $bitmap.Save((Join-Path $OutputDirectory "ClashColorizer$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $whitePen.Dispose(); $gold.Dispose(); $cyan.Dispose(); $magenta.Dispose(); $navy.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
