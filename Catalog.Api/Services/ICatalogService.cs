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
        IReadOnlyCollection<Guid>? tagIds,
        Guid userId,
        CancellationToken ct = default);

    Task<PagedResponse<BookResponse>> GetUserCatalogAsync(
        GetBooksQuery query,
        Guid userId,
        CancellationToken ct = default);

    Task<ErrorOr<BookResponse>> GetBookAsync(Guid bookId, Guid userId, CancellationToken ct = default);

    Task<ErrorOr<BookFileResponse>> GetBookFileAsync(Guid bookId, Guid userId, CancellationToken ct = default);

    Task<ErrorOr<BookResponse>> UpdateBookAsync(
        Guid bookId,
        UpdateBookRequest request,
        Guid userId,
        CancellationToken ct = default);

    Task<ErrorOr<Success>> DeleteBookAsync(Guid bookId, Guid userId, CancellationToken ct = default);
}
