using WallpaperSwitcher;

namespace WallpaperSwitcher.Tests;

[TestClass]
public class WallpaperSelectionServiceTests
{
    [TestMethod]
    public void LoadWallpapers_InfersDayAndNightFromFileNames()
    {
        var directory = Directory.CreateTempSubdirectory("wallpaper-switcher-tests-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "forest-day.jpg"), string.Empty);
            File.WriteAllText(Path.Combine(directory.FullName, "city-night.png"), string.Empty);
            File.WriteAllText(Path.Combine(directory.FullName, "ventura-night.heic"), string.Empty);
            File.WriteAllText(Path.Combine(directory.FullName, "notes.txt"), string.Empty);

            var wallpapers = WallpaperSelectionService.LoadWallpapers(directory.FullName);

            Assert.AreEqual(3, wallpapers.Count);
            Assert.AreEqual(WallpaperCategory.Night, wallpapers.Single(item => item.FileName == "city-night.png").Category);
            Assert.AreEqual(WallpaperCategory.Day, wallpapers.Single(item => item.FileName == "forest-day.jpg").Category);
            Assert.AreEqual(WallpaperCategory.Night, wallpapers.Single(item => item.FileName == "ventura-night.heic").Category);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void LoadWallpapers_ScansSubfolders()
    {
        var directory = Directory.CreateTempSubdirectory("wallpaper-switcher-tests-");

        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(directory.FullName, "Nested"));
            File.WriteAllText(Path.Combine(nested.FullName, "sunset-day.webp"), string.Empty);

            var wallpapers = WallpaperSelectionService.LoadWallpapers(directory.FullName);

            Assert.AreEqual(1, wallpapers.Count);
            Assert.AreEqual("sunset-day.webp", wallpapers[0].FileName);
            Assert.AreEqual(WallpaperCategory.Day, wallpapers[0].Category);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void PickWallpaper_AvoidsRepeatingLastWallpaperOnNewCycleWhenPossible()
    {
        var candidates = new[] { "a.jpg", "b.jpg" };
        var cycleKey = "Day:20260501";
        var initiallyPicked = WallpaperSelectionService.PickWallpaper(candidates, cycleKey, null, null);

        var nextPick = WallpaperSelectionService.PickWallpaper(
            candidates,
            "Day:20260502",
            initiallyPicked,
            cycleKey);

        Assert.AreNotEqual(initiallyPicked, nextPick);
    }
}
