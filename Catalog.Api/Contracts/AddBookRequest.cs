namespace Catalog.Api.Contracts;

public sealed record AddBookRequest(
    string Title,
    string? Author,
    string? Isbn,
    IFormFile File,
    List<Guid> TagIds);