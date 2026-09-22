using ErrorOr;

namespace BookMetadata.Parsers;

public interface IBookParser
{
    bool CanParse(string fileName);
    ErrorOr<BookMetadata> Parse(Stream stream);
}