using Catalog.Api.Contracts;
using ErrorOr;

namespace Catalog.Api.Services;

public interface ICatalogService
{
    Task<ErrorOr<BookResponse>> AddBookAsync(
        string title, 
        string? author, 
        string? isbn, 
        List<Guid> tagIds,
        Guid fileId,
        Guid? coverFileId,
        int pageCount,
        Guid userId,
        CancellationToken cancellationToken = default);
}