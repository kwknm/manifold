using ErrorOr;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;

namespace BookMetadata.Parsers;

public class PdfParser : IBookParser
{
    private const int MaxCoverWidth = 800;
    private const int JpegQuality = 85;

    public bool CanParse(string fileName)
        => fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public ErrorOr<BookMetadata> Parse(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        try
        {
            using var document = PdfDocument.Open(stream);
            var result = new BookMetadata
            {
                Title = document.Information.Title ?? string.Empty,
                PageCount = document.NumberOfPages
            };

            if (!string.IsNullOrWhiteSpace(document.Information.Author))
            {
                result.Authors.AddRange(ParseAuthors(document.Information.Author));
            }

            SetCover(result, stream);

            return result;
        }
        catch (Exception ex)
        {
            return Error.Failure(
                code: "BookMetadata.PdfParseFailed",
                description: $"Failed to parse PDF metadata: {ex.Message}");
        }
    }

    private static void SetCover(BookMetadata result, Stream stream)
    {
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            // CA1416: PDFtoImage supports windows/linux/macos/browser, which covers all server deployment targets.
#pragma warning disable CA1416
            using var page = Conversion.ToImage(pdfStream: stream, page: 0);
#pragma warning restore CA1416
            using var cover = Downscale(page, MaxCoverWidth);
            using var data = cover.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

            result.CoverImage = data.ToArray();
            result.CoverContentType = CoverMimeDetector.Jpeg;
        }
        catch (Exception)
        {
            result.CoverImage = PlaceholderCoverGenerator.Create(result.Title, result.Author);
            result.CoverContentType = CoverMimeDetector.Jpeg;
        }
    }

    private static SKBitmap Downscale(SKBitmap bitmap, int maxWidth)
    {
        if (bitmap.Width <= maxWidth)
        {
            return bitmap;
        }

        var height = Math.Max(1, (int)Math.Round(bitmap.Height * (maxWidth / (double)bitmap.Width)));
        return bitmap.Resize(new SKImageInfo(maxWidth, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
    }

    private static IEnumerable<string> ParseAuthors(string author)
        => author.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}