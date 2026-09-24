using Catalog.Api.Contracts;
using ErrorOr;

namespace Catalog.Api.Services;

public interface ICatalogService
{
    Task<ErrorOr<BookResponse>> AddBookAsync(
        string title, 
        string[] authors, 
        string? isbn,
        Guid fileId,
        Guid? coverFileId,
        Guid userId,
        CancellationToken cancellationToken = default);
}