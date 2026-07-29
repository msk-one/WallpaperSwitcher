namespace WallpaperSwitcher.Tests;

/// <summary>
/// Builds absolute paths that are absolute on the platform running the test.
/// </summary>
/// <remarks>
/// The settings tests exercise <see cref="WallpaperSelectionService.BuildAssignmentKey"/>,
/// which only makes a path relative when <see cref="Path.IsPathRooted"/> says it
/// is rooted. A literal <c>C:\Wallpapers\a.jpg</c> is rooted on Windows but is
/// just a filename on Linux and macOS, where the backslash is an ordinary
/// character — so those tests silently asserted nothing on two of the three
/// platforms and then failed on the assertions that did depend on it.
/// </remarks>
internal static class TestPaths
{
    /// <summary>
    /// Joins <paramref name="segments"/> under a platform-appropriate root:
    /// <c>C:\</c> on Windows, <c>/</c> elsewhere.
    /// </summary>
    public static string Rooted(params string[] segments)
    {
        var root = OperatingSystem.IsWindows() ? @"C:\" : "/";
        return Path.Combine(new[] { root }.Concat(segments).ToArray());
    }
}
