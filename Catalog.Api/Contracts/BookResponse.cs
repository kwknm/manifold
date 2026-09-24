namespace Catalog.Api.Contracts;

public sealed record BookResponse(
    Guid Id,
    string Title,
    string[] Authors,
    string? Isbn,
    Guid? CoverFileId,
    List<TagResponse> Tags,
    Guid UserId);