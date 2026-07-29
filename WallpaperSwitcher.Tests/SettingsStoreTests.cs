namespace WallpaperSwitcher.Tests;

[TestClass]
public sealed class SettingsStoreTests
{
    private static SettingsStore CreateStoreIn(string directory)
    {
        return new SettingsStore(directory);
    }

    [TestMethod]
    public void MissingFileReportsNotFoundAndReturnsDefaults()
    {
        using var folder = new TempFolder();
        var store = CreateStoreIn(folder.Path);

        var settings = store.Load(out var status);

        Assert.AreEqual(SettingsLoadStatus.NotFound, status);
        Assert.AreEqual(ShuffleCadence.Daily, settings.ShuffleCadence);
        Assert.AreEqual(WallpaperFit.Fill, settings.WallpaperFit);
        Assert.IsFalse(settings.StartMinimized);
    }

    [TestMethod]
    public void SaveThenLoadRoundTripsEverySetting()
    {
        using var folder = new TempFolder();
        var store = CreateStoreIn(folder.Path);

        // Non-ASCII in both the folder and the filename. One image inside the
        // wallpaper folder, which is stored relative, and one outside it, which
        // stays absolute.
        var wallpapers = TestPaths.Rooted("Wallpapers", "Zdjęcia");
        var inside = Path.Combine(wallpapers, "nocą.jpg");
        var outside = Path.Combine(TestPaths.Rooted("Inne"), "dzień.jpg");

        var saved = new AppSettings
        {
            WallpaperDirectory = wallpapers,
            DayStartsAt = TimeSpan.FromHours(7),
            NightStartsAt = TimeSpan.FromHours(21),
            ShuffleCadence = ShuffleCadence.SixHours,
            WallpaperFit = WallpaperFit.Span,
            StartMinimized = true,
            CloseAction = WindowCloseAction.Quit,
            Assignments =
            [
                new WallpaperAssignment { Path = inside, Category = WallpaperCategory.Night },
                new WallpaperAssignment { Path = outside, Category = WallpaperCategory.Day }
            ]
        };

        Assert.IsTrue(store.TrySave(saved, out var error), error);

        var loaded = store.Load(out var status);

        Assert.AreEqual(SettingsLoadStatus.Loaded, status);
        Assert.AreEqual(saved.WallpaperDirectory, loaded.WallpaperDirectory);
        Assert.AreEqual(TimeSpan.FromHours(7), loaded.DayStartsAt);
        Assert.AreEqual(TimeSpan.FromHours(21), loaded.NightStartsAt);
        Assert.AreEqual(ShuffleCadence.SixHours, loaded.ShuffleCadence);
        Assert.AreEqual(WallpaperFit.Span, loaded.WallpaperFit);
        Assert.IsTrue(loaded.StartMinimized);
        Assert.AreEqual(WindowCloseAction.Quit, loaded.CloseAction, "the answer to the close prompt must survive a restart");

        Assert.AreEqual(2, loaded.Assignments.Count);
        Assert.AreEqual("nocą.jpg", loaded.Assignments[0].Path, "an image inside the folder is stored relative");
        Assert.AreEqual(WallpaperCategory.Night, loaded.Assignments[0].Category);
        Assert.AreEqual(outside, loaded.Assignments[1].Path, "an image outside the folder stays absolute");
        Assert.AreEqual(WallpaperCategory.Day, loaded.Assignments[1].Category);
    }

    [TestMethod]
    public void CorruptFileIsQuarantinedRatherThanSilentlyDiscarded()
    {
        using var folder = new TempFolder();
        var store = CreateStoreIn(folder.Path);

        Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
        File.WriteAllText(store.SettingsPath, "{ \"WallpaperDirectory\": \"C:\\\\Wall");

        var settings = store.Load(out var status);

        Assert.AreEqual(SettingsLoadStatus.Corrupt, status);
        Assert.AreEqual(string.Empty, settings.WallpaperDirectory);
        Assert.IsFalse(File.Exists(store.SettingsPath), "the bad file should have been moved aside");

        var quarantined = Directory.GetFiles(
            Path.GetDirectoryName(store.SettingsPath)!,
            "settings.corrupt-*.json");
        Assert.AreEqual(1, quarantined.Length, "the original content should still be recoverable");
    }

    [TestMethod]
    public void SaveDoesNotLeaveATemporaryFileBehind()
    {
        using var folder = new TempFolder();
        var store = CreateStoreIn(folder.Path);

        Assert.IsTrue(store.TrySave(new AppSettings(), out _));

        Assert.IsFalse(File.Exists(store.SettingsPath + ".tmp"));
    }

    [TestMethod]
    public void SaveOverAnExistingFileReplacesItAtomically()
    {
        using var folder = new TempFolder();
        var store = CreateStoreIn(folder.Path);

        Assert.IsTrue(store.TrySave(new AppSettings { WallpaperDirectory = "first" }, out _));
        Assert.IsTrue(store.TrySave(new AppSettings { WallpaperDirectory = "second" }, out _));

        Assert.AreEqual("second", store.Load().WallpaperDirectory);
        Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(store.SettingsPath)!).Length);
    }
}
