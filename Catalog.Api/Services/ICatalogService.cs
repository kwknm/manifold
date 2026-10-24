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
        CancellationToken ct = default);

    Task<IList<BookResponse>> GetUserCatalogAsync(Guid userId, CancellationToken ct = default);

    Task<ErrorOr<BookResponse>> GetBookAsync(Guid bookId, Guid userId, CancellationToken ct = default);

    Task<ErrorOr<BookFileResponse>> GetBookFileAsync(Guid bookId, Guid userId, CancellationToken ct = default);
}
