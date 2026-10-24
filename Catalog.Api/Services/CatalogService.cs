using Catalog.Api.Contracts;
using Catalog.Api.Database;
using Catalog.Api.Database.Entities;
using Catalog.Api.Errors;
using ErrorOr;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Clients;

namespace Catalog.Api.Services;

public class CatalogService(ILogger<CatalogService> logger, CatalogDbContext context, IFilesClient filesClient)
    : ICatalogService
{
    private const int MaxPageSize = 100;
    private const int MaxPage = 10_000;

    public async Task<ErrorOr<BookResponse>> AddBookAsync(string title, string[] author, string? isbn,
        Guid fileId, Guid? coverFileId, IReadOnlyCollection<Guid>? tagIds, Guid userId,
        CancellationToken ct = default)
    {
        var tags = new List<Tag>();

        if (tagIds is { Count: > 0 })
        {
            var tagsResult = await ResolveTagsAsync(tagIds, userId, ct);

            if (tagsResult.IsError)
            {
                return tagsResult.Errors;
            }

            tags = tagsResult.Value;
        }

        var authors = await ResolveAuthorsAsync(author, userId, ct);

        var book = new Book
        {
            Title = title,
            Isbn = isbn,
            FileId = fileId,
            CoverFileId = coverFileId,
            UserId = userId,
            Authors = authors,
            Tags = tags
        };

        try
        {
            await context.Books.AddAsync(book, ct);
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to create book with title {Title} for user {UserId}", title, userId);
            return CatalogErrors.Book.FailedToCreate;
        }

        return await BuildResponseAsync(book, ct);
    }

    public async Task<PagedResponse<BookResponse>> GetUserCatalogAsync(GetBooksQuery query, Guid userId,
        CancellationToken ct = default)
    {
        var page = Math.Clamp(query.Page, 1, MaxPage);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var books = context.Books
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";

            books = books.Where(x => EF.Functions.ILike(x.Title, pattern)
                                     || x.Authors.Any(a => EF.Functions.ILike(a.FullName, pattern)));
        }

        if (query.TagIds is { Length: > 0 })
        {
            var tagIds = query.TagIds.Distinct().ToArray();

            books = books.Where(x => x.Tags.Any(t => tagIds.Contains(t.Id)));
        }

        var totalCount = await books.CountAsync(ct);

        var rows = await books
            .AsSplitQuery()
            .OrderBy(x => x.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BookRow
            {
                Id = x.Id,
                Title = x.Title,
                Authors = x.Authors.Select(a => a.FullName).ToList(),
                Isbn = x.Isbn,
                Tags = x.Tags.Select(t => new TagResponse
                {
                    Id = t.Id,
                    Name = t.Name,
                    ColorHex = t.ColorHex
                }).ToList(),
                CoverFileId = x.CoverFileId,
                UserId = x.UserId
            })
            .ToListAsync(ct);

        var items = rows.Select(ToResponse).ToList();

        await EnrichCoverUrlsAsync(rows, items, ct);

        return new PagedResponse<BookResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ErrorOr<BookResponse>> GetBookAsync(Guid bookId, Guid userId, CancellationToken ct = default)
    {
        var book = await context.Books
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Authors)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == bookId && x.UserId == userId, ct);

        if (book is null)
        {
            return CatalogErrors.Book.NotFound;
        }

        return await BuildResponseAsync(book, ct);
    }

    public async Task<ErrorOr<BookFileResponse>> GetBookFileAsync(Guid bookId, Guid userId,
        CancellationToken ct = default)
    {
        var fileId = await context.Books
            .AsNoTracking()
            .Where(x => x.Id == bookId && x.UserId == userId)
            .Select(x => (Guid?)x.FileId)
            .FirstOrDefaultAsync(ct);

        if (fileId is null)
        {
            return CatalogErrors.Book.NotFound;
        }

        var presignedUrl = await filesClient.GetPresignedUrlAsync(fileId.Value, ct);

        return new BookFileResponse(presignedUrl.Url, presignedUrl.FileName, presignedUrl.ContentType);
    }

    public async Task<ErrorOr<BookResponse>> UpdateBookAsync(Guid bookId, UpdateBookRequest request,
        Guid userId, CancellationToken ct = default)
    {
        var book = await context.Books
            .AsSplitQuery()
            .Include(x => x.Authors)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == bookId && x.UserId == userId, ct);

        if (book is null)
        {
            return CatalogErrors.Book.NotFound;
        }

        if (request.TagIds is not null)
        {
            var tagsResult = await ResolveTagsAsync(request.TagIds, userId, ct);

            if (tagsResult.IsError)
            {
                return tagsResult.Errors;
            }

            book.Tags.Clear();

            foreach (var tag in tagsResult.Value)
            {
                book.Tags.Add(tag);
            }
        }

        if (request.Authors is not null)
        {
            var authors = await ResolveAuthorsAsync(request.Authors, userId, ct);

            book.Authors.Clear();

            foreach (var author in authors)
            {
                book.Authors.Add(author);
            }
        }

        if (request.Title is not null)
        {
            book.Title = request.Title.Trim();
        }

        if (request.Isbn is not null)
        {
            book.Isbn = string.IsNullOrWhiteSpace(request.Isbn) ? null : request.Isbn.Trim();
        }

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update book {BookId} for user {UserId}", bookId, userId);
            return CatalogErrors.Book.FailedToUpdate;
        }

        return await BuildResponseAsync(book, ct);
    }

    public async Task<ErrorOr<Success>> DeleteBookAsync(Guid bookId, Guid userId, CancellationToken ct = default)
    {
        var book = await context.Books
            .FirstOrDefaultAsync(x => x.Id == bookId && x.UserId == userId, ct);

        if (book is null)
        {
            return CatalogErrors.Book.NotFound;
        }

        context.Books.Remove(book);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete book {BookId} for user {UserId}", bookId, userId);
            return CatalogErrors.Book.FailedToDelete;
        }

        // The database is the source of truth: once the row is gone, storage cleanup
        // is best-effort. Use CancellationToken.None so an aborted request does not
        // leave orphaned files behind.
        await TryDeleteFileAsync(book.FileId);

        if (book.CoverFileId.HasValue)
        {
            await TryDeleteFileAsync(book.CoverFileId.Value);
        }

        return Result.Success;
    }

    private async Task<ErrorOr<List<Tag>>> ResolveTagsAsync(IReadOnlyCollection<Guid> tagIds, Guid userId,
        CancellationToken ct)
    {
        var ids = tagIds.Distinct().ToArray();

        var tags = await context.Tags
            .Where(t => t.UserId == userId && ids.Contains(t.Id))
            .ToListAsync(ct);

        return tags.Count == ids.Length
            ? tags
            : CatalogErrors.Book.InvalidTags;
    }

    private async Task<List<Author>> ResolveAuthorsAsync(IEnumerable<string> names, Guid userId,
        CancellationToken ct)
    {
        var normalized = names
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct()
            .ToList();

        if (normalized.Count == 0)
        {
            return [];
        }

        var existingAuthors = await context.Authors
            .Where(a => a.UserId == userId && normalized.Contains(a.FullName))
            .ToListAsync(ct);

        var newAuthors = normalized
            .Except(existingAuthors.Select(a => a.FullName))
            .Select(name => new Author { FullName = name, UserId = userId })
            .ToList();

        if (newAuthors.Count > 0)
        {
            await context.Authors.AddRangeAsync(newAuthors, ct);
        }

        return [.. existingAuthors, .. newAuthors];
    }

    private async Task<BookResponse> BuildResponseAsync(Book book, CancellationToken ct)
    {
        var response = new BookResponse(book);

        if (book.CoverFileId.HasValue)
        {
            try
            {
                response.CoverUrl = (await filesClient.GetPresignedUrlAsync(book.CoverFileId.Value, ct)).Url;
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Failed to generate cover URL for book {BookId}", book.Id);
            }
        }

        return response;
    }

    private async Task TryDeleteFileAsync(Guid fileId)
    {
        try
        {
            await filesClient.DeleteFileAsync(fileId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete storage file {FileId}", fileId);
        }
    }

    private async Task EnrichCoverUrlsAsync(IReadOnlyList<BookRow> rows, IReadOnlyList<BookResponse> books,
        CancellationToken ct)
    {
        var coverFileIds = rows
            .Where(r => r.CoverFileId.HasValue)
            .Select(r => r.CoverFileId!.Value)
            .Distinct()
            .ToList();

        if (coverFileIds.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<Guid, PresignedUrlResult> urls;

        try
        {
            urls = await filesClient.GetPresignedUrlsAsync(coverFileIds, ct);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "Failed to generate cover URLs for user catalog");
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].CoverFileId is { } coverFileId && urls.TryGetValue(coverFileId, out var url))
            {
                books[i].CoverUrl = url.Url;
            }
        }
    }

    private static BookResponse ToResponse(BookRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        Authors = row.Authors,
        Isbn = row.Isbn,
        Tags = row.Tags,
        UserId = row.UserId
    };

    private sealed class BookRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public List<string> Authors { get; init; } = [];
        public string? Isbn { get; init; }
        public List<TagResponse> Tags { get; init; } = [];
        public Guid? CoverFileId { get; init; }
        public Guid UserId { get; init; }
    }
}
