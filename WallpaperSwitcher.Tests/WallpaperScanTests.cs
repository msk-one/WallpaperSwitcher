namespace WallpaperSwitcher.Tests;

[TestClass]
public sealed class WallpaperScanTests
{
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    /// <summary>
    /// A junction or symlink pointing back up its own tree used to be followed
    /// forever, collecting duplicates until the paths grew too long to open.
    /// </summary>
    [TestMethod]
    public void SymlinkedSubdirectoryIsNotFollowed()
    {
        using var folder = new TempFolder();
        folder.WriteBytes("photo-day.jpg", JpegHeader);

        var nested = Path.Combine(folder.Path, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "nested-night.jpg"), JpegHeader);

        try
        {
            Directory.CreateSymbolicLink(Path.Combine(nested, "loop"), folder.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive("Creating a symbolic link requires Developer Mode or elevation on Windows.");
            return;
        }

        var result = WallpaperSelectionService.LoadWallpapersWithDiagnostics(folder.Path);

        Assert.AreEqual(2, result.Wallpapers.Count, "each file should be found exactly once");
        CollectionAssert.AllItemsAreUnique(result.Wallpapers.Select(item => item.FullPath).ToList());
    }

    [TestMethod]
    public void GifIsRecognisedAsASupportedFormat()
    {
        using var folder = new TempFolder();
        folder.WriteBytes("animated-day.gif", [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]);

        var result = WallpaperSelectionService.LoadWallpapersWithDiagnostics(folder.Path);

        Assert.AreEqual(1, result.Wallpapers.Count);
        Assert.AreEqual(WallpaperCategory.Day, result.Wallpapers[0].Category);
    }

    [TestMethod]
    public void ScanIsStillRecursiveForOrdinaryFolders()
    {
        using var folder = new TempFolder();
        var deep = Path.Combine(folder.Path, "a", "b", "c");
        Directory.CreateDirectory(deep);
        File.WriteAllBytes(Path.Combine(deep, "deep-night.png"), [0x89, 0x50, 0x4E, 0x47]);

        var result = WallpaperSelectionService.LoadWallpapersWithDiagnostics(folder.Path);

        Assert.AreEqual(1, result.Wallpapers.Count);
        Assert.AreEqual(WallpaperCategory.Night, result.Wallpapers[0].Category);
    }
}
