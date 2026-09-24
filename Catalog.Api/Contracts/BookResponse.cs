namespace Catalog.Api.Contracts;

public sealed record BookResponse(
    Guid Id,
    string Title,
    string? Author,
    string? Isbn,
    Guid? CoverFileId,
    List<TagResponse> Tags,
    Guid UserId);