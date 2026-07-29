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
        ".gif",
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
            .Select(path => new WallpaperItem(
                Path.GetFileName(path),
                path,
                ResolveCategory(preferredAssignments, wallpaperDirectory, path)))
            .ToList();

        var message = skippedDirectories == 0
            ? null
            : $"Skipped {skippedDirectories} folder(s) that could not be read.";

        return new WallpaperLoadResult(files, skippedDirectories, message);
    }

    /// <summary>
    /// The key an assignment is stored and looked up under: the path relative to
    /// the wallpaper folder where possible, otherwise the absolute path.
    /// </summary>
    /// <remarks>
    /// Keying on the relative path is what lets Day/Night choices survive the
    /// folder being renamed or moved. Anything outside the folder, or on another
    /// volume, stays absolute so it remains meaningful.
    /// </remarks>
    public static string BuildAssignmentKey(string baseDirectory, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Path.IsPathRooted(fullPath))
        {
            return fullPath;
        }

        try
        {
            var relative = Path.GetRelativePath(baseDirectory.Trim(), fullPath);
            return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
                ? fullPath
                : relative;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return fullPath;
        }
    }

    /// <summary>
    /// Looks up a saved category, preferring the folder-relative key and falling
    /// back to the absolute path so settings files written before the change
    /// keep working.
    /// </summary>
    public static WallpaperCategory ResolveCategory(
        IReadOnlyDictionary<string, WallpaperCategory>? preferredAssignments,
        string baseDirectory,
        string fullPath)
    {
        if (preferredAssignments is not null)
        {
            if (preferredAssignments.TryGetValue(BuildAssignmentKey(baseDirectory, fullPath), out var byRelative))
            {
                return byRelative;
            }

            if (preferredAssignments.TryGetValue(fullPath, out var byAbsolute))
            {
                return byAbsolute;
            }
        }

        return InferCategoryFromName(Path.GetFileNameWithoutExtension(fullPath));
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
                // Junctions and symlinks can point back up the tree. Following
                // them turns a scan of somewhere like C:\Users\<name> into a loop
                // that collects the same files over and over until the paths grow
                // too long. Explorer's own search skips reparse points too.
                if (IsReparsePoint(childDirectory))
                {
                    continue;
                }

                pendingDirectories.Push(childDirectory);
            }
        }

        return collectedFiles;
    }

    private static bool IsReparsePoint(string directory)
    {
        try
        {
            return (new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // If the attributes cannot be read, treat it as one and skip it.
            return true;
        }
    }
}

public sealed record WallpaperLoadResult(
    IReadOnlyList<WallpaperItem> Wallpapers,
    int SkippedDirectories,
    string? WarningMessage);
