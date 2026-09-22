using ErrorOr;

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
}
