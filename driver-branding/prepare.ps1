param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$archive = Join-Path $repo 'payload\driver\com0com-source.zip'
$expectedHash = '6751E911F73980B23CC878A456EB99D1DC6D0603C11C1D3CED109ABE1C556380'
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Upstream source hash changed. Review the source before applying branding.'
}
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo ('test-results\branded-driver-' + [guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'OutputDirectory must be a new directory; existing files are never overwritten.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $prefix = $output.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($entry in $zip.Entries) {
        $target = [IO.Path]::GetFullPath((Join-Path $output $entry.FullName))
        if ($entry.FullName.Contains(':') -or !$target.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid archive path.' }
    }
} finally { $zip.Dispose() }
[IO.Compression.ZipFile]::ExtractToDirectory($archive,$output)
$source = Join-Path $output 'com0com-3.0.0.0'
$encoding = New-Object System.Text.UTF8Encoding($false)
$changes = New-Object 'System.Collections.Generic.List[object]'
function Replace-Source([string]$relative,[hashtable]$replacements) {
    $path = Join-Path $source $relative
    $before = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $text = [IO.File]::ReadAllText($path)
    foreach ($old in $replacements.Keys) {
        if ([regex]::Matches($text,[regex]::Escape($old)).Count -ne 1) { throw "Expected exactly one branding target in $relative" }
        $text = $text.Replace($old,[string]$replacements[$old])
    }
    if ($relative.EndsWith('.inf')) { $text = "; Nation RS232 bridge display branding, modified 2026-09-30. See NATION-BRANDING.md.`r`n" + $text }
    [IO.File]::WriteAllText($path,$text,$encoding)
    $changes.Add([pscustomobject]@{file=$relative;beforeSha256=$before;afterSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash})
}

# Use the same A/B relation that upstream PdoPortQueryCaps uses for Address/UINumber.
# Setup.cs maps A to NationPort and B to BridgePort. COM numbering is not changed.
$oldText = 'StrAppendStr0(&status, &portText, L"com0com - serial port emulator");'
$newText = @'
/* Nation RS232 bridge branding, modified 2026-09-30.
       * A is the application endpoint; B is the internal worker endpoint.
       * Original com0com copyright and license remain in this source tree.
       */
      StrAppendStr0(&status, &portText,
          pDevExt->pIoPortLocal == &pDevExt->pBusExt->childs[0].ioPort
              ? L"Nation COM Port" : L"Nation Bridge Internal");
'@
$newText = $newText -replace '\r?\n', "`r`n"
Replace-Source 'sys\pnp.c' @{$oldText=$newText}
Replace-Source 'com0com.inf' @{
    'com0com.BusDesc = "com0com - bus for serial port pair emulator"'='com0com.BusDesc = "Nation RS232 Bridge Bus"'
    'com0com.SrvDesc = "com0com - emulates the serial ports interconnected via a null-modem cable"'='com0com.SrvDesc = "Nation RS232 Bridge Serial Driver"'
    'ClassName = "com0com - serial port emulators"'='ClassName = "Nation RS232 Bridge Ports"'
    'DiskId1 = "Installation Disk #1 (com0com - Null-modem emulator)"'='DiskId1 = "Nation RS232 Bridge Driver"'
}
foreach ($inf in @('comport.inf','cncport.inf')) {
    Replace-Source $inf @{
        'com0com.PortDesc = "com0com - serial port emulator"'='com0com.PortDesc = "Nation COM Port"'
        'com0com.SrvDesc = "com0com - emulates the serial ports interconnected via a null-modem cable"'='com0com.SrvDesc = "Nation RS232 Bridge Serial Driver"'
    }
}
$manifest = [pscustomobject]@{
    status='SOURCE_PREPARED_ONLY_NOT_BUILT_OR_SIGNED'
    upstreamArchiveSha256=$expectedHash
    applicationEndpoint='Nation COM Port'
    workerEndpoint='Nation Bridge Internal'
    changes=$changes.ToArray()
}
[IO.File]::WriteAllText((Join-Path $output 'branding-manifest.json'),($manifest | ConvertTo-Json -Depth 5),$encoding)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $source 'NATION-BRANDING.md')
Write-Output "Prepared source: $source"
Write-Output 'Not compiled, signed or installed. The running RC6 driver has not been changed.'
