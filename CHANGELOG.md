# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

The release workflow reads the section matching the tag and uses it as the
GitHub release notes, so the heading format matters.

## [Unreleased]

## [1.0.0] - 2026-07-24

First public release. Windows, macOS, and Linux.

### Added

- Day/Night wallpaper switching from a single folder, with subfolder scanning and
  automatic tagging of files with `day` or `night` in the name.
- Shuffle cadences: every hour, every 6 hours, each day, each week. The image
  chosen for a period is deterministic, so restarting does not reshuffle it.
- Tray icon with cycle-now, swap day/night hours, cadence selection, start at
  login, and open log folder.
- Start minimized directly into the tray, via a setting or the `--minimized`
  argument.
- Wallpaper fit on Windows: Fill, Fit, Stretch, Center, Tile, or Span.
- Per-user Windows installer and a portable zip, plus macOS DMGs and Linux
  tarballs, all built and published by GitHub Actions.
- Logging to the per-user app data folder, retained for 7 days.
- Single-instance guard on Windows, so launching again surfaces the running
  window instead of starting a competing scheduler.

### Fixed

Everything below was found while preparing this release and never shipped, but is
listed because the pre-release builds circulated.

- The wallpaper schedule only armed when a window was shown, which made a
  start-minimized mode impossible.
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
- Saving was refused when no images were loaded, which silently discarded tray
  cadence and day/night changes made before a folder was chosen.
- Directory junctions and symlinks were followed during the folder scan, so a
  self-referential junction sent it into a loop.
- Large folders materialised every row and decoded every thumbnail up front, and
  the cache was never bounded or freed. A 520-image folder now loads in under two
  seconds.
- The wallpaper was not reclaimed after sleep, a clock change, or another app
  taking it over, until the next schedule boundary.
- A thumbnail that failed to decode could take down the UI thread.
- Status messages mentioning macOS were shown on Windows and Linux.
- The executable had no icon and the application manifest declared no DPI
  awareness, supported OS versions, or long-path support.

[Unreleased]: https://github.com/msk-one/WallpaperSwitcher/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/msk-one/WallpaperSwitcher/releases/tag/v1.0.0
