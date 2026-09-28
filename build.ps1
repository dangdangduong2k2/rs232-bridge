$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
New-Item -ItemType Directory -Force -Path (Join-Path $root 'test-results') | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$payload = Join-Path $root 'payload'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'bridge-src') -Filter '*.cs' | ForEach-Object FullName)
foreach ($arch in @('x86','x64')) {
    $bin = Join-Path $payload "bin\$arch"
    New-Item -ItemType Directory -Force -Path $bin | Out-Null
    & $compiler /nologo /optimize+ /target:exe "/platform:$arch" "/out:$bin\NationZkBridge.exe" $sources
    if ($LASTEXITCODE -ne 0) { throw 'Bridge build failed' }
    Copy-Item -LiteralPath (Join-Path $root 'bridge-src\App.config') -Destination "$bin\NationZkBridge.exe.config"
    Copy-Item -LiteralPath (Join-Path $payload "vendor\zk\$arch\UHFReader288.dll") -Destination $bin
}
$common = Join-Path $root 'src\Common.cs'
$protocol = Join-Path $root 'bridge-src\Protocol.cs'
$diagnostics = Join-Path $payload 'diagnostics'
New-Item -ItemType Directory -Force -Path $diagnostics | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'bridge-tests\vendor\GReaderApi.dll') -Destination $diagnostics
& $compiler /nologo /target:exe "/r:$diagnostics\GReaderApi.dll" "/out:$diagnostics\NationSerialCheck.exe" (Join-Path $root 'src\NationSerialCheck.cs')
if ($LASTEXITCODE -ne 0) { throw 'Original Nation SDK diagnostic build failed' }
Copy-Item -LiteralPath (Join-Path $root 'bridge-src\App.config') -Destination "$diagnostics\NationSerialCheck.exe.config"
& $compiler /nologo /optimize+ /target:winexe /r:System.Management.dll /r:System.ServiceProcess.dll "/out:$payload\NationComService.exe" $common $protocol (Join-Path $root 'src\Service.cs')
if ($LASTEXITCODE -ne 0) { throw 'Service build failed' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $root 'payload.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
[System.IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip)
& $compiler /nologo /optimize+ /target:winexe /r:System.Management.dll /r:System.ServiceProcess.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/resource:$zip,payload.zip" "/out:$root\NationComPortSetup.exe" $common $protocol (Join-Path $root 'src\Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
& $compiler /nologo /target:exe /r:System.Management.dll "/out:$root\test-results\SetupTests.exe" $common $protocol (Join-Path $root 'src\SetupTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Setup test build failed' }
& (Join-Path $root 'test-results\SetupTests.exe') | Tee-Object -FilePath (Join-Path $root 'test-results\setup-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Setup tests failed' }
Write-Output 'Built NationComPortSetup.exe (requires .NET Framework 4.5+; Windows x64; Internet for Microsoft Catalog driver).'
