# Design notes

How the scheduler works, and the limitations that are deliberate.

## The logical day

Everything is anchored to the **day start**, not to midnight. If day starts at
06:00, then 01:15 belongs to the *previous* logical day. Without this, a night
window that crosses midnight would change wallpaper at midnight for no reason the
user asked for.

`WallpaperScheduleCalculator.GetLogicalDayAnchor` does this, and it is why
`BuildCycleKey` produces the same key at 23:00 and 01:00 on a daily cadence.

## Choosing an image

Each period gets a **cycle key**, for example `Night:20260724:h04`. The key is
hashed and the hash selects an index into the sorted list of candidates.

This is deterministic on purpose: restarting the app, or resuming from sleep,
recomputes the same key and therefore the same image. An earlier design that
picked randomly reshuffled the wallpaper on every launch, which reads as a bug.

There is one adjustment: if a *new* cycle would select the image that is already
showing, the index advances by one, so a new period always looks like something
happened.

## Waking up at the right time

Two timers:

- A **one-shot** timer set to the next boundary, which is the earlier of the next
  day/night phase change and the next shuffle boundary. Daily and weekly cadences
  have no separate shuffle boundary, because their rollover coincides with a
  phase change.
- A **one-minute watchdog**, because a one-shot timer measures elapsed time and
  does not fire while the machine is asleep. Without it, sleeping through a
  boundary would leave the wrong wallpaper until the next one.

On Windows there are also `PowerModeChanged`, `TimeChanged`, and
`DisplaySettingsChanged` hooks. The watchdog alone is not enough for these,
because it asks for a non-forced apply, which short-circuits when the schedule
has not changed. If something *else* took over the wallpaper — Windows Spotlight,
a theme, a display driver reset — only a forced apply reclaims it.

## Daylight saving time

**Known and accepted.** In the autumn transition the repeated hour produces the
same hourly cycle key twice, so the wallpaper holds for two hours. In spring one
hourly cycle is skipped.

This is not worth fixing. Doing so means anchoring the calculator to UTC, which
contradicts the entire premise of local-time day and night windows, and would
invalidate the scheduling tests. For a wallpaper app, one hour of drift twice a
year is cosmetic.

## Deciding whether an image is usable

`SystemParametersInfo` on Windows returns success for an empty or corrupt file
and then paints the desktop black. Measured, not assumed — a zero-byte `.png`
and 500 bytes of random data both returned `TRUE`.

So the return value cannot be trusted, and `ImageFileProbe` checks the file's
leading bytes before the wallpaper is applied. A file that fails is skipped in
favour of the next candidate and remembered for the rest of the session, because
the cycle key is deterministic and would otherwise select the same unusable file
every time the watchdog ran.

The probe only rejects files that are definitely not images. Whether the OS can
decode a well-formed HEIC or TIFF still depends on installed codecs.

## Assignment paths

Stored **relative to the wallpaper folder**. With absolute paths, renaming or
moving the folder made every assignment stop matching, and the user's Day/Night
choices silently reverted to filename guesses.

Absolute paths are still read, so settings files written by earlier versions keep
working, and images outside the folder stay absolute so they remain meaningful.

## Theming and icons

Every colour resolves through a theme resource, so light and dark follow the OS
with no code involved. `ThemePalette` and the `ActualThemeVariantChanged` handler
that rebuilt the entire visual tree on a theme switch are both gone.

The design is specified against WinUI's semantic tokens
(`CardBackgroundFillColorDefault`, `TextFillColorSecondary`, and so on).
Avalonia's Fluent theme does not define those — it ships per-control keys instead,
verified by enumerating every key the theme exposes in both 11.3 and 12.0. The
names come from WinUI, which FluentAvalonia repackages, but FluentAvalonia is
net10.0-only at 3.x and Avalonia-11-only at the net9.0-compatible 2.4.1. So
`Theming/FluentTokens.cs` defines that layer directly, using WinUI's published
values, as a theme dictionary keyed by variant.

Icons are vector geometry in `Theming/Icons.cs`, not Segoe Fluent Icons glyphs.
That font ships only with Windows, so the glyphs the mockup uses would render as
empty boxes on macOS and Linux.

## Accessibility

Nothing interactive is a `Border` with pointer handlers. Tiles, the schedule
handles, and every button are real controls, so they take focus, activate on
Space and Enter, and keep the Fluent focus adorner.

Verified through the UI Automation tree on Windows: the nav exposes two
`TabItem`s, each tile is a keyboard-focusable `Button` named
"beach-day.jpg, currently day. Activate to change.", and the schedule handles are
`Thumb`s named "Day starts" and "Night starts" carrying their value as help text.
The status line is a polite live region. Colour is never the only signal — each
tile badge carries the word Day, Night or Ignore.

The schedule bar has to be fully keyboard operable because it replaced the time
text boxes and there is no typed entry left: arrows move 15 minutes, Page keys an
hour, and Home/End run to the limit the 60-minute minimum gap allows.

## Platform adapters

| | |
|---|---|
| Windows | `SystemParametersInfo(SPI_SETDESKWALLPAPER)`, plus `HKCU\Control Panel\Desktop` for the fit mode, which the API does not carry |
| macOS | `osascript` telling System Events to set every desktop's picture |
| Linux | `gsettings`, then `plasma-apply-wallpaperimage`, then XFCE's `xfconf-query`, then `swww`, then `feh` — first one that works wins |

macOS also needs a security-scoped bookmark to reopen the wallpaper folder after
a restart, which is why the folder picker there goes through Avalonia's storage
provider rather than `Directory.GetFiles`. Windows and Linux use the direct
filesystem scan, which is an order of magnitude faster.

## Known limitations

- **One wallpaper across all monitors.** Per-monitor images need the
  `IDesktopWallpaper` COM interface. Multi-monitor behaviour has not been tested.
- **No macOS notarisation.** Builds are ad-hoc signed, which is enough to launch
  but still shows a Gatekeeper warning.
- **Windows only for the fit setting.** macOS and Linux desktops manage scaling
  themselves.
- **Ignored tiles are dimmed but not desaturated.** The design asks for grayscale
  plus 50% opacity on the image layer. Avalonia ships blur and drop-shadow
  effects but no colour-matrix, so there is no way to desaturate a bitmap without
  a custom shader or re-decoding every thumbnail. Opacity is applied; the badge
  carries the word "Ignore" either way, so nothing depends on the difference.
