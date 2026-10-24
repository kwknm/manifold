using Catalog.Api.Contracts;
using Catalog.Api.Database;
using Catalog.Api.Database.Entities;
using Catalog.Api.Errors;
using ErrorOr;
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
        var books = await context.Books.Where(x => x.UserId == userId)
            .Select(b => new BookResponse(b)).ToListAsync(ct);

        if (books.Count == 0)
        {
            return [];
        }
        
        // todo Enrich books with cover urls

        return books;
    }
}