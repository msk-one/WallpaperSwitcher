# Performance and Security Review

## Current Shape

- The desktop client is Avalonia-based and shares scheduling, settings, and selection logic through `WallpaperSwitcher.Core`.
- Wallpaper changes are local-only. The app has no network features and does not upload, sync, or fetch wallpaper data.
- macOS folder access uses Avalonia storage bookmarks so a user-selected wallpaper folder can be reopened after restart.
- Start-at-login is per-user only: Windows uses `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, macOS writes a user LaunchAgent, and Linux writes a user autostart `.desktop` file.

## Performance Notes

- Thumbnail rendering decodes images to a small fixed width instead of loading full-resolution wallpaper bitmaps into the UI list.
- Thumbnails are cached for the process lifetime to avoid decoding the same image repeatedly while scrolling.
- The scheduler uses a single timer and wakes only on the next phase or shuffle boundary.
- Wallpaper folder scanning is recursive and synchronous once a folder is selected or refreshed. For very large photo libraries, the next improvement should be an async/incremental scanner with cancellation and progress.
- The current thumbnail cache is unbounded. This is fine for typical wallpaper folders, but a future LRU cap would be better for folders with thousands of images.

## Security Notes

- The app stores local file paths, schedule settings, wallpaper assignments, and optionally a macOS bookmark in a JSON settings file. It does not store secrets.
- Platform wallpaper commands use fixed executable names and argument lists. Wallpaper paths are passed as arguments, not interpolated into a shell command.
- macOS wallpaper changes use AppleScript through `/usr/bin/osascript`; this may require macOS privacy approval for automation/system events.
- Linux wallpaper support intentionally tries known desktop tools only: GNOME `gsettings`, KDE Plasma, XFCE, `swww`, and `feh`.
- Start-at-login writes only to the current user's startup location. It does not install privileged agents, services, daemons, or system-wide files.

## Remaining Release Risks

- macOS distribution should be signed and notarized before broad release. The current DMG is suitable for testing but may still show Gatekeeper warnings.
- Linux desktop behavior varies by window manager and desktop environment. The supported command fallbacks should be documented in release notes.
- HEIC/HEIF thumbnail decoding depends on platform codec support. Unsupported formats remain listable, but their previews may be blank.
- The app should get a manual smoke test on macOS and at least one GNOME/KDE Linux machine before tagging a release.

## Push Checklist

- Build the desktop app in Release mode.
- Run the unit test suite.
- Package macOS as DMG for `osx-arm64` and `osx-x64`.
- Confirm the main window hides to tray/menu bar on close and quits from the tray menu.
- Confirm folder restore works after app restart on macOS.
- Confirm launch-at-login toggle creates and removes the expected per-user startup entry.
