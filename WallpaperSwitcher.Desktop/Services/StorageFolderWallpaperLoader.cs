using Avalonia.Platform.Storage;

namespace WallpaperSwitcher.Desktop.Services;

public static class StorageFolderWallpaperLoader
{
    public static async Task<WallpaperLoadResult> LoadAsync(
        IStorageFolder folder,
        IReadOnlyDictionary<string, WallpaperCategory>? preferredAssignments = null)
    {
        var skippedFolders = 0;
        var paths = new List<string>();
        await CollectFilesAsync(folder, paths, () => skippedFolders++).ConfigureAwait(true);

        var wallpapers = paths
            .Where(path => WallpaperSelectionService.SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new WallpaperItem(
                Path.GetFileName(path),
                path,
                WallpaperSelectionService.ResolveCategory(preferredAssignments, folder.TryGetLocalPath() ?? string.Empty, path)))
            .ToList();

        var warning = skippedFolders == 0
            ? null
            : $"Skipped {skippedFolders} folder(s) that could not be read.";

        return new WallpaperLoadResult(wallpapers, skippedFolders, warning);
    }

    private static async Task CollectFilesAsync(IStorageFolder folder, List<string> paths, Action onSkippedFolder)
    {
        try
        {
            await foreach (var item in folder.GetItemsAsync())
            {
                switch (item)
                {
                    case IStorageFile file:
                        var filePath = file.TryGetLocalPath();
                        if (!string.IsNullOrWhiteSpace(filePath))
                        {
                            paths.Add(filePath);
                        }

                        break;
                    case IStorageFolder childFolder:
                        await CollectFilesAsync(childFolder, paths, onSkippedFolder).ConfigureAwait(true);
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            onSkippedFolder();
        }
    }
}
