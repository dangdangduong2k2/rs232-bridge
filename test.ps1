$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$testDir = Join-Path $root 'test-results'
$bridge = Join-Path $root 'payload\bin\x64\NationZkBridge.exe'
Copy-Item -LiteralPath $bridge -Destination $testDir
Copy-Item -LiteralPath (Join-Path $root 'bridge-tests\vendor\GReaderApi.dll') -Destination $testDir
foreach ($test in @('ProtocolTests','SdkIntegration')) {
    $reference = if ($test -eq 'ProtocolTests') { 'NationZkBridge.exe' } else { 'GReaderApi.dll' }
    & $compiler /nologo /platform:x64 "/out:$testDir\$test.exe" "/r:$testDir\$reference" (Join-Path $root "bridge-tests\$test.cs")
    if ($LASTEXITCODE -ne 0) { throw 'Test compile failed' }
}
& (Join-Path $testDir 'ProtocolTests.exe') | Tee-Object -FilePath (Join-Path $testDir 'protocol-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed' }
& $compiler /nologo /platform:x64 "/out:$testDir\WriteTests.exe" "/r:$testDir\NationZkBridge.exe" "/r:$testDir\GReaderApi.dll" (Join-Path $root 'bridge-tests\WriteTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Write test compile failed' }
& (Join-Path $testDir 'WriteTests.exe') | Tee-Object -FilePath (Join-Path $testDir 'write-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Write tests failed' }
$stopFile = Join-Path $testDir ('stop-' + [guid]::NewGuid().ToString() + '.flag')
$process = Start-Process -FilePath $bridge -ArgumentList @('--simulate','--antennas','4','--listen','18162','--stop-file',('"' + $stopFile + '"')) -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Milliseconds 500
    if ($process.HasExited) { throw 'Simulation bridge exited early' }
    & (Join-Path $testDir 'SdkIntegration.exe') '127.0.0.1:18162' | Tee-Object -FilePath (Join-Path $testDir 'sdk-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'SDK tests failed' }
    & (Join-Path $root 'payload\diagnostics\NationSerialCheck.exe') '--test-tcp' '127.0.0.1:18162' | Tee-Object -FilePath (Join-Path $testDir 'diagnostic-test.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostic helper failed' }
    Set-Content -LiteralPath $stopFile -Value 'stop'
    if (-not $process.WaitForExit(5000)) { throw 'Graceful stop failed' }
    if ($process.ExitCode -ne 0) { throw 'Graceful stop did not return success' }
    'PASS: graceful file-triggered stop exits with code 0' | Set-Content -LiteralPath (Join-Path $testDir 'service-stop-test.txt')
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
    if (Test-Path -LiteralPath $stopFile) { Remove-Item -LiteralPath $stopFile }
}
$extractDir = Join-Path $testDir ('extracted-' + [guid]::NewGuid().ToString())
$extract = Start-Process -FilePath (Join-Path $root 'NationComPortSetup.exe') -ArgumentList @('/extract',('"' + $extractDir + '"')) -WindowStyle Hidden -PassThru -Wait
if ($extract.ExitCode -ne 0) { throw 'Self-extraction failed' }
$files = Get-ChildItem -LiteralPath (Join-Path $root 'payload') -File -Recurse
foreach ($f in $files) {
    $relative = $f.FullName.Substring((Join-Path $root 'payload').Length + 1)
    if ((Get-FileHash -LiteralPath $f.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $extractDir $relative)).Hash) { throw "Extraction mismatch: $relative" }
}
"PASS: self-contained EXE extracted $($files.Count) files with matching SHA256 hashes" | Set-Content -LiteralPath (Join-Path $testDir 'payload-test.txt')
Write-Output 'Passed local packaging and protocol tests. No drivers installed; no physical COM opened.'
