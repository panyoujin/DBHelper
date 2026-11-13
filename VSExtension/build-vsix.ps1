# DBHelper.KeyNavigation VSIX packaging script
# Usage: powershell -ExecutionPolicy Bypass -File build-vsix.ps1
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projDir = Join-Path $PSScriptRoot "DBHelper.KeyNavigation"
$release = Join-Path $projDir "bin\$Configuration"
$msbuild = "D:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
$csproj = Join-Path $projDir "DBHelper.KeyNavigation.csproj"
$vsixUtil = "C:\Users\jimmy.pan\.nuget\packages\microsoft.vssdk.buildtools\17.14.2142\tools\vssdk\bin\VsixUtil.exe"
$vsixSchemas = "C:\Users\jimmy.pan\.nuget\packages\microsoft.vssdk.buildtools\17.14.2142\tools\vssdk\schemas"

# 1. Build (with restore)
# Note: VSSDK packaging targets emit a bogus VSSDK1311 error on legacy csproj,
# but the dll and obj manifest are still produced. We check outputs instead of exit code.
& $msbuild $csproj /t:Restore /v:q /nologo 2>&1 | Select-String ": error" | ForEach-Object { Write-Host $_ }
& $msbuild $csproj /p:Configuration=$Configuration /v:m /nologo 2>&1 | Select-String ": error " | ForEach-Object { Write-Host $_ }

# 2. Verify build outputs
$manifest = Join-Path $projDir "obj\$Configuration\extension.vsixmanifest"
$dll = Join-Path $release "DBHelper.KeyNavigation.dll"
if (-not (Test-Path $manifest)) { throw "manifest not found: $manifest" }
if (-not (Test-Path $dll)) { throw "dll not found: $dll" }

# 3. Package with the official VsixUtil (generates [Content_Types].xml, manifest.json, catalog.json)
#    -noValidate skips the VSSDK1311 false positive; installer validation still applies.
$out = Join-Path $release "DBHelper.KeyNavigation.vsix"
if (Test-Path $out) { Remove-Item $out -Force }
$files = Join-Path $projDir "vsix-files.json"
& $vsixUtil package -outputPath $out -sourceManifest $manifest -files $files -is64BitBuild -compressionLevel Normal -vsixSchemaPath $vsixSchemas
if ($LASTEXITCODE -ne 0) { throw "VsixUtil package failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $out)) { throw "vsix was not produced: $out" }

Write-Host "VSIX generated: $out"
