namespace Catalog.Api.Contracts;

/// <summary>
/// Partial update of a book. A <c>null</c> property means "leave unchanged";
/// an empty <see cref="Authors"/> or <see cref="TagIds"/> array clears the collection.
/// </summary>
public sealed record UpdateBookRequest
{
    public string? Title { get; init; }
    public string[]? Authors { get; init; }
    public string? Isbn { get; init; }
    public Guid[]? TagIds { get; init; }
}
