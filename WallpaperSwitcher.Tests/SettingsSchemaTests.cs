namespace WallpaperSwitcher.Tests;

[TestClass]
public sealed class SettingsSchemaTests
{
    /// <summary>
    /// Guards the assumption every test below rests on.
    /// </summary>
    /// <remarks>
    /// These tests previously hard-coded <c>C:\Wallpapers</c>, which is rooted on
    /// Windows but an ordinary filename on Linux and macOS. The relative-path
    /// logic only engages for a rooted path, so three of them asserted nothing
    /// real on Windows' terms and failed outright on the other two platforms. If
    /// the fixture ever stops producing a genuinely rooted path, this fails first
    /// and says why.
    /// </remarks>
    [TestMethod]
    public void FixturePathsAreRootedOnThisPlatform()
    {
        var folder = TestPaths.Rooted("Wallpapers");
        var file = Path.Combine(folder, "a.jpg");

        Assert.IsTrue(Path.IsPathRooted(folder), $"'{folder}' must be rooted for the relative-path logic to engage");
        Assert.IsTrue(Path.IsPathRooted(file), $"'{file}' must be rooted");
        Assert.AreEqual("a.jpg", WallpaperSelectionService.BuildAssignmentKey(folder, file));
    }

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

        var before = TestPaths.Rooted("Wallpapers", "Before");

        // The user tagged an image while the folder was called "Before".
        store.TrySave(
            new AppSettings
            {
                WallpaperDirectory = before,
                Assignments =
                [
                    new WallpaperAssignment
                    {
                        Path = Path.Combine(before, "sub", "a.jpg"),
                        Category = WallpaperCategory.Night
                    }
                ]
            },
            out _);

        var saved = store.Load().Assignments.ToDictionary(
            assignment => assignment.Path,
            assignment => assignment.Category,
            StringComparer.OrdinalIgnoreCase);

        // The folder is renamed and rescanned. Its files now have new absolute
        // paths, but the same paths relative to the folder.
        var after = TestPaths.Rooted("Wallpapers", "After");
        var category = WallpaperSelectionService.ResolveCategory(
            saved,
            after,
            Path.Combine(after, "sub", "a.jpg"));

        Assert.AreEqual(WallpaperCategory.Night, category, "the assignment should survive the rename");
    }

    [TestMethod]
    public void LegacyAbsoluteAssignmentsStillMatchWhenTheFolderHasNotMoved()
    {
        var folder = TestPaths.Rooted("Wallpapers");
        var image = Path.Combine(folder, "a.jpg");

        var saved = new Dictionary<string, WallpaperCategory>(StringComparer.OrdinalIgnoreCase)
        {
            [image] = WallpaperCategory.Day
        };

        Assert.AreEqual(WallpaperCategory.Day, WallpaperSelectionService.ResolveCategory(saved, folder, image));
    }

    [TestMethod]
    public void UnknownFileFallsBackToNameInference()
    {
        var folder = TestPaths.Rooted("Wallpapers");
        var saved = new Dictionary<string, WallpaperCategory>(StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual(
            WallpaperCategory.Night,
            WallpaperSelectionService.ResolveCategory(saved, folder, Path.Combine(folder, "city-night.jpg")));
        Assert.AreEqual(
            WallpaperCategory.Ignore,
            WallpaperSelectionService.ResolveCategory(saved, folder, Path.Combine(folder, "untagged.jpg")));
    }

    [TestMethod]
    public void AssignmentsArePersistedRelativeToTheWallpaperFolder()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);

        var wallpapers = TestPaths.Rooted("Wallpapers");

        store.TrySave(
            new AppSettings
            {
                WallpaperDirectory = wallpapers,
                Assignments =
                [
                    new WallpaperAssignment
                    {
                        Path = Path.Combine(wallpapers, "a.jpg"),
                        Category = WallpaperCategory.Day
                    }
                ]
            },
            out _);

        var json = File.ReadAllText(store.SettingsPath);

        StringAssert.Contains(json, "a.jpg");

        // The stored key must be the bare filename, not the full path. Comparing
        // against the JSON-escaped absolute path keeps this honest on every
        // platform's separator.
        var escapedAbsolute = System.Text.Json.JsonSerializer.Serialize(Path.Combine(wallpapers, "a.jpg")).Trim('"');
        Assert.IsFalse(json.Contains(escapedAbsolute), "the assignment path should be relative on disk");
    }

    [TestMethod]
    public void PathsOutsideTheWallpaperFolderStayAbsolute()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);

        var outside = Path.Combine(TestPaths.Rooted("Elsewhere"), "b.jpg");

        store.TrySave(
            new AppSettings
            {
                WallpaperDirectory = TestPaths.Rooted("Wallpapers"),
                Assignments = [new WallpaperAssignment { Path = outside, Category = WallpaperCategory.Day }]
            },
            out _);

        Assert.AreEqual(outside, store.Load().Assignments[0].Path);
    }

    [TestMethod]
    public void IgnoreAssignmentsAreNotPersisted()
    {
        using var folder = new TempFolder();
        var store = new SettingsStore(folder.Path);

        var wallpapers = TestPaths.Rooted("Wallpapers");

        store.TrySave(
            new AppSettings
            {
                WallpaperDirectory = wallpapers,
                Assignments =
                [
                    new WallpaperAssignment { Path = Path.Combine(wallpapers, "keep.jpg"), Category = WallpaperCategory.Day },
                    new WallpaperAssignment { Path = Path.Combine(wallpapers, "drop.jpg"), Category = WallpaperCategory.Ignore }
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

        var wallpapers = TestPaths.Rooted("Wallpapers");
        var legacyImage = Path.Combine(wallpapers, "legacy.jpg");

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            WallpaperDirectory = wallpapers,
            DayStartsAt = "06:00:00",
            NightStartsAt = "18:00:00",
            ShuffleCadence = 3,
            Assignments = new[] { new { Path = legacyImage, Category = 2 } }
        });

        File.WriteAllText(store.SettingsPath, json);

        var loaded = store.Load(out var status);

        Assert.AreEqual(SettingsLoadStatus.Loaded, status);
        Assert.AreEqual(ShuffleCadence.Weekly, loaded.ShuffleCadence);
        Assert.AreEqual(WallpaperFit.Fill, loaded.WallpaperFit, "a value absent from an old file should fall back to the default");
        Assert.AreEqual(1, loaded.Assignments.Count);
        Assert.AreEqual(legacyImage, loaded.Assignments[0].Path);
        Assert.AreEqual(WallpaperCategory.Night, loaded.Assignments[0].Category);
    }
}
