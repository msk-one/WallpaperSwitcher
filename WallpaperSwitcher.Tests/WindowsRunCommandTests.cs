namespace WallpaperSwitcher.Tests;

[TestClass]
public sealed class WindowsRunCommandTests
{
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
        var original = @"C:\Users\MichałSzklarski\AppData\Local\Programs\WallpaperSwitcher\WallpaperSwitcher.exe";

        var parsed = WindowsRunCommand.ParseExecutablePath(WindowsRunCommand.Format(original, "--minimized"));

        Assert.AreEqual(original, parsed);
    }

    [TestMethod]
    public void ParseHandlesAPathWithSpacesAndAnArgument()
    {
        var parsed = WindowsRunCommand.ParseExecutablePath(@"""C:\WP Test & More\WallpaperSwitcher.exe"" --minimized");

        Assert.AreEqual(@"C:\WP Test & More\WallpaperSwitcher.exe", parsed);
    }

    [TestMethod]
    public void ParseHandlesAnUnquotedValue()
    {
        var parsed = WindowsRunCommand.ParseExecutablePath(@"C:\Apps\WallpaperSwitcher.exe");

        Assert.AreEqual(@"C:\Apps\WallpaperSwitcher.exe", parsed);
    }

    [TestMethod]
    public void ParseHandlesAnUnquotedValueWithAnArgument()
    {
        var parsed = WindowsRunCommand.ParseExecutablePath(@"C:\Apps\WallpaperSwitcher.exe --minimized");

        Assert.AreEqual(@"C:\Apps\WallpaperSwitcher.exe", parsed);
    }

    [TestMethod]
    public void ParseNormalisesRedundantSeparators()
    {
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
