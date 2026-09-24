using Catalog.Api.Contracts;
using Catalog.Api.Database;
using Catalog.Api.Database.Entities;
using Catalog.Api.Errors;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class CatalogService(ILogger<CatalogService> logger, CatalogDbContext context, ITagService tagService)
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

            return new BookResponse(book.Id, book.Title, [.. book.Authors.Select(a => a.FullName)], book.Isbn,
                book.CoverFileId,
                [.. book.Tags.Select(t => new TagResponse(t))],
                book.UserId);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to create book with title {Title} for user {UserId}", title, userId);
            return CatalogErrors.Book.FailedToCreate;
        }
    }
}