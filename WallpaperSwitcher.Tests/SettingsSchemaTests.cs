namespace WallpaperSwitcher.Tests;

[TestClass]
public sealed class SettingsSchemaTests
{
    /// <summary>
    /// The reason assignments are stored relative: with absolute paths, renaming
    /// or moving the wallpaper folder made every stored assignment stop matching,
    /// so the user's Day/Night choices were silently replaced by filename guesses.
    /// </summary>
    [TestMethod]
    public void AssignmentsSurviveRenamingTheWallpaperFolder()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);

        // The user tagged an image while the folder was called "Before".
        store.TrySave(
            new AppSettings
            {
                WallpaperDirectory = @"C:\Wallpapers\Before",
                Assignments =
                [
                    new WallpaperAssignment { Path = @"C:\Wallpapers\Before\sub\a.jpg", Category = WallpaperCategory.Night }
                ]
            },
            out _);

        var saved = store.Load().Assignments.ToDictionary(
            assignment => assignment.Path,
            assignment => assignment.Category,
            StringComparer.OrdinalIgnoreCase);

        // The folder is renamed and rescanned. Its files now have new absolute
        // paths, but the same paths relative to the folder.
        var category = WallpaperSelectionService.ResolveCategory(
            saved,
            @"C:\Wallpapers\After",
            @"C:\Wallpapers\After\sub\a.jpg");

        Assert.AreEqual(WallpaperCategory.Night, category, "the assignment should survive the rename");
    }

    [TestMethod]
    public void LegacyAbsoluteAssignmentsStillMatchWhenTheFolderHasNotMoved()
    {
        var saved = new Dictionary<string, WallpaperCategory>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\Wallpapers\a.jpg"] = WallpaperCategory.Day
        };

        var category = WallpaperSelectionService.ResolveCategory(saved, @"C:\Wallpapers", @"C:\Wallpapers\a.jpg");

        Assert.AreEqual(WallpaperCategory.Day, category);
    }

    [TestMethod]
    public void UnknownFileFallsBackToNameInference()
    {
        var saved = new Dictionary<string, WallpaperCategory>(StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual(
            WallpaperCategory.Night,
            WallpaperSelectionService.ResolveCategory(saved, @"C:\Wallpapers", @"C:\Wallpapers\city-night.jpg"));
        Assert.AreEqual(
            WallpaperCategory.Ignore,
            WallpaperSelectionService.ResolveCategory(saved, @"C:\Wallpapers", @"C:\Wallpapers\untagged.jpg"));
    }

    [TestMethod]
    public void AssignmentsArePersistedRelativeToTheWallpaperFolder()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);

        store.TrySave(
            new AppSettings
            {
                WallpaperDirectory = @"C:\Wallpapers",
                Assignments =
                [
                    new WallpaperAssignment { Path = @"C:\Wallpapers\a.jpg", Category = WallpaperCategory.Day }
                ]
            },
            out _);

        var json = File.ReadAllText(store.SettingsPath);

        StringAssert.Contains(json, "a.jpg");
        Assert.IsFalse(json.Contains(@"C:\\Wallpapers\\a.jpg"), "the assignment path should be relative on disk");
    }

    [TestMethod]
    public void PathsOutsideTheWallpaperFolderStayAbsolute()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);

        store.TrySave(
            new AppSettings
            {
                WallpaperDirectory = @"C:\Wallpapers",
                Assignments =
                [
                    new WallpaperAssignment { Path = @"C:\Elsewhere\b.jpg", Category = WallpaperCategory.Day }
                ]
            },
            out _);

        Assert.AreEqual(@"C:\Elsewhere\b.jpg", store.Load().Assignments[0].Path);
    }

    [TestMethod]
    public void IgnoreAssignmentsAreNotPersisted()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);

        store.TrySave(
            new AppSettings
            {
                WallpaperDirectory = @"C:\Wallpapers",
                Assignments =
                [
                    new WallpaperAssignment { Path = @"C:\Wallpapers\keep.jpg", Category = WallpaperCategory.Day },
                    new WallpaperAssignment { Path = @"C:\Wallpapers\drop.jpg", Category = WallpaperCategory.Ignore }
                ]
            },
            out _);

        var loaded = store.Load();

        Assert.AreEqual(1, loaded.Assignments.Count);
        Assert.AreEqual("keep.jpg", loaded.Assignments[0].Path);
    }

    [TestMethod]
    public void EnumsArePersistedByName()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);

        store.TrySave(new AppSettings { ShuffleCadence = ShuffleCadence.Weekly, WallpaperFit = WallpaperFit.Span }, out _);
        var json = File.ReadAllText(store.SettingsPath);

        StringAssert.Contains(json, "\"Weekly\"");
        StringAssert.Contains(json, "\"Span\"");
    }

    /// <summary>
    /// Settings files written before schema 2 stored absolute paths and numeric
    /// enums. Those must keep loading — Linux and macOS users already have them.
    /// </summary>
    [TestMethod]
    public void LegacyFileWithAbsolutePathsAndNumericEnumsStillLoads()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);

        File.WriteAllText(store.SettingsPath, """
            {
              "WallpaperDirectory": "C:\\Wallpapers",
              "DayStartsAt": "06:00:00",
              "NightStartsAt": "18:00:00",
              "ShuffleCadence": 3,
              "Assignments": [
                { "Path": "C:\\Wallpapers\\legacy.jpg", "Category": 2 }
              ]
            }
            """);

        var loaded = store.Load(out var status);

        Assert.AreEqual(SettingsLoadStatus.Loaded, status);
        Assert.AreEqual(ShuffleCadence.Weekly, loaded.ShuffleCadence);
        Assert.AreEqual(WallpaperFit.Fill, loaded.WallpaperFit, "a value absent from an old file should fall back to the default");
        Assert.AreEqual(1, loaded.Assignments.Count);
        Assert.AreEqual(@"C:\Wallpapers\legacy.jpg", loaded.Assignments[0].Path);
        Assert.AreEqual(WallpaperCategory.Night, loaded.Assignments[0].Category);
    }
}
