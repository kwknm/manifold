namespace BookMetadata.Parsers;

public static class CoverMimeDetector
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Gif = "image/gif";
    public const string Webp = "image/webp";

    private const int MagicBytesLength = 12;

    public static bool IsRaster(string? contentType)
    {
        return contentType is Jpeg or Png or Gif or Webp;
    }

    public static string? Detect(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < MagicBytesLength)
        {
            return null;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return Jpeg;
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return Png;
        }

        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
        {
            return Gif;
        }

        if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return Webp;
        }

        return null;
    }

    public static string GetFileExtension(string contentType)
    {
        return contentType switch
        {
            Jpeg => "jpg",
            Png => "png",
            Gif => "gif",
            Webp => "webp",
            _ => "bin"
        };
    }
}