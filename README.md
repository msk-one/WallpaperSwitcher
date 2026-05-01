# WallpaperSwitcher

Native Windows wallpaper scheduler built with `.NET 9` and `WPF`.

The app switches the desktop wallpaper between `Day` and `Night` image sets based on local PC time, lets you choose the source folder, and supports shuffle cadences of hourly, every 6 hours, daily, and weekly.

## Project layout

- `WallpaperSwitcher/`
  The desktop application
- `WallpaperSwitcher.Tests/`
  Unit tests for schedule calculation and boundary behavior
- `WallpaperSwitcher.sln`
  Solution file for the app and tests

## Why this stack

- Windows-native UI with low runtime overhead
- Direct access to the Windows wallpaper API
- Simple deployment as a self-contained `win-x64` executable

## Build

```powershell
dotnet build WallpaperSwitcher.sln -c Release
```

## Test

```powershell
dotnet test .\WallpaperSwitcher.Tests\WallpaperSwitcher.Tests.csproj -c Release
```

## Publish

```powershell
dotnet publish .\WallpaperSwitcher\WallpaperSwitcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Published executable output:

`WallpaperSwitcher\bin\Release\net9.0-windows\win-x64\publish\WallpaperSwitcher.exe`
