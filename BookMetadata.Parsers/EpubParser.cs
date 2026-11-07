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
                Title = book.Title
            };

            if (book.AuthorList.Count == 0 && !string.IsNullOrWhiteSpace(book.Author))
            {
                book.AuthorList.Add(book.Author);
            }

            result.Authors.AddRange(book.AuthorList.Where(a => !string.IsNullOrWhiteSpace(a)));

            result.Isbn = book.Schema.Package.Metadata.Identifiers
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
        var validated = coverMime is null ? null : CoverValidator.Validate(coverBytes, coverMime);

        if (validated is null)
        {
            // Either the bytes are not a raster image or they do not decode as one.
            coverBytes = PlaceholderCoverGenerator.Create(result.Title, result.Author);
            coverMime = CoverMimeDetector.Jpeg;
        }
        else
        {
            coverBytes = validated.Bytes;
            coverMime = validated.ContentType;
        }

        result.CoverImage = coverBytes;
        result.CoverContentType = coverMime;
    }

    private static byte[]? TryReadCoverByHref(EpubBook book)
    {
        var coverMeta = book.Schema.Package.Metadata.MetaItems
            .FirstOrDefault(m => string.Equals(m.Name, "cover", StringComparison.OrdinalIgnoreCase))?
            .Content;

        if (string.IsNullOrWhiteSpace(coverMeta))
        {
            return null;
        }

        var item = book.Schema.Package.Manifest.Items
            .FirstOrDefault(i => string.Equals(i.Href, coverMeta, StringComparison.OrdinalIgnoreCase)
                                 && (i.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)));

        if (item is null)
        {
            return null;
        }

        return book.Content.AllFiles.Local
            .OfType<EpubLocalByteContentFile>()
            .FirstOrDefault(f => string.Equals(f.FilePath, item.Href, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(f.Key, item.Href, StringComparison.OrdinalIgnoreCase))?
            .Content;
    }

    private static int EstimatePageCount(EpubBook book)
    {
        const int charsPerPage = 2500;

        // Page count is an estimate and not worth unbounded work: stop once the running
        // total is large enough that further documents cannot change the result materially.
        const long maxCountedChars = 2500L * 20_000;

        long totalChars = 0;

        foreach (var file in book.Content.Html.Local)
        {
            if (string.IsNullOrWhiteSpace(file.Content))
            {
                continue;
            }

            totalChars += StripHtml(file.Content).Length;

            if (totalChars >= maxCountedChars)
            {
                totalChars = maxCountedChars;
                break;
            }
        }

        return totalChars > 0 ? (int)Math.Ceiling(totalChars / (double)charsPerPage) : 0;
    }

    private static string StripHtml(string? html)
    {
        return string.IsNullOrEmpty(html) ? string.Empty : Regex.Replace(html, "<[^>]*>", " ");
    }
}