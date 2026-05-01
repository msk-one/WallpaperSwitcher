# Wallpaper Switcher

Small native Windows utility for switching the desktop wallpaper between Day and Night image sets based on local PC time.

## Tech stack

- .NET 9
- WPF
- Windows desktop API via `SystemParametersInfo`

This is the best fit here because the app is Windows-only, needs a tiny native UI, and only has to run a lightweight background timer plus a tray icon. WPF keeps that simple and stable without adding the packaging overhead of a heavier app model.

## What it does

- Lets you choose one wallpaper folder
- Lets you tag each image as `Day`, `Night`, or `Ignore`
- Uses `06:00` for day start and `18:00` for night start by default
- Supports shuffle cadence: every hour, every 6 hours, each day, or each week
- Minimizes to the tray so the schedule keeps running
- Saves settings to `%LocalAppData%\WallpaperSwitcher\settings.json`

## Run

```powershell
dotnet run
```

Published output:

`bin\Release\net9.0-windows\win-x64\publish\WallpaperSwitcher.exe`

The publish folder currently contains a self-contained `win-x64` build.
