namespace Shared;

/// <summary>
/// The single source of truth for book formats supported by the MVP: which MIME types
/// and file extensions are accepted, and how they relate to each other.
/// </summary>
/// <remarks>
/// Content-Type and file extension arrive from the client as independent values, so they
/// must be validated together: an accepted MIME with a mismatched extension otherwise
/// selects the wrong parser and produces nonsense instead of a clean rejection.
/// </remarks>
public static class BookFormats
{
    public sealed record Format(
        string ContentType,
        IReadOnlyList<string> Extensions,
        FormatKind Kind);

    public enum FormatKind
    {
        EPUB,
        FB2,
        PDF
    }

    public static readonly Format Epub = new(
        ContentType.EPUB.ToValue(),
        [".epub"],
        FormatKind.EPUB);

    public static readonly Format Fb2 = new(
        ContentType.FB2.ToValue(),
        [".fb2", ".fb2.zip"],
        FormatKind.FB2);

    public static readonly Format Pdf = new(
        ContentType.PDF.ToValue(),
        [".pdf"],
        FormatKind.PDF);

    public static readonly IReadOnlyList<Format> All = [Epub, Fb2, Pdf];

    public static readonly IReadOnlyList<string> AllowedContentTypes =
        [.. All.Select(f => f.ContentType)];

    /// <summary>
    /// Normalizes a media type for comparison. Media types are case-insensitive; the
    /// parameters ("; charset=...") are irrelevant for format detection.
    /// </summary>
    public static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var separatorIndex = contentType.IndexOf(';');
        var mediaType = separatorIndex >= 0 ? contentType[..separatorIndex] : contentType;

        return mediaType.Trim().ToLowerInvariant();
    }

    public static bool IsAllowedContentType(string? contentType)
    {
        var normalized = NormalizeContentType(contentType);
        return AllowedContentTypes.Any(allowed =>
            string.Equals(allowed, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasAllowedExtension(string? fileName)
    {
        var normalized = NormalizeFileName(fileName);
        return All.Any(format => format.Extensions.Any(extension => normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Resolves the declared format when the MIME type and the file extension agree.
    /// Returns <c>null</c> when the extension is unknown or contradicts the MIME type.
    /// </summary>
    public static Format? Resolve(string? fileName, string? contentType)
    {
        var normalizedContentType = NormalizeContentType(contentType);

        var byContentType = All.FirstOrDefault(format =>
            string.Equals(format.ContentType, normalizedContentType, StringComparison.OrdinalIgnoreCase));

        if (byContentType is null)
        {
            return null;
        }

        var normalizedFileName = NormalizeFileName(fileName);

        return byContentType.Extensions.Any(extension =>
            normalizedFileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            ? byContentType
            : null;
    }

    public static string DescribeAllowed()
        => string.Join(", ", All.Select(f => $"{f.ContentType} ({string.Join(" / ", f.Extensions)})"));

    private static string NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        // Some clients send a full path; only the name matters for extension matching.
        var lastSeparator = fileName.LastIndexOfAny(['/', '\\']);
        var name = lastSeparator >= 0 ? fileName[(lastSeparator + 1)..] : fileName;

        return name.Trim().ToLowerInvariant();
    }
}
