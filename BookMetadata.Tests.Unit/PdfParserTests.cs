using BookMetadata.Parsers;
using BookMetadata.Tests.Unit.TestData;

namespace BookMetadata.Tests.Unit;

public class PdfParserTests
{
    private readonly PdfParser _parser = new();

    [Fact]
    public void Parse_PopulatesMetadata()
    {
        var stream = PdfBuilder.Create(title: "Test PDF Book", author: "Jane Smith");

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.Equal("Test PDF Book", result.Value.Title);
        Assert.Equal(["Jane Smith"], result.Value.Authors);
        Assert.Equal(1, result.Value.PageCount);
    }

    [Fact]
    public void Parse_SplitsMultipleAuthors()
    {
        var stream = PdfBuilder.Create(author: "Jane Smith; John Doe");

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.Equal(["Jane Smith", "John Doe"], result.Value.Authors);
    }

    [Fact]
    public void Parse_ReturnsDefaults_WhenInfoMissing()
    {
        var stream = PdfBuilder.Create();

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.Equal(string.Empty, result.Value.Title);
        Assert.Empty(result.Value.Authors);
        Assert.Equal(1, result.Value.PageCount);
    }

    [Fact]
    public void Parse_ReturnsFailure_ForInvalidPdf()
    {
        var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x00 });

        var result = _parser.Parse(stream);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public void Parse_RendersFirstPageAsCover()
    {
        var stream = PdfBuilder.Create(title: "Test PDF Book", author: "Jane Smith");

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal("image/jpeg", result.Value.CoverContentType);
        CoverAssert.AssertJpeg(result.Value.CoverImage);
    }
}