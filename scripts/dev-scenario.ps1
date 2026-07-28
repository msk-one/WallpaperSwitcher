#Requires -Version 5.1
<#
.SYNOPSIS
    Puts the app into a known state for manual UI testing, then launches it.

.DESCRIPTION
    Development helper. Writes settings.json, restarts the app, and reports what
    to look for. Not used by the build or the release.

.PARAMETER Scenario
    Running       a folder with 6 images, hero showing the current wallpaper
    Empty         no folder chosen -- hero collapsed, empty panel, first-run route
    Missing       folder points somewhere that does not exist -- red hero
    Large         520 images across subfolders, for grid virtualization
    Formats       one file per advertised extension
    Unicode       non-ASCII folder and file names
    Minimized     starts into the tray with no window

.EXAMPLE
    ./scripts/dev-scenario.ps1 Running
#>
[CmdletBinding()]
param(
    [ValidateSet('Running', 'Empty', 'Missing', 'Large', 'Formats', 'Unicode', 'Minimized')]
    [string]$Scenario = 'Running',

    [switch]$NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root 'WallpaperSwitcher.Desktop\bin\Release\net9.0\WallpaperSwitcher.exe'
$dataDir = Join-Path $env:LOCALAPPDATA 'WallpaperSwitcher'
$settingsPath = Join-Path $dataDir 'settings.json'

if (-not (Test-Path $exe)) {
    throw "No build at $exe. Run: dotnet build WallpaperSwitcher.sln -c Release"
}

Get-Process WallpaperSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
New-Item -ItemType Directory -Force $dataDir | Out-Null

$base = [ordered]@{
    SchemaVersion           = 2
    WallpaperDirectory      = 'C:\WPTest\Small'
    WallpaperFolderBookmark = $null
    DayStartsAt             = '06:00:00'
    NightStartsAt           = '18:00:00'
    ShuffleCadence          = 'Daily'
    WallpaperFit            = 'Fill'
    StartMinimized          = $false
    Assignments             = @(
        @{ Path = 'beach-day.jpg'; Category = 'Day' }
        @{ Path = 'forest-day.png'; Category = 'Day' }
        @{ Path = 'city-night.jpg'; Category = 'Night' }
        @{ Path = 'stars-night.png'; Category = 'Night' }
    )
}

$expect = ''
switch ($Scenario) {
    'Running' {
        $expect = 'Hero shows the current wallpaper, its set and the next change. 6 tiles, 2 day / 2 night / 2 ignored.'
    }
    'Empty' {
        $base.WallpaperDirectory = ''
        $base.Assignments = @()
        $expect = 'No hero at all. Centred "No images yet" panel with one accent button. Settings shows "No folder selected".'
    }
    'Missing' {
        $base.WallpaperDirectory = 'D:\Photos\ThisFolderIsGone'
        $expect = 'Red 4px bar down the left of the hero, warning tile, "Wallpaper unchanged", and a "Fix in Settings" button.'
    }
    'Large' {
        $base.WallpaperDirectory = 'C:\WPTest\Large'
        $base.Assignments = @()
        $expect = '520 images. Scroll the grid: it should stay smooth, and memory should not climb with every row you pass.'
    }
    'Formats' {
        $base.WallpaperDirectory = 'C:\WPTest\Formats'
        $base.Assignments = @()
        $expect = 'One tile per format. tif/tiff show the badge and filename over an empty thumbnail -- Skia cannot decode them.'
    }
    'Unicode' {
        $base.WallpaperDirectory = 'C:\WPTest\Tapety-Ą-Ł'
        $base.Assignments = @()
        $expect = 'Non-ASCII path renders correctly in Settings and the wallpaper still applies.'
    }
    'Minimized' {
        $base.StartMinimized = $true
        $expect = 'No window. Tray icon only. The schedule still arms -- check the tray menu and the status after reopening.'
    }
}

$base | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 $settingsPath
Write-Host "Scenario: $Scenario" -ForegroundColor Cyan
Write-Host "  folder : $($base.WallpaperDirectory)"
Write-Host "  expect : $expect" -ForegroundColor Yellow

if ($NoLaunch) {
    Write-Host 'Not launching (-NoLaunch).'
    return
}

# Start-Process rejects an empty ArgumentList, so branch rather than splat.
if ($Scenario -eq 'Minimized') {
    Start-Process $exe -ArgumentList '--minimized' | Out-Null
}
else {
    Start-Process $exe | Out-Null
}
Write-Host 'Launched.' -ForegroundColor Green
