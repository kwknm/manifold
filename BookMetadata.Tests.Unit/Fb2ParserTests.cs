using BookMetadata.Parsers;
using BookMetadata.Tests.Unit.TestData;

namespace BookMetadata.Tests.Unit;

public class Fb2ParserTests
{
    private readonly Fb2Parser _parser = new();

    [Fact]
    public void Parse_PopulatesMetadata_FromNamespacedXml()
    {
        var xml = Fb2Builder.CreateXml(
            "Р’РѕР№РЅР° Рё РјРёСЂ",
            [("Р›РµРІ", "РўРѕР»СЃС‚РѕР№"), ("РђР»РµРєСЃРµР№", "РРІР°РЅРѕРІ")],
            isbn: "978-5-17-123456-7",
            withNamespace: true);

        var result = _parser.Parse(Fb2Builder.ToStream(xml));

        Assert.False(result.IsError);
        Assert.Equal("Р’РѕР№РЅР° Рё РјРёСЂ", result.Value.Title);
        Assert.Equal(["Р›РµРІ РўРѕР»СЃС‚РѕР№", "РђР»РµРєСЃРµР№ РРІР°РЅРѕРІ"], result.Value.Authors);
        Assert.Equal("978-5-17-123456-7", result.Value.Isbn);
    }

    [Fact]
    public void Parse_PopulatesMetadata_FromXmlWithoutNamespace()
    {
        var xml = Fb2Builder.CreateXml("No Namespace Book", [("John", "Doe")], isbn: "1234", withNamespace: false);

        var result = _parser.Parse(Fb2Builder.ToStream(xml));

        Assert.False(result.IsError);
        Assert.Equal("No Namespace Book", result.Value.Title);
        Assert.Equal(["John Doe"], result.Value.Authors);
        Assert.Equal("1234", result.Value.Isbn);
    }

    [Fact]
    public void Parse_ReturnsEmptyMetadata_WhenDescriptionMissing()
    {
        var result = _parser.Parse(Fb2Builder.ToStream(Fb2Builder.CreateMinimalXml()));

        Assert.False(result.IsError);
        Assert.Equal(string.Empty, result.Value.Title);
        Assert.Empty(result.Value.Authors);
    }

    [Fact]
    public void Parse_ReturnsFailure_ForMalformedXml()
    {
        var result = _parser.Parse(Fb2Builder.ToStream("<FictionBook><description>"));

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
    public void Parse_ExtractsFromFb2Zip()
    {
        var xml = Fb2Builder.CreateXml("Zipped Book", [("Ann", "Lee")], isbn: "0001");
        var zip = Fb2Builder.CreateZip("book.fb2", xml);

        var result = _parser.Parse(zip);

        Assert.False(result.IsError);
        Assert.Equal("Zipped Book", result.Value.Title);
        Assert.Equal(["Ann Lee"], result.Value.Authors);
    }

    [Fact]
    public void Parse_ReturnsFailure_WhenZipHasNoFb2Entry()
    {
        var zip = Fb2Builder.CreateZip("readme.txt", "not a book");

        var result = _parser.Parse(zip);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public void Parse_ExtractsCover_FromCoverpageAndBinary()
    {
        var cover = Fb2Builder.CreatePng();
        var xml = Fb2Builder.CreateXml(
            "Book With Cover",
            [("Ann", "Lee")],
            isbn: "0001",
            coverHref: "cover.png",
            coverImage: cover);

        var result = _parser.Parse(Fb2Builder.ToStream(xml));

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal(cover, result.Value.CoverImage);
        Assert.Equal("image/png", result.Value.CoverContentType);
    }

    [Fact]
    public void Parse_GeneratesPlaceholder_WhenCoverpageMissing()
    {
        var xml = Fb2Builder.CreateXml("Book Without Cover", [("Ann", "Lee")]);

        var result = _parser.Parse(Fb2Builder.ToStream(xml));

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal("image/jpeg", result.Value.CoverContentType);
        CoverAssert.AssertJpeg(result.Value.CoverImage);
    }

    [Fact]
    public void Parse_GeneratesPlaceholder_WhenBinaryIdNotFound()
    {
        var xml = Fb2Builder.CreateXml(
            "Broken Cover Book",
            [("Ann", "Lee")],
            coverHref: "missing.png");

        var result = _parser.Parse(Fb2Builder.ToStream(xml));

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal("image/jpeg", result.Value.CoverContentType);
        CoverAssert.AssertJpeg(result.Value.CoverImage);
    }

    [Fact]
    public void Parse_GeneratesPlaceholder_WhenBinaryIsNotRaster()
    {
        var svg = Fb2Builder.CreateSvg();
        var xml = Fb2Builder.CreateXml(
            "Svg Cover Book",
            [("Ann", "Lee")],
            coverHref: "cover.svg",
            coverImage: svg);

        var result = _parser.Parse(Fb2Builder.ToStream(xml));

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal("image/jpeg", result.Value.CoverContentType);
        CoverAssert.AssertJpeg(result.Value.CoverImage);
    }

    [Fact]
    public void Parse_ExtractsCover_FromFb2Zip()
    {
        var xml = Fb2Builder.CreateXml(
            "Zipped Book",
            [("Ann", "Lee")],
            coverHref: "cover.png",
            coverImage: Fb2Builder.CreatePng());
        var zip = Fb2Builder.CreateZip("book.fb2", xml);

        var result = _parser.Parse(zip);

        Assert.False(result.IsError);
        Assert.True(result.Value.HasCover);
        Assert.Equal("image/png", result.Value.CoverContentType);
    }
}
