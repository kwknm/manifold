using ErrorOr;
using Shared;

namespace BookMetadata.Parsers;

public class BookParser(IEnumerable<IBookParser>? parsers = null)
{
    private readonly IList<IBookParser> _parsers = parsers?.ToList() ??
    [
        new EpubParser(),
        new Fb2Parser(),
        new PdfParser()
    ];

    public ErrorOr<BookMetadata> Parse(string fileName, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(stream);

        var parser = _parsers.FirstOrDefault(p => p.CanParse(fileName));
        if (parser is null)
        {
            return Error.NotFound(
                code: "BookMetadata.ParserNotFound",
                description: $"No parser found for file '{fileName}'.");
        }

        return parser.Parse(stream);
    }

    /// <summary>
    /// Parses a file whose format was already validated against its declared MIME type.
    /// Selecting the parser from the validated format keeps the MIME/extension pair as the
    /// single source of truth instead of re-deriving it from the file name here.
    /// </summary>
    public ErrorOr<BookMetadata> Parse(BookFormats.Format format, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(stream);

        IBookParser? parser = format.Kind switch
        {
            BookFormats.FormatKind.EPUB => _parsers.OfType<EpubParser>().FirstOrDefault(),
            BookFormats.FormatKind.FB2 => _parsers.OfType<Fb2Parser>().FirstOrDefault(),
            BookFormats.FormatKind.PDF => _parsers.OfType<PdfParser>().FirstOrDefault(),
            _ => null
        };

        if (parser is null)
        {
            return Error.NotFound(
                code: "BookMetadata.ParserNotFound",
                description: $"No parser registered for format '{format.ContentType}'.");
        }

        return parser.Parse(stream);
    }
}
