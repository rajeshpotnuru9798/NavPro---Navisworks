param(
    [Parameter(Mandatory=$true)][string]$Report,
    [int]$ExpectedResults = 34662,
    [string]$SamplesDirectory = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$stream = [IO.StreamReader]::new([IO.Path]::GetFullPath($Report))
$json = $null
$rows = 0; $images = 0; $missing = 0; $bad = 0; $notes = 0
$samples = @(1, 9002, 15000, 25000, $ExpectedResults)
$sampleRows = @()
try {
    # Start the streaming JSON reader at the embedded snapshot, without materializing
    # hundreds of MB of HTML, JSON and base64 into several simultaneous full copies.
    $marker = '<script type="application/json" id="reportData">'
    $tail = ''; $scanned = 0
    while (!$tail.EndsWith($marker, [StringComparison]::Ordinal)) {
        $c = $stream.Read()
        if ($c -lt 0 -or $scanned++ -gt 2000000) { throw 'Embedded dashboard snapshot marker not found.' }
        $tail += [char]$c
        if ($tail.Length -gt $marker.Length) { $tail = $tail.Substring($tail.Length - $marker.Length) }
    }
    $json = [Newtonsoft.Json.JsonTextReader]::new($stream)
    $json.DateParseHandling = [Newtonsoft.Json.DateParseHandling]::None
    $json.CloseInput = $false
    $insideResults = $false; $resultDepth = -1
    while ($json.Read()) {
        if ($json.TokenType -eq 'PropertyName' -and $json.Value -ceq 'Results') {
            [void]$json.Read(); $insideResults = $true; $resultDepth = $json.Depth; continue
        }
        if ($insideResults -and $json.TokenType -eq 'EndArray' -and $json.Depth -eq $resultDepth) { $insideResults = $false }
        if ($insideResults -and $json.TokenType -eq 'StartObject' -and $json.Depth -eq ($resultDepth + 1)) { $rows++; $clashName = ''; $testName = '' }
        if ($insideResults -and $json.TokenType -eq 'PropertyName') {
            $key = [string]$json.Value
            if ($key -in @('ClashName','TestName','ImageData','ImageNote')) {
                [void]$json.Read()
                switch -CaseSensitive ($key) {
                    'ClashName' { $clashName = [string]$json.Value }
                    'TestName' { $testName = [string]$json.Value }
                    'ImageNote' { if (![string]::IsNullOrEmpty([string]$json.Value)) { $notes++ } }
                    'ImageData' {
                        if ([string]::IsNullOrEmpty([string]$json.Value)) { $missing++; continue }
                        $images++
                        $memory = $null; $bitmap = $null
                        try {
                            $bytes = [Convert]::FromBase64String([string]$json.Value)
                            $memory = [IO.MemoryStream]::new($bytes, $false)
                            $bitmap = [Drawing.Image]::FromStream($memory, $false, $true)
                            if ($bitmap.Width -ne 420 -or $bitmap.Height -ne 240) { $bad++ }
                            if ($rows -in $samples) {
                                $sampleRows += [pscustomobject]@{Row=$rows;Test=$testName;Clash=$clashName;Width=$bitmap.Width;Height=$bitmap.Height}
                                if ($SamplesDirectory) {
                                    [IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($SamplesDirectory)) | Out-Null
                                    [IO.File]::WriteAllBytes((Join-Path $SamplesDirectory ('clash-image-' + $rows + '.jpg')), $bytes)
                                }
                            }
                        }
                        catch { $bad++ }
                        finally { if ($bitmap) {$bitmap.Dispose()}; if ($memory) {$memory.Dispose()} }
                    }
                }
            }
        }
        if ($json.TokenType -eq 'EndObject' -and $json.Depth -eq 0) { break }
    }
    [pscustomobject]@{Report=[IO.Path]::GetFullPath($Report);Rows=$rows;Images=$images;Missing=$missing;InvalidImages=$bad;ImageNotes=$notes;Samples=$sampleRows} | ConvertTo-Json -Depth 5
    if ($rows -ne $ExpectedResults -or $images -ne $rows -or $missing -ne 0 -or $bad -ne 0 -or $notes -ne 0) { exit 1 }
}
finally { if ($json) {$json.Close()}; $stream.Dispose() }
