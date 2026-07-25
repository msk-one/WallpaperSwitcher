# WallpaperSwitcher

Multiplatform wallpaper scheduler built with `.NET 9` and Avalonia.

The app switches the desktop wallpaper between `Day` and `Night` image sets based on local time, lets you choose the source folder, scans subfolders, and supports shuffle cadences of hourly, every 6 hours, daily, and weekly.

## Project layout

- `WallpaperSwitcher.Core/`
  Portable scheduling, settings, and wallpaper selection code
- `WallpaperSwitcher.Desktop/`
  Avalonia desktop application for Windows, macOS, and Linux
- `WallpaperSwitcher.Tests/`
  Portable unit tests for schedule calculation and boundary behavior
- `WallpaperSwitcher.sln`
  Solution file for the multiplatform app, core, and tests

## Why this stack

- Avalonia gives one native-feeling desktop shell across Windows, macOS, and Linux
- The core logic is platform-neutral and testable on every target
- Wallpaper application is handled through small OS-specific adapters:
  Windows `user32.dll`, macOS `osascript`, and common Linux desktop tools

## Build

```bash
dotnet build WallpaperSwitcher.Desktop/WallpaperSwitcher.Desktop.csproj -c Release
```

## Test

```bash
dotnet test WallpaperSwitcher.Tests/WallpaperSwitcher.Tests.csproj -c Release
```

## Publish

Publish a single target:

```bash
dotnet publish WallpaperSwitcher.Desktop/WallpaperSwitcher.Desktop.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Package a macOS DMG:

```bash
./scripts/package-macos-dmg.sh osx-arm64
```

The DMG is written to `artifacts/dmg/WallpaperSwitcher-osx-arm64.dmg`.

Publish all release targets and macOS DMGs:

```bash
./scripts/publish-all.sh
```

Default targets:

- `win-x64`
- `osx-arm64`
- `osx-x64`
- `linux-x64`
- `linux-arm64`

Artifacts are written under `artifacts/publish/<runtime>/`.

macOS app bundles are written under `artifacts/macos/<runtime>/`, and DMGs are written under `artifacts/dmg/`.

## macOS folder access

Use the in-app `Browse` button to grant the app access to the wallpaper folder. On macOS the app saves an Avalonia storage bookmark for that folder and reloads it on launch, which avoids relying only on a raw path under newer macOS privacy rules. The scanner includes nested folders and supports `.jpg`, `.jpeg`, `.png`, `.bmp`, `.heic`, `.heif`, `.webp`, `.tif`, and `.tiff`.
