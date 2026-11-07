namespace Shared;

/// <summary>
/// Size and resource limits for book upload and processing. Enforced on every layer
/// that touches the file (REST validation, Storage, Metadata), because the REST check
/// alone can be bypassed by calling the internal gRPC services directly.
/// </summary>
public static class UploadLimits
{
    public const long MaxBookFileSizeBytes = 100L * 1024 * 1024;

    /// <summary>
    /// Maximum request body size. Includes multipart overhead on top of the file itself.
    /// </summary>
    public const long MaxRequestSizeBytes = MaxBookFileSizeBytes + 10L * 1024 * 1024;

    public const long MaxCoverFileSizeBytes = 10L * 1024 * 1024;

    /// <summary>
    /// Maximum total size of entries expanded from an archive (FB2.ZIP). Guards against
    /// archive bombs: a 100 MiB upload can otherwise expand to gigabytes.
    /// </summary>
    public const long MaxExpandedArchiveBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Maximum number of entries inspected inside an uploaded archive.
    /// </summary>
    public const int MaxArchiveEntries = 512;

    /// <summary>
    /// Maximum accepted width or height of an extracted cover image, in pixels.
    /// </summary>
    public const int MaxCoverImageDimension = 10_000;

    /// <summary>
    /// Maximum number of book files parsed concurrently by BookMetadata.Grpc.
    /// Parsing is CPU- and memory-intensive; unbounded concurrency exhausts both.
    /// </summary>
    public const int MaxConcurrentMetadataParses = 4;
}
