# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

The release workflow reads the section matching the tag and uses it as the
GitHub release notes, so the heading format matters.

## [Unreleased]

## [0.7.0] - 2026-08-12

First public release. Windows, macOS, and Linux.

It is numbered 0.7.0 rather than 1.0.0 deliberately: everything below has been
exercised on Windows 11 and on macOS and Linux, but the app has not yet been used
by anyone outside the people who built it. 1.0.0 comes after that.

### Added

- Day/Night wallpaper switching from a single folder, with subfolder scanning and
  automatic tagging of files with `day` or `night` in the name.
- Shuffle cadences: every hour, every 6 hours, each day, each week. The image
  chosen for a period is deterministic, so restarting does not reshuffle it.
- A two-pane window — Wallpapers and Settings — built against the Fluent design
  system, following the operating system's light or dark theme.
- Images are shown as a grid of tiles. Clicking one cycles it Day → Night →
  Ignore; every tile is reachable by keyboard and announced to screen readers.
- A 24-hour bar for setting when day and night begin, with 15-minute snapping, a
  one-hour minimum gap, and full keyboard control.
- Changes are saved as they are made. There is no Save button.
- Closing the window asks whether to keep running in the tray or quit, with an
  option to remember the answer. Settings can change that answer later.
- Tray icon with cycle-now, swap day/night hours, cadence selection, start at
  login, and open log folder.
- Start minimized directly into the tray, via a setting or the `--minimized`
  argument.
- Wallpaper fit on Windows: Fill, Fit, Stretch, Center, Tile, or Span.
- Per-user Windows installer and a portable zip, plus macOS DMGs and Linux
  tarballs, all built and published by GitHub Actions from a public tag with
  SHA-256 checksums.
- Logging to the per-user app data folder, retained for 7 days.
- Single-instance guard on Windows, so launching again surfaces the running
  window instead of starting a competing scheduler.

### Fixed

Everything below was found while preparing this release and never shipped, but is
listed because the pre-release builds circulated.

- The wallpaper schedule only armed when a window was shown, which made a
  start-minimized mode impossible.
- Choosing a wallpaper folder did not save it or arm the schedule unless some
  other setting happened to change, so the choice was lost on the next launch.
- The Windows "Start at login" entry was written with escaping meant for Linux
  desktop files, so it never matched when read back and the checkbox always
  appeared unchecked. A stale entry pointing at a moved executable is now
  repointed rather than reported as off.
- Day/Night assignments were lost when the wallpaper folder was renamed or moved,
  because they were stored as absolute paths.
- An interrupted settings write could truncate the file, and the unreadable
  result was silently replaced by defaults. Writes are now atomic and a corrupt
  file is set aside with a message.
- An image the system could not use was retried every minute for the rest of the
  cycle instead of falling through to another one. Windows reports success for a
  corrupt or empty file and then paints the desktop black, so images are now
  checked before use.
- Directory junctions and symlinks were followed during the folder scan, so a
  self-referential junction sent it into a loop.
- The wallpaper was not reclaimed after sleep, a clock change, or another app
  taking it over, until the next schedule boundary.
- Changing the shuffle cadence also changed the wallpaper on the spot.
- The tray menu did not reflect cadence or start-at-login changes made in the
  window on Windows.
- Opening the app from the tray left the window behind other windows.
- A thumbnail that failed to decode could take down the UI thread.
- Status messages mentioning macOS were shown on Windows and Linux.
- The executable had no icon and the application manifest declared no DPI
  awareness, supported OS versions, or long-path support.

### Performance

- Thumbnails decode off the UI thread. They previously ran synchronously while
  the list was being built, so every image on screen blocked it. Measured with
  6–9 MB wallpapers, the window now appears in 1.8s instead of 5.2s.
- Applying the wallpaper runs off the UI thread. On Windows it ends in a
  `SystemParametersInfo` call that broadcasts to every top-level window and
  blocks until they answer, which is what made the tray menu unresponsive.
- Large folders no longer materialise every row up front, and the preview cache
  is bounded. A 520-image folder loads in under two seconds.

[Unreleased]: https://github.com/msk-one/WallpaperSwitcher/compare/v0.7.0...HEAD
[0.7.0]: https://github.com/msk-one/WallpaperSwitcher/releases/tag/v0.7.0
