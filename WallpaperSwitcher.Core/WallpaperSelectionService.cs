using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace WallpaperSwitcher;

public static class WallpaperSelectionService
{
    public static readonly string[] SupportedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".heic",
        ".heif",
        ".webp",
        ".tif",
        ".tiff"
    ];

    public static List<WallpaperItem> LoadWallpapers(
        string wallpaperDirectory,
        IReadOnlyDictionary<string, WallpaperCategory>? preferredAssignments = null)
    {
        return LoadWallpapersWithDiagnostics(wallpaperDirectory, preferredAssignments).Wallpapers.ToList();
    }

    public static WallpaperLoadResult LoadWallpapersWithDiagnostics(
        string wallpaperDirectory,
        IReadOnlyDictionary<string, WallpaperCategory>? preferredAssignments = null)
    {
        if (string.IsNullOrWhiteSpace(wallpaperDirectory) || !Directory.Exists(wallpaperDirectory))
        {
            return new WallpaperLoadResult([], 0, "The selected wallpaper folder does not exist.");
        }

        var files = EnumerateFilesSafely(wallpaperDirectory, out var skippedDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var category = preferredAssignments is not null
                    && preferredAssignments.TryGetValue(path, out var savedCategory)
                        ? savedCategory
                        : InferCategoryFromName(Path.GetFileNameWithoutExtension(path));

                return new WallpaperItem(Path.GetFileName(path), path, category);
            })
            .ToList();

        var message = skippedDirectories == 0
            ? null
            : $"Skipped {skippedDirectories} folder(s) that macOS would not allow this app to read.";

        return new WallpaperLoadResult(files, skippedDirectories, message);
    }

    public static WallpaperCategory InferCategoryFromName(string name)
    {
        if (name.Contains("night", StringComparison.OrdinalIgnoreCase))
        {
            return WallpaperCategory.Night;
        }

        if (name.Contains("day", StringComparison.OrdinalIgnoreCase))
        {
            return WallpaperCategory.Day;
        }

        return WallpaperCategory.Ignore;
    }

    public static string PickWallpaper(
        IReadOnlyList<string> candidates,
        string cycleKey,
        string? lastAppliedPath,
        string? lastAppliedCycleKey)
    {
        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one candidate wallpaper is required.", nameof(candidates));
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cycleKey));
        var rawIndex = BinaryPrimitives.ReadUInt32LittleEndian(hashBytes);
        var index = (int)(rawIndex % (uint)candidates.Count);

        if (!string.IsNullOrWhiteSpace(lastAppliedPath)
            && !string.Equals(cycleKey, lastAppliedCycleKey, StringComparison.Ordinal)
            && string.Equals(candidates[index], lastAppliedPath, StringComparison.OrdinalIgnoreCase))
        {
            index = (index + 1) % candidates.Count;
        }

        return candidates[index];
    }

    private static List<string> EnumerateFilesSafely(string rootDirectory, out int skippedDirectories)
    {
        skippedDirectories = 0;
        var collectedFiles = new List<string>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectory);

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();

            string[] directoryFiles;
            try
            {
                directoryFiles = Directory.GetFiles(directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                skippedDirectories++;
                continue;
            }

            foreach (var file in directoryFiles)
            {
                collectedFiles.Add(file);
            }

            string[] childDirectories;
            try
            {
                childDirectories = Directory.GetDirectories(directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                skippedDirectories++;
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                pendingDirectories.Push(childDirectory);
            }
        }

        return collectedFiles;
    }
}

public sealed record WallpaperLoadResult(
    IReadOnlyList<WallpaperItem> Wallpapers,
    int SkippedDirectories,
    string? WarningMessage);
