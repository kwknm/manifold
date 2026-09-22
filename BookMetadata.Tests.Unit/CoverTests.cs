using BookMetadata.Parsers;
using BookMetadata.Tests.Unit.TestData;
using SkiaSharp;

namespace BookMetadata.Tests.Unit;

public class PlaceholderCoverGeneratorTests
{
    [Fact]
    public void Create_IsDeterministic()
    {
        var first = PlaceholderCoverGenerator.Create("Война и мир", "Лев Толстой");
        var second = PlaceholderCoverGenerator.Create("Война и мир", "Лев Толстой");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_DiffersForDifferentTitles()
    {
        var first = PlaceholderCoverGenerator.Create("Book One", "Author");
        var second = PlaceholderCoverGenerator.Create("Book Two", "Author");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_ProducesValidJpegWithBookProportions()
    {
        var cover = PlaceholderCoverGenerator.Create("Some Book", "Some Author");

        CoverAssert.AssertJpeg(cover);

        using var bitmap = SKBitmap.Decode(cover);
        Assert.NotNull(bitmap);
        Assert.Equal(600, bitmap.Width);
        Assert.Equal(900, bitmap.Height);
    }

    [Fact]
    public void Create_HandlesEmptyTitleAndAuthor()
    {
        var cover = PlaceholderCoverGenerator.Create(string.Empty, string.Empty);

        CoverAssert.AssertJpeg(cover);
    }
}

public class CoverMimeDetectorTests
{
    [Fact]
    public void Detect_ReturnsNull_ForNullBytes()
        => Assert.Null(CoverMimeDetector.Detect(null));

    [Fact]
    public void Detect_ReturnsNull_ForShortBytes()
        => Assert.Null(CoverMimeDetector.Detect([0x01, 0x02]));

    [Fact]
    public void Detect_ReturnsNull_ForUnknownBytes()
        => Assert.Null(CoverMimeDetector.Detect(new byte[12]));

    [Fact]
    public void Detect_ReturnsJpeg_ForJpegMagic()
        => Assert.Equal(CoverMimeDetector.Jpeg,
            CoverMimeDetector.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));

    [Fact]
    public void Detect_ReturnsPng_ForPngMagic()
        => Assert.Equal(CoverMimeDetector.Png,
            CoverMimeDetector.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00]));

    [Fact]
    public void Detect_ReturnsGif_ForGifMagic()
        => Assert.Equal(CoverMimeDetector.Gif,
            CoverMimeDetector.Detect([0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));

    [Fact]
    public void Detect_ReturnsWebp_ForWebpMagic()
        => Assert.Equal(CoverMimeDetector.Webp,
            CoverMimeDetector.Detect(
                [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]));

    [Fact]
    public void IsRaster_ReturnsTrue_ForRasterMimes()
    {
        Assert.True(CoverMimeDetector.IsRaster(CoverMimeDetector.Jpeg));
        Assert.True(CoverMimeDetector.IsRaster(CoverMimeDetector.Png));
        Assert.True(CoverMimeDetector.IsRaster(CoverMimeDetector.Gif));
        Assert.True(CoverMimeDetector.IsRaster(CoverMimeDetector.Webp));
    }

    [Fact]
    public void IsRaster_ReturnsFalse_ForSvgAndOthers()
    {
        Assert.False(CoverMimeDetector.IsRaster("image/svg+xml"));
        Assert.False(CoverMimeDetector.IsRaster("image/jpg"));
        Assert.False(CoverMimeDetector.IsRaster(string.Empty));
        Assert.False(CoverMimeDetector.IsRaster(null));
    }

    [Fact]
    public void GetFileExtension_MapsMimes()
    {
        Assert.Equal("jpg", CoverMimeDetector.GetFileExtension(CoverMimeDetector.Jpeg));
        Assert.Equal("png", CoverMimeDetector.GetFileExtension(CoverMimeDetector.Png));
        Assert.Equal("gif", CoverMimeDetector.GetFileExtension(CoverMimeDetector.Gif));
        Assert.Equal("webp", CoverMimeDetector.GetFileExtension(CoverMimeDetector.Webp));
    }
}