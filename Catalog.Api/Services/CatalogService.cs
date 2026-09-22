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
    public async Task<ErrorOr<BookResponse>> AddBookAsync(string title, string? author, string? isbn, List<Guid> tagIds,
        Guid fileId, Guid? coverFileId, int pageCount, Guid userId,
        CancellationToken ct = default)
    {
        var fetchedTags = await tagService.GetTagsAsync(tagIds, ct);

        var book = new Book
        {
            Title = title,
            Author = author,
            Isbn = isbn,
            FileId = fileId,
            CoverFileId = coverFileId,
            PageCount = pageCount,
            UserId = userId,
            Tags = fetchedTags
        };

        try
        {
            await context.Books.AddAsync(book, ct);
            await context.SaveChangesAsync(ct);

            return new BookResponse(book.Id, book.Title, book.Author, book.Isbn, book.PageCount,
                book.CoverFileId,
                book.Tags.Select(t => new TagResponse(t)).ToList(),
                book.UserId);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to create book with title {Title} for user {UserId}", title, userId);
            return CatalogErrors.Book.FailedToCreate;
        }
    }
}