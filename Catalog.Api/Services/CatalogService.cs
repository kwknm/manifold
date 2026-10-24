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
    public async Task<ErrorOr<BookResponse>> AddBookAsync(string title, string[] author, string? isbn,
        Guid fileId, Guid? coverFileId, Guid userId,
        CancellationToken ct = default)
    {
        var names = author
            .Select(a => a.Trim())
            .Where(a => a.Length > 0)
            .Distinct()
            .ToList();

        var existingAuthors = names.Count == 0
            ? []
            : await context.Authors
                .Where(a => a.UserId == userId && names.Contains(a.FullName))
                .ToListAsync(ct);

        var newAuthors = names.Except(existingAuthors.Select(a => a.FullName))
            .Select(name => new Author { FullName = name, UserId = userId })
            .ToList();

        if (newAuthors.Count > 0)
        {
            await context.Authors.AddRangeAsync(newAuthors, ct);
        }

        var book = new Book
        {
            Title = title,
            Isbn = isbn,
            FileId = fileId,
            CoverFileId = coverFileId,
            UserId = userId,
            Authors = [.. existingAuthors, .. newAuthors]
        };

        try
        {
            await context.Books.AddAsync(book, ct);
            await context.SaveChangesAsync(ct);

            var response = new BookResponse(book);

            if (book.CoverFileId.HasValue)
            {
                response.CoverUrl = (await filesClient.GetPresignedUrlAsync(book.CoverFileId.Value, ct)).Url;
            }
            
            return response;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to create book with title {Title} for user {UserId}", title, userId);
            return CatalogErrors.Book.FailedToCreate;
        }
    }

    public async Task<IList<BookResponse>> GetUserCatalogAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await context.Books
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Title)
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

        var books = rows.Select(ToResponse).ToList();

        await EnrichCoverUrlsAsync(rows, books, ct);

        return books;
    }

    public async Task<ErrorOr<BookResponse>> GetBookAsync(Guid bookId, Guid userId, CancellationToken ct = default)
    {
        var row = await context.Books
            .AsNoTracking()
            .Where(x => x.Id == bookId && x.UserId == userId)
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
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return CatalogErrors.Book.NotFound;
        }

        var response = ToResponse(row);

        if (row.CoverFileId is not null)
        {
            try
            {
                response.CoverUrl = (await filesClient.GetPresignedUrlAsync(row.CoverFileId.Value, ct)).Url;
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Failed to generate cover URL for book {BookId}", bookId);
            }
        }

        return response;
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
