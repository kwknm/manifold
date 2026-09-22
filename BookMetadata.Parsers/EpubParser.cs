using System.Text.RegularExpressions;
using ErrorOr;
using VersOne.Epub;
using VersOne.Epub.Options;

namespace BookMetadata.Parsers;

public class EpubParser : IBookParser
{
    private static readonly EpubReaderOptions ReaderOptions = new()
    {
        BookCoverReaderOptions = new BookCoverReaderOptions
        {
            Epub2MetadataIgnoreMissingContent = true,
            Epub2MetadataIgnoreMissingManifestItem = true,
            Epub2MetadataIgnoreMissingContentFile = true,
            Epub3IgnoreMissingContentFile = true,
            IgnoreRemoteContentFileError = true
        }
    };

    public bool CanParse(string fileName)
        => fileName.EndsWith(".epub", StringComparison.OrdinalIgnoreCase);

    public ErrorOr<BookMetadata> Parse(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        try
        {
            var book = EpubReader.ReadBook(stream, ReaderOptions);
            if (book is null)
            {
                return Error.Failure(
                    code: "BookMetadata.EpubParseFailed",
                    description: "Failed to parse EPUB metadata: reader returned null.");
            }

            var result = new BookMetadata
            {
                Title = book.Title ?? string.Empty
            };

            if (book.AuthorList.Count == 0 && !string.IsNullOrWhiteSpace(book.Author))
            {
                book.AuthorList.Add(book.Author);
            }
            result.Authors.AddRange(book.AuthorList.Where(a => !string.IsNullOrWhiteSpace(a)));

            result.Isbn = book.Schema?.Package?.Metadata?.Identifiers?
                .FirstOrDefault(i => string.Equals(i.Scheme, "ISBN", StringComparison.OrdinalIgnoreCase))?
                .Identifier ?? string.Empty;

            result.PageCount = EstimatePageCount(book);

            SetCover(result, book);

            return result;
        }
        catch (Exception ex)
        {
            return Error.Failure(
                code: "BookMetadata.EpubParseFailed",
                description: $"Failed to parse EPUB metadata: {ex.Message}");
        }
    }

    private static void SetCover(BookMetadata result, EpubBook book)
    {
        var coverBytes = book.CoverImage;

        if (coverBytes is not { Length: > 0 })
        {
            coverBytes = TryReadCoverByHref(book)
                ?? PlaceholderCoverGenerator.Create(result.Title, result.Author);
        }

        var coverMime = CoverMimeDetector.Detect(coverBytes);
        if (coverMime is null)
        {
            coverBytes = PlaceholderCoverGenerator.Create(result.Title, result.Author);
            coverMime = CoverMimeDetector.Jpeg;
        }

        result.CoverImage = coverBytes;
        result.CoverContentType = coverMime;
    }

    private static byte[]? TryReadCoverByHref(EpubBook book)
    {
        var coverMeta = book.Schema?.Package?.Metadata?.MetaItems?
            .FirstOrDefault(m => string.Equals(m.Name, "cover", StringComparison.OrdinalIgnoreCase))?
            .Content;

        if (string.IsNullOrWhiteSpace(coverMeta))
        {
            return null;
        }

        var item = book.Schema?.Package?.Manifest?.Items?
            .FirstOrDefault(i => string.Equals(i.Href, coverMeta, StringComparison.OrdinalIgnoreCase)
                && (i.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false));

        if (item is null)
        {
            return null;
        }

        return book.Content?.AllFiles?.Local?
            .OfType<EpubLocalByteContentFile>()
            .FirstOrDefault(f => string.Equals(f.FilePath, item.Href, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Key, item.Href, StringComparison.OrdinalIgnoreCase))?
            .Content;
    }

    private static int EstimatePageCount(EpubBook book)
    {
        const int charsPerPage = 2500;

        var totalChars = book.Content?.Html?.Local?
            .Select(f => f.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Sum(c => StripHtml(c).Length) ?? 0;

        return totalChars > 0 ? (int)Math.Ceiling(totalChars / (double)charsPerPage) : 0;
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return Regex.Replace(html, "<[^>]*>", " ");
    }
}