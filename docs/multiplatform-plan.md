# Multiplatform Shipping Plan

## Current State

- The previous Windows WPF app is still available in `WallpaperSwitcher/` for reference.
- The shipping app is now `WallpaperSwitcher.Desktop`, an Avalonia desktop app targeting `net9.0`.
- Shared behavior lives in `WallpaperSwitcher.Core` so scheduling, settings, and wallpaper selection can be tested on any OS.
- Portable tests run from `WallpaperSwitcher.Tests`.
- macOS packaging creates a real `.app` bundle and `.dmg`, rather than shipping loose executables.
- macOS folder picking uses Avalonia storage bookmarks so user-granted folder access survives relaunch.
- macOS bundles include a generated `WC` icon.

## Platform Support

- Windows: uses `SystemParametersInfoW` from `user32.dll`.
- macOS: uses `/usr/bin/osascript` to set every desktop picture.
- Linux: tries `gsettings`, `plasma-apply-wallpaperimage`, `xfconf-query`, `swww`, then `feh`.

## Release Targets

- `win-x64`
- `osx-arm64`
- `osx-x64`
- `linux-x64`
- `linux-arm64`

macOS DMGs are created with `scripts/package-macos-dmg.sh`.

## Next Hardening Pass

- Add macOS signing/notarization.
- Add Linux `.deb`, `.rpm`, and AppImage packaging.
- Add Windows installer packaging and startup-at-login integration.
- Add tray/menu-bar behavior for Avalonia across all platforms.
- Add integration tests with fake platform wallpaper commands.
