using BookMetadata.Parsers;
using BookMetadata.Tests.Unit.TestData;
using ErrorOr;
using ParsedMetadata = BookMetadata.Parsers.BookMetadata;

namespace BookMetadata.Tests.Unit;

public class BookParserTests
{
    private sealed class StubParser(string supportedExtension) : IBookParser
    {
        public bool CanParse(string fileName)
            => fileName.EndsWith(supportedExtension, StringComparison.OrdinalIgnoreCase);

        public ErrorOr<ParsedMetadata> Parse(Stream stream)
            => new ParsedMetadata { Title = "parsed" };
    }

    [Fact]
    public void Parse_SelectsParserByExtension()
    {
        var stub = new StubParser(".epub");
        var parser = new BookParser([stub]);

        var result = parser.Parse("book.epub", new MemoryStream());

        Assert.False(result.IsError);
        Assert.Equal("parsed", result.Value.Title);
    }

    [Fact]
    public void Parse_ReturnsNotFound_ForUnknownExtension()
    {
        var parser = new BookParser();

        var result = parser.Parse("book.txt", new MemoryStream());

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public void Parse_Throws_WhenFileNameIsNull()
    {
        var parser = new BookParser();

        Assert.Throws<ArgumentNullException>(() => parser.Parse(null!, new MemoryStream()));
    }

    [Fact]
    public void Parse_Throws_WhenStreamIsNull()
    {
        var parser = new BookParser();

        Assert.Throws<ArgumentNullException>(() => parser.Parse("book.epub", null!));
    }

    [Fact]
    public void Parse_EndToEnd_Fb2Zip()
    {
        var parser = new BookParser();
        var xml = Fb2Builder.CreateXml("Zipped Book", [("Ann", "Lee")], isbn: "0001");
        var zip = Fb2Builder.CreateZip("book.fb2", xml);

        var result = parser.Parse("book.fb2.zip", zip);

        Assert.False(result.IsError);
        Assert.Equal("Zipped Book", result.Value.Title);
    }
}