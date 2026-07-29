namespace WallpaperSwitcher.Tests;

[TestClass]
public sealed class WindowsRunCommandTests
{
    /// <summary>
    /// Skips a test that cannot mean anything off Windows.
    /// </summary>
    /// <remarks>
    /// <see cref="WindowsRunCommand.ParseExecutablePath"/> finishes by calling
    /// <see cref="Path.GetFullPath"/> to normalise the value read back from the
    /// registry. That is Windows path semantics: on Linux and macOS a backslash
    /// is an ordinary character, so <c>C:\Apps\x.exe</c> is a relative filename
    /// and GetFullPath prepends the working directory to it. Formatting is plain
    /// string work and stays covered everywhere.
    /// </remarks>
    private static bool RequireWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        Assert.Inconclusive("WindowsRunCommand parses Windows paths; only the formatting half is portable.");
        return false;
    }

    [TestMethod]
    public void FormatQuotesThePathWithoutEscapingBackslashes()
    {
        var value = WindowsRunCommand.Format(@"C:\Program Files\WallpaperSwitcher\WallpaperSwitcher.exe");

        Assert.AreEqual(@"""C:\Program Files\WallpaperSwitcher\WallpaperSwitcher.exe""", value);
        Assert.IsFalse(value.Contains(@"\\"), "backslash escaping is .desktop syntax and breaks the round trip here");
    }

    [TestMethod]
    public void FormatAppendsTheArgument()
    {
        var value = WindowsRunCommand.Format(@"C:\Apps\WallpaperSwitcher.exe", "--minimized");

        Assert.AreEqual(@"""C:\Apps\WallpaperSwitcher.exe"" --minimized", value);
    }

    /// <summary>
    /// The bug this class exists to prevent: a value written with .desktop-style
    /// escaping never matched the executable path when read back, so the
    /// "Start at login" checkbox always displayed as unchecked.
    /// </summary>
    [TestMethod]
    public void FormatThenParseRoundTripsToTheOriginalPath()
    {
        if (!RequireWindows())
        {
            return;
        }

        var original = @"C:\Users\MichałSzklarski\AppData\Local\Programs\WallpaperSwitcher\WallpaperSwitcher.exe";

        var parsed = WindowsRunCommand.ParseExecutablePath(WindowsRunCommand.Format(original, "--minimized"));

        Assert.AreEqual(original, parsed);
    }

    [TestMethod]
    public void ParseHandlesAPathWithSpacesAndAnArgument()
    {
        if (!RequireWindows())
        {
            return;
        }

        var parsed = WindowsRunCommand.ParseExecutablePath(@"""C:\WP Test & More\WallpaperSwitcher.exe"" --minimized");

        Assert.AreEqual(@"C:\WP Test & More\WallpaperSwitcher.exe", parsed);
    }

    [TestMethod]
    public void ParseHandlesAnUnquotedValue()
    {
        if (!RequireWindows())
        {
            return;
        }

        var parsed = WindowsRunCommand.ParseExecutablePath(@"C:\Apps\WallpaperSwitcher.exe");

        Assert.AreEqual(@"C:\Apps\WallpaperSwitcher.exe", parsed);
    }

    [TestMethod]
    public void ParseHandlesAnUnquotedValueWithAnArgument()
    {
        if (!RequireWindows())
        {
            return;
        }

        var parsed = WindowsRunCommand.ParseExecutablePath(@"C:\Apps\WallpaperSwitcher.exe --minimized");

        Assert.AreEqual(@"C:\Apps\WallpaperSwitcher.exe", parsed);
    }

    [TestMethod]
    public void ParseNormalisesRedundantSeparators()
    {
        if (!RequireWindows())
        {
            return;
        }

        var parsed = WindowsRunCommand.ParseExecutablePath(@"""C:\Apps\\Sub\.\WallpaperSwitcher.exe""");

        Assert.AreEqual(@"C:\Apps\Sub\WallpaperSwitcher.exe", parsed);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\"")]
    [DataRow("\"\"")]
    public void ParseReturnsNullForValuesThatAreNotPaths(string? value)
    {
        Assert.IsNull(WindowsRunCommand.ParseExecutablePath(value));
    }
}
