#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes the Windows build and packages it as a zip and an installer.

.DESCRIPTION
    Mirrors what the release workflow does on windows-latest, so a local build
    and a released build are produced the same way.

    Produces:
      artifacts/publish/win-x64/WallpaperSwitcher.exe          self-contained, single file
      artifacts/zip/WallpaperSwitcher-<version>-win-x64.zip    portable
      artifacts/installer/WallpaperSwitcher-<version>-win-x64-Setup.exe

    The installer step is skipped with a warning if Inno Setup is not installed
    (winget install JRSoftware.InnoSetup).

.PARAMETER Version
    Version to stamp into the binary and the artifact names. Defaults to the
    VersionPrefix in Directory.Build.props.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = 'Release',
    [switch]$SkipInstaller
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'WallpaperSwitcher.Desktop\WallpaperSwitcher.Desktop.csproj'
$publishDir = Join-Path $root 'artifacts\publish\win-x64'
$zipDir = Join-Path $root 'artifacts\zip'
$installerDir = Join-Path $root 'artifacts\installer'

$env:AVALONIA_TELEMETRY_OPTOUT = '1'

if (-not $Version) {
    $props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
    if ($props -match '<VersionPrefix>([^<]+)</VersionPrefix>') {
        $Version = $Matches[1]
    }
    else {
        throw 'Could not read VersionPrefix from Directory.Build.props; pass -Version explicitly.'
    }
}

Write-Host "Building Wallpaper Switcher $Version (win-x64, $Configuration)" -ForegroundColor Cyan

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

# PublishReadyToRun is off deliberately: it roughly doubles the download for a
# startup saving that does not matter for an app that lives in the tray.
& dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=false `
    -p:DebugType=none `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $publishDir 'WallpaperSwitcher.exe'
if (-not (Test-Path $exe)) {
    throw "Publish did not produce $exe"
}

$exeInfo = Get-Item $exe
Write-Host ("  WallpaperSwitcher.exe  {0} MB" -f [math]::Round($exeInfo.Length / 1MB, 1))

# The Skia and HarfBuzz native libraries are embedded in the single file rather
# than sitting beside it, so a suspiciously small executable means the publish
# silently produced something that will not run on a machine without them.
if ($exeInfo.Length -lt 40MB) {
    throw "Executable is only $([math]::Round($exeInfo.Length / 1MB, 1)) MB; native libraries are probably not embedded."
}

# --- Portable zip ---------------------------------------------------------
New-Item -ItemType Directory -Force $zipDir | Out-Null
$zipPath = Join-Path $zipDir "WallpaperSwitcher-$Version-win-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $exe -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host ("  {0}  {1} MB" -f (Split-Path $zipPath -Leaf), [math]::Round((Get-Item $zipPath).Length / 1MB, 1))

# --- Installer ------------------------------------------------------------
if ($SkipInstaller) {
    Write-Host 'Skipping the installer (-SkipInstaller).' -ForegroundColor Yellow
    return
}

# winget installs Inno per-user by default, so %LOCALAPPDATA% is checked first.
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    # Under StrictMode, dereferencing .Source on a null result is an error.
    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) {
        $iscc = $command.Source
    }
}

if (-not $iscc) {
    Write-Warning 'Inno Setup not found; skipping the installer. Install it with: winget install JRSoftware.InnoSetup'
    return
}

New-Item -ItemType Directory -Force $installerDir | Out-Null
& $iscc "/DAppVersion=$Version" (Join-Path $root 'installer\WallpaperSwitcher.iss') | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
}

$setup = Join-Path $installerDir "WallpaperSwitcher-$Version-win-x64-Setup.exe"
Write-Host ("  {0}  {1} MB" -f (Split-Path $setup -Leaf), [math]::Round((Get-Item $setup).Length / 1MB, 1))
Write-Host 'Done.' -ForegroundColor Green
