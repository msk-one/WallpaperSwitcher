namespace WallpaperSwitcher.Tests;

[TestClass]
public sealed class ImageFileProbeTests
{
    [TestMethod]
    public void EmptyFileIsRejected()
    {
        using var folder = new TempFolder();
        var path = folder.WriteBytes("empty.jpg", []);

        Assert.IsFalse(ImageFileProbe.LooksLikeImage(path, out var reason));
        StringAssert.Contains(reason, "empty");
    }

    [TestMethod]
    public void GarbageWithAnImageExtensionIsRejected()
    {
        using var folder = new TempFolder();
        var path = folder.WriteBytes("garbage.jpg", [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);

        Assert.IsFalse(ImageFileProbe.LooksLikeImage(path, out var reason));
        StringAssert.Contains(reason, "recognised image header");
    }

    [TestMethod]
    public void TruncatedFileIsRejected()
    {
        using var folder = new TempFolder();
        var path = folder.WriteBytes("tiny.png", [0x89, 0x50]);

        Assert.IsFalse(ImageFileProbe.LooksLikeImage(path));
    }

    [DataTestMethod]
    [DataRow("photo.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 })]
    [DataRow("photo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [DataRow("photo.bmp", new byte[] { 0x42, 0x4D, 0x36, 0x00, 0x00, 0x00, 0x00, 0x00 })]
    [DataRow("photo.gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00 })]
    [DataRow("photo.tif", new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00 })]
    [DataRow("photo.tiff", new byte[] { 0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08 })]
    public void KnownSignaturesAreAccepted(string fileName, byte[] header)
    {
        using var folder = new TempFolder();
        var path = folder.WriteBytes(fileName, [.. header, .. new byte[32]]);

        Assert.IsTrue(ImageFileProbe.LooksLikeImage(path), $"{fileName} should be recognised");
    }

    [TestMethod]
    public void WebPIsAccepted()
    {
        using var folder = new TempFolder();
        var bytes = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
            0x00, 0x00, 0x00, 0x00,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P',
            (byte)'V', (byte)'P', (byte)'8', (byte)' '
        };

        Assert.IsTrue(ImageFileProbe.LooksLikeImage(folder.WriteBytes("photo.webp", bytes)));
    }

    [TestMethod]
    public void HeicIsAccepted()
    {
        using var folder = new TempFolder();
        var bytes = new byte[]
        {
            0x00, 0x00, 0x00, 0x18,
            (byte)'f', (byte)'t', (byte)'y', (byte)'p',
            (byte)'h', (byte)'e', (byte)'i', (byte)'c',
            0x00, 0x00, 0x00, 0x00
        };

        Assert.IsTrue(ImageFileProbe.LooksLikeImage(folder.WriteBytes("photo.heic", bytes)));
    }

    [TestMethod]
    public void MissingFileIsRejected()
    {
        using var folder = new TempFolder();

        Assert.IsFalse(ImageFileProbe.LooksLikeImage(Path.Combine(folder.Path, "nope.jpg")));
    }
}
