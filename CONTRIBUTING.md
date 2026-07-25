# Contributing

Thanks for taking a look. Bug reports and patches are both welcome.

## Getting set up

You need the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
`global.json` pins the feature band so your build matches CI.

```bash
dotnet build WallpaperSwitcher.sln -c Release
dotnet test WallpaperSwitcher.Tests/WallpaperSwitcher.Tests.csproj -c Release
dotnet run --project WallpaperSwitcher.Desktop
```

Please run the tests before opening a pull request. CI runs them on Windows,
Linux, and macOS.

## Things worth knowing before you start

**The UI is written in C#, not XAML.** There are no `.axaml` files.
`WallpaperSwitcher.Desktop/MainWindow.cs` builds the whole visual tree
imperatively, with colours coming from `ThemePalette`. This surprises everyone at
least once. If you add a control, take its colours from the palette so light and
dark themes both work.

**Core has no dependencies and no UI.** `WallpaperSwitcher.Core` is where the
scheduling, settings, and image selection live, and it is the only project with
test coverage. Logic that can go there should, because that is what can be
tested.

**The app must work with no window open.** It lives in the tray, and the
scheduler is owned by `App`, not by `MainWindow`. Do not move startup work into
window events.

**Three platforms, one adapter.** `PlatformWallpaperService` branches on the OS.
If you change shared code, say so in the pull request so the other platforms get
retested — the maintainer may not have all three to hand.

## Reporting a bug

Include your OS and version, the app version from the status bar, and the
relevant part of the log. **Open log folder** in the tray menu takes you straight
there; logs live in the per-user app data folder and are kept for 7 days.

## Style

Match the surrounding code. A few conventions that are already consistent:

- Comments explain *why*, not *what*. If a line is defensive, say what it is
  defending against.
- Catch specific exception types. Where a broad catch is genuinely needed, add a
  comment saying why.
- Prefer a clear status message to a silent failure. This app is often running
  with no window visible, so anything that fails quietly is invisible.

## License

Contributions are accepted under the [MIT License](LICENSE).
