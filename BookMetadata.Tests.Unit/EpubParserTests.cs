using BookMetadata.Parsers;
using BookMetadata.Tests.Unit.TestData;

namespace BookMetadata.Tests.Unit;

public class EpubParserTests
{
    private readonly EpubParser _parser = new();

    [Fact]
    public void Parse_PopulatesMetadata()
    {
        var stream = EpubBuilder.Create(
            "Epub Test Book",
            "Author One",
            "978-1-234567-89-0",
            new string('a', 3000));

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.Equal("Epub Test Book", result.Value.Title);
        Assert.Equal(["Author One"], result.Value.Authors);
        Assert.Equal("978-1-234567-89-0", result.Value.Isbn);
        Assert.True(result.Value.PageCount > 0);
    }

    [Fact]
    public void Parse_ReturnsFailure_ForCorruptZip()
    {
        var stream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });

        var result = _parser.Parse(stream);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public void Parse_ReturnsFailure_ForEmptyStream()
    {
        var result = _parser.Parse(new MemoryStream());

        Assert.True(result.IsError);
    }

    [Fact]
    public void Parse_ExtractsCover()
    {
        var cover = Fb2Builder.CreatePng();
        var stream = EpubBuilder.Create(
            "Cover Book",
            "Author",
            "978-1-111111-11-1",
            "text",
            coverImage: cover);

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal(cover, result.Value.CoverImage);
        Assert.Equal("image/png", result.Value.CoverContentType);
    }

    [Fact]
    public void Parse_GeneratesPlaceholder_WhenCoverMissing()
    {
        var stream = EpubBuilder.Create("No Cover Book", "Author One", "978-2-222222-22-2", "text");

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal("image/jpeg", result.Value.CoverContentType);
        CoverAssert.AssertJpeg(result.Value.CoverImage);
    }

[Fact]
    public void Parse_GeneratesPlaceholder_WhenCoverIsSvg()
    {
        var stream = EpubBuilder.Create(
            "Svg Cover Book",
            "Author One",
            "978-3-333333-33-3",
            "text",
            coverImage: Fb2Builder.CreateSvg(),
            coverExtension: "svg");

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal("image/jpeg", result.Value.CoverContentType);
        CoverAssert.AssertJpeg(result.Value.CoverImage);
    }

    [Fact]
    public void Parse_RepairsCover_WhenMetaContainsHrefInsteadOfId()
    {
        var cover = Fb2Builder.CreatePng();
        var stream = EpubBuilder.Create(
            "Broken Meta Book",
            "Author One",
            "978-4-444444-44-4",
            "text",
            coverImage: cover,
            coverMetaAsHref: true);

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal(cover, result.Value.CoverImage);
        Assert.Equal("image/png", result.Value.CoverContentType);
    }

    [Fact]
    public void Parse_GeneratesPlaceholder_WhenCoverMetaReferencesMissingFile()
    {
        var stream = EpubBuilder.Create(
            "Missing Cover File Book",
            "Author One",
            "978-5-555555-55-5",
            "text",
            coverMetaContent: "Images/missing.png");

        var result = _parser.Parse(stream);

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal("image/jpeg", result.Value.CoverContentType);
        CoverAssert.AssertJpeg(result.Value.CoverImage);
    }
}
