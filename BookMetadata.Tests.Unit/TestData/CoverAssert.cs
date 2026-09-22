using BookMetadata.Parsers;

namespace BookMetadata.Tests.Unit.TestData;

public static class CoverAssert
{
    public static void AssertJpeg(byte[]? cover)
    {
        Assert.NotNull(cover);
        Assert.True(cover.Length > 3);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, cover[..3]);
    }
}