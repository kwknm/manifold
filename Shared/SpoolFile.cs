namespace Shared;

/// <summary>
/// Temporary buffer for book-sized payloads. Uploads and downloads reach
/// <see cref="UploadLimits.MaxBookFileSizeBytes"/> and several can be in flight at once
/// (Storage accepts concurrent uploads, BookMetadata parses up to
/// <see cref="UploadLimits.MaxConcurrentMetadataParses"/> books), so buffering them in a
/// <see cref="MemoryStream"/> puts hundreds of megabytes of garbage-prone memory per service
/// on the heap. A file in the temp directory keeps the same seekable stream contract with
/// bounded memory, and it disappears when the stream is closed.
/// </summary>
public static class SpoolFile
{
    private const int BufferSize = 64 * 1024;

    public static FileStream Create()
    {
        var path = Path.Combine(Path.GetTempPath(), $"booktracking-{Guid.CreateVersion7():N}.tmp");

        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
    }
}
