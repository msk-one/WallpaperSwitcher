namespace WallpaperSwitcher;

/// <summary>
/// Cheap "is this actually an image" check based on the file's leading bytes.
/// </summary>
/// <remarks>
/// The Windows wallpaper API reports success for a zero-byte or corrupt file and
/// then paints the desktop black, so its return value cannot be used to decide
/// whether an image is usable. Sniffing the header is what lets a bad file be
/// skipped in favour of the next candidate instead of silently blanking the
/// desktop for a whole cycle.
///
/// This only rejects files that are definitely not images. Whether the OS can
/// decode a well-formed HEIC or TIFF still depends on installed codecs.
/// </remarks>
public static class ImageFileProbe
{
    private const int HeaderLength = 16;

    public static bool LooksLikeImage(string path)
    {
        return LooksLikeImage(path, out _);
    }

    public static bool LooksLikeImage(string path, out string? reason)
    {
        reason = null;

        byte[] header;
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length == 0)
            {
                reason = "the file is empty";
                return false;
            }

            header = new byte[HeaderLength];
            var read = stream.ReadAtLeast(header, HeaderLength, throwOnEndOfStream: false);
            if (read < 4)
            {
                reason = "the file is too small to be an image";
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            reason = ex.Message;
            return false;
        }

        if (HasKnownSignature(header))
        {
            return true;
        }

        reason = "the file does not start with a recognised image header";
        return false;
    }

    private static bool HasKnownSignature(ReadOnlySpan<byte> header)
    {
        // JPEG
        if (header.StartsWith([(byte)0xFF, (byte)0xD8, (byte)0xFF]))
        {
            return true;
        }

        // PNG
        if (header.StartsWith([(byte)0x89, (byte)'P', (byte)'N', (byte)'G', (byte)0x0D, (byte)0x0A, (byte)0x1A, (byte)0x0A]))
        {
            return true;
        }

        // BMP
        if (header.StartsWith([(byte)'B', (byte)'M']))
        {
            return true;
        }

        // GIF87a / GIF89a
        if (header.StartsWith([(byte)'G', (byte)'I', (byte)'F', (byte)'8']))
        {
            return true;
        }

        // TIFF, little- and big-endian
        if (header.StartsWith([(byte)'I', (byte)'I', (byte)0x2A, (byte)0x00])
            || header.StartsWith([(byte)'M', (byte)'M', (byte)0x00, (byte)0x2A]))
        {
            return true;
        }

        // WebP: "RIFF" .... "WEBP"
        if (header.Length >= 12
            && header.StartsWith([(byte)'R', (byte)'I', (byte)'F', (byte)'F'])
            && header[8..12].SequenceEqual([(byte)'W', (byte)'E', (byte)'B', (byte)'P']))
        {
            return true;
        }

        // HEIC/HEIF and other ISO base media files: a "ftyp" box at offset 4.
        if (header.Length >= 8
            && header[4..8].SequenceEqual([(byte)'f', (byte)'t', (byte)'y', (byte)'p']))
        {
            return true;
        }

        return false;
    }
}
