using SkiaSharp;
using Shared;

namespace BookMetadata.Parsers;

/// <summary>
/// Validates extracted cover images. Magic-byte detection only proves the first few bytes,
/// so a truncated or corrupt payload with a valid signature would otherwise be stored as a
/// cover and fail to render for the client. Validation decodes the image, rejects
/// unreasonable dimensions, and re-encodes it into a canonical format.
/// </summary>
public static class CoverValidator
{
    private const int JpegQuality = 85;

    public sealed record ValidatedCover(byte[] Bytes, string ContentType);

    /// <summary>
    /// Returns a re-encoded, size-checked cover, or <c>null</c> when the payload is not a
    /// decodable raster image within the allowed dimensions.
    /// </summary>
    public static ValidatedCover? Validate(byte[]? bytes, string? declaredContentType)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        if (!CoverMimeDetector.IsRaster(declaredContentType))
        {
            return null;
        }

        if (bytes.Length > UploadLimits.MaxCoverFileSizeBytes)
        {
            return null;
        }

        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);

            if (codec is null)
            {
                return null;
            }

            if (codec.Info.Width <= 0 || codec.Info.Height <= 0)
            {
                return null;
            }

            if (codec.Info.Width > UploadLimits.MaxCoverImageDimension
                || codec.Info.Height > UploadLimits.MaxCoverImageDimension)
            {
                return null;
            }

            // Decoding, not just header parsing: this is what rejects truncated payloads.
            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return null;
            }

            using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            if (encoded is null || encoded.Size == 0)
            {
                return null;
            }

            return new ValidatedCover(encoded.ToArray(), CoverMimeDetector.Jpeg);
        }
        catch (Exception)
        {
            // Malformed image data surfaces as a variety of codec exceptions; all of them
            // mean the same thing here: not a usable cover.
            return null;
        }
    }
}
