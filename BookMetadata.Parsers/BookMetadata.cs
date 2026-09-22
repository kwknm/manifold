namespace BookMetadata.Parsers;

public class BookMetadata
{
    public string Title { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = [];
    // Backwards-compatible single Author property
    public string Author => Authors.Count > 0 ? string.Join(", ", Authors) : string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public byte[]? CoverImage { get; set; }
    public string CoverContentType { get; set; } = string.Empty;
    public bool HasCover => CoverImage is { Length: > 0 };
    public int? Year { get; set; }
}
