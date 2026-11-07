using System.IO.Compression;
using ErrorOr;
using Shared;

namespace BookMetadata.Parsers;

/// <summary>
/// Container checks for EPUB before the reader library takes over. An EPUB is a ZIP, and the
/// reader materializes every entry into memory, so a single small upload can otherwise expand
/// into gigabytes (zip bomb) or into thousands of tiny allocations. The declared entry sizes
/// come from the archive headers and can lie, so the expansion is measured by reading it.
/// </summary>
public static class EpubArchiveGuard
{
    private const int ReadBufferSize = 64 * 1024;

    /// <summary>
    /// Returns <c>null</c> when the archive is within the limits, otherwise the error to report.
    /// The stream is left at the position it had on entry.
    /// </summary>
    public static Error? Validate(Stream stream)
    {
        var position = stream.CanSeek ? stream.Position : 0;

        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            if (archive.Entries.Count > UploadLimits.MaxArchiveEntries)
            {
                return Error.Validation(
                    code: "BookMetadata.ArchiveTooManyEntries",
                    description: $"Archive contains more than {UploadLimits.MaxArchiveEntries} entries.");
            }

            var declaredBytes = archive.Entries.Sum(entry => entry.Length);

            if (declaredBytes > UploadLimits.MaxExpandedArchiveBytes)
            {
                return Error.Validation(
                    code: "BookMetadata.ArchiveEntryTooLarge",
                    description: $"Archive expands beyond {UploadLimits.MaxExpandedArchiveBytes} bytes.");
            }

            return MeasureExpansion(archive);
        }
        catch (InvalidDataException ex)
        {
            return Error.Failure(
                code: "BookMetadata.EpubParseFailed",
                description: $"Failed to parse EPUB metadata: {ex.Message}");
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = position;
            }
        }
    }

    /// <summary>
    /// Decompresses the entries once without keeping the result, stopping as soon as the budget
    /// is exceeded. Costs one extra pass over the archive and is what makes the declared sizes
    /// above irrelevant to an attacker.
    /// </summary>
    private static Error? MeasureExpansion(ZipArchive archive)
    {
        var buffer = new byte[ReadBufferSize];
        long totalBytes = 0;

        foreach (var entry in archive.Entries)
        {
            using var entryStream = entry.Open();

            while (true)
            {
                var read = entryStream.Read(buffer, 0, buffer.Length);

                if (read == 0)
                {
                    break;
                }

                totalBytes += read;

                if (totalBytes > UploadLimits.MaxExpandedArchiveBytes)
                {
                    return Error.Validation(
                        code: "BookMetadata.ArchiveEntryTooLarge",
                        description: $"Archive expands beyond {UploadLimits.MaxExpandedArchiveBytes} bytes.");
                }
            }
        }

        return null;
    }
}
