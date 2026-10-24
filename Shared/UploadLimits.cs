namespace Shared;

public static class UploadLimits
{
    public const long MaxBookFileSizeBytes = 100L * 1024 * 1024;

    /// <summary>
    /// Maximum request body size. Includes multipart overhead on top of the file itself.
    /// </summary>
    public const long MaxRequestSizeBytes = MaxBookFileSizeBytes + 10L * 1024 * 1024;
}
