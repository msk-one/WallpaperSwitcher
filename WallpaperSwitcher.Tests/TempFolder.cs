namespace WallpaperSwitcher.Tests;

/// <summary>
/// Scratch directory that cleans itself up, so tests can work against real files
/// without leaving anything behind on a failure.
/// </summary>
internal sealed class TempFolder : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("wallpaperswitcher-tests");

    public string Path => _directory.FullName;

    public string WriteBytes(string fileName, byte[] contents)
    {
        var path = System.IO.Path.Combine(Path, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    public string WriteText(string fileName, string contents)
    {
        var path = System.IO.Path.Combine(Path, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        try
        {
            _directory.Delete(recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a test over.
        }
    }
}
