namespace Catalog.Api.Contracts;

public sealed record GetBooksQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid[]? TagIds { get; init; }
}
