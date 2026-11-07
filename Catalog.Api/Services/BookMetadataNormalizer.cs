namespace Catalog.Api.Services;

/// <summary>
/// Brings extracted book metadata into the shape the catalog schema accepts.
/// Metadata comes from an uploaded file, so its lengths are not under the client's
/// control: rejecting a valid file because its title is long would turn a data quirk
/// into a failed upload. Values are normalized instead, and the caller logs truncations.
/// </summary>
public static class BookMetadataNormalizer
{
    public const int MaxTitleLength = 200;
    public const int MaxAuthorLength = 64;
    public const int MaxIsbnLength = 20;

    /// <summary>
    /// Page count is only meaningful when positive; otherwise it means "unknown" and is
    /// stored as null rather than as a misleading zero (contract D-12).
    /// </summary>
    public static int? NormalizePageCount(int pageCount) => pageCount > 0 ? pageCount : null;

    public static string NormalizeTitle(string? title)
    {
        var collapsed = CollapseWhitespace(title);

        if (collapsed.Length == 0)
        {
            return "Unknown";
        }

        return Truncate(collapsed, MaxTitleLength);
    }

    public static string? NormalizeIsbn(string? isbn)
    {
        var trimmed = isbn?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : Truncate(trimmed, MaxIsbnLength);
    }

    public static IReadOnlyList<string> NormalizeAuthors(IEnumerable<string?> authors)
    {
        return authors
            .Select(CollapseWhitespace)
            .Where(name => name.Length > 0)
            .Select(name => Truncate(name, MaxAuthorLength))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static bool ExceedsTitleLimit(string? title) => CollapseWhitespace(title).Length > MaxTitleLength;

    public static bool ExceedsIsbnLimit(string? isbn) => (isbn?.Trim().Length ?? 0) > MaxIsbnLength;

    public static bool ExceedsAuthorLimit(string? author) => CollapseWhitespace(author).Length > MaxAuthorLength;

    private static string CollapseWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
