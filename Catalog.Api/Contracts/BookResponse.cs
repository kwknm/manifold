namespace Catalog.Api.Contracts;

public sealed record BookResponse(
    Guid Id,
    string Title,
    string? Author,
    string? Isbn,
    List<string> Tags,
    Guid UserId);