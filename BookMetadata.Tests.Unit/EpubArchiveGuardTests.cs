using System.IO.Compression;
using BookMetadata.Parsers;
using BookMetadata.Tests.Unit.TestData;
using Shared;

namespace BookMetadata.Tests.Unit;

public class EpubArchiveGuardTests
{
    [Fact]
    public void Validate_ReturnsNull_ForOrdinaryEpub()
    {
        var stream = EpubBuilder.Create("Guard Book", "Author One", "978-1-111111-11-1", "text");

        Assert.Null(EpubArchiveGuard.Validate(stream));
    }

    [Fact]
    public void Validate_RejectsArchiveWithTooManyEntries()
    {
        var stream = CreateZipWithEntries(UploadLimits.MaxArchiveEntries + 1, entrySize: 1);

        var error = EpubArchiveGuard.Validate(stream);

        Assert.NotNull(error);
        Assert.Equal(ErrorOr.ErrorType.Validation, error!.Value.Type);
        Assert.Equal("BookMetadata.ArchiveTooManyEntries", error.Value.Code);
    }

    [Fact]
    public void Validate_RejectsArchiveThatExpandsBeyondTheLimit()
    {
        // One entry larger than the total expansion budget: the header alone is enough to
        // reject it, and the guard must not try to materialize the content first.
        var stream = CreateZipWithEntries(1, entrySize: UploadLimits.MaxExpandedArchiveBytes + 1);

        var error = EpubArchiveGuard.Validate(stream);

        Assert.NotNull(error);
        Assert.Equal(ErrorOr.ErrorType.Validation, error!.Value.Type);
        Assert.Equal("BookMetadata.ArchiveEntryTooLarge", error.Value.Code);
    }

    [Fact]
    public void Validate_ReturnsFailure_ForStreamThatIsNotAnArchive()
    {
        var stream = new MemoryStream([0x01, 0x02, 0x03, 0x04, 0x05]);

        var error = EpubArchiveGuard.Validate(stream);

        Assert.NotNull(error);
        Assert.Equal(ErrorOr.ErrorType.Failure, error!.Value.Type);
    }

    [Fact]
    public void Validate_LeavesStreamPositionUnchanged()
    {
        var stream = EpubBuilder.Create("Guard Book", "Author One", "978-1-111111-11-1", "text");
        stream.Position = 0;

        EpubArchiveGuard.Validate(stream);

        Assert.Equal(0, stream.Position);
    }

    private static Stream CreateZipWithEntries(int count, long entrySize)
    {
        var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var buffer = new byte[64 * 1024];

            for (var i = 0; i < count; i++)
            {
                var entry = zip.CreateEntry($"entry{i}.bin", CompressionLevel.Fastest);

                using var writer = entry.Open();

                for (long written = 0; written < entrySize; written += buffer.Length)
                {
                    writer.Write(buffer, 0, (int)Math.Min(buffer.Length, entrySize - written));
                }
            }
        }

        ms.Position = 0;
        return ms;
    }
}
