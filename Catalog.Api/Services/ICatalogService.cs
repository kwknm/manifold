using Catalog.Api.Contracts;
using ErrorOr;

namespace Catalog.Api.Services;

public interface ICatalogService
{
    Task<ErrorOr<BookResponse>> AddBookAsync(
        string title, 
        string? author, 
        string? isbn, 
        List<string> tags,
        Guid fileId,
        Guid userId,
        CancellationToken cancellationToken = default);
}