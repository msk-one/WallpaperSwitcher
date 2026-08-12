# Wallpaper Switcher

Switches your desktop wallpaper between **Day** and **Night** image sets based on
your local time, from a folder you choose. Open source, no account, no server, no
telemetry — it only ever reads the folder you point it at.

Runs on Windows, macOS, and Linux. Built with .NET 9 and Avalonia.

[![CI](https://github.com/msk-one/WallpaperSwitcher/actions/workflows/ci.yml/badge.svg)](https://github.com/msk-one/WallpaperSwitcher/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

![The Wallpapers page on Windows](docs/screenshot-windows.png)

## What it does

- Point it at one folder. Subfolders are scanned too.
- Click a tile to tag each image **Day**, **Night**, or **Ignore**. Files with
  `day` or `night` in the name are tagged for you.
- Drag the bar to set when day and night begin. A night window that crosses
  midnight works.
- Shuffle within the current set **every hour**, **every 6 hours**, **each day**,
  or **each week**.
- Lives in the tray. Closing the window asks whether to keep it running or quit,
  and can remember your answer.
- Optionally starts when you sign in, and can start straight into the tray.

Changes are saved as you make them. The image chosen for a given period is
deterministic, so restarting the app does not reshuffle your wallpaper.

## Install

Download from the [latest release](https://github.com/msk-one/WallpaperSwitcher/releases/latest).

### Windows

| | |
|---|---|
| **Installer** (recommended) | `WallpaperSwitcher-<version>-win-x64-Setup.exe` |
| **Portable** | `WallpaperSwitcher-<version>-win-x64.zip` |

Windows 10 or 11, 64-bit. Nothing else to install — .NET is bundled.

The installer is per-user: it installs to `%LOCALAPPDATA%\Programs\WallpaperSwitcher`,
never prompts for administrator rights, and adds Start menu and Add/Remove
Programs entries. The portable zip is a single executable you can run from
anywhere, including a USB stick.

### macOS

macOS 11 or later. Download `WallpaperSwitcher-<version>-osx-arm64.dmg` (Apple
silicon) or `-osx-x64.dmg` (Intel) and drag the app to Applications, then clear
the quarantine flag once, because the app is not notarised:

```bash
xattr -dr com.apple.quarantine /Applications/WallpaperSwitcher.app
```

### Linux

```bash
tar -xzf WallpaperSwitcher-<version>-linux-x64.tar.gz
chmod +x WallpaperSwitcher
./WallpaperSwitcher
```

`linux-arm64` is published too. Works with GNOME, KDE Plasma, XFCE, and any
compositor with `swww` or `feh` available — the app tries each in turn.

### Why your OS warns about it

**Wallpaper Switcher is not code-signed**, so Windows shows "Windows protected
your PC" (**More info** → **Run anyway**) and macOS says the developer cannot be
verified (use the `xattr` command above). That is what every unsigned application
gets: a signing certificate costs several hundred dollars a year, which is hard
to justify for a free tool.

Every release is built by [GitHub Actions](.github/workflows/release.yml) from a
public tag, and SHA-256 checksums are attached to each release so you can verify
what you downloaded. If you would rather not trust the binaries,
[build from source](#build-from-source) — it is one command.

## Using it

1. On **Settings**, choose your wallpaper folder.
2. On **Wallpapers**, click any tile to cycle it **Day → Night → Ignore**.
   Anything left as Ignore is never used.
3. Back on **Settings**, drag the bar to set when day and night begin, and pick a
   **Shuffle** cadence. On Windows you can also set the **Fit** mode.

There is no Save button — every change is written immediately and takes effect at
once.

The tray menu has the things you want without opening the window: cycle to the
next wallpaper now, swap the day and night hours, change cadence, toggle start at
login, and open the log folder.

### File formats

| Format | Preview | Applies as wallpaper |
|---|---|---|
| `.jpg` `.jpeg` `.png` `.bmp` `.gif` | Yes | Yes |
| `.tif` `.tiff` | No | Yes |
| `.heic` `.heif` `.webp` | `.webp` only | Only with the matching Windows codec installed |

Verified on Windows 11. `.tif`/`.tiff` apply correctly but have no preview
thumbnail, because the renderer the app uses cannot decode them. Files that
cannot be used are skipped in favour of the next image and noted in the log,
rather than leaving you with a blank desktop.

### Where your data lives

| | |
|---|---|
| Settings | `%LOCALAPPDATA%\WallpaperSwitcher\settings.json` (Windows)<br>`~/.local/share/WallpaperSwitcher/settings.json` (macOS, Linux) |
| Logs | the `logs` folder beside it, 7 days retained |

Settings are plain JSON you can read and edit. Uninstalling leaves them in place
unless you ask for them to be removed.

## Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
git clone https://github.com/msk-one/WallpaperSwitcher.git
cd WallpaperSwitcher
dotnet build WallpaperSwitcher.sln -c Release
dotnet test WallpaperSwitcher.Tests/WallpaperSwitcher.Tests.csproj -c Release
dotnet run --project WallpaperSwitcher.Desktop
```

To build the release artifacts yourself:

```powershell
./scripts/publish-windows.ps1          # zip + Inno Setup installer
```

```bash
./scripts/package-macos-dmg.sh osx-arm64   # .app bundle + DMG, macOS only
./scripts/publish-linux.sh linux-x64       # tarball
```

Each target is built on its own operating system, which is also what
`.github/workflows/release.yml` does. A macOS build produced on a non-macOS host
will not run: arm64 macOS binaries need a code signature that `dotnet publish`
only applies on macOS.

## Project layout

| | |
|---|---|
| `WallpaperSwitcher.Core/` | Scheduling, settings, and image selection. No UI, no platform dependencies. |
| `WallpaperSwitcher.Desktop/` | The Avalonia app and the per-OS wallpaper adapters. |
| `WallpaperSwitcher.Tests/` | Unit tests for the core. |
| `docs/design-notes.md` | How scheduling works and why, plus known limitations. |

The UI is built in C# rather than XAML. See [CONTRIBUTING.md](CONTRIBUTING.md)
before sending a patch.

## License

[MIT](LICENSE).
