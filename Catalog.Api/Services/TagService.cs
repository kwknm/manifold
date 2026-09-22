using Catalog.Api.Contracts;
using Catalog.Api.Database;
using Catalog.Api.Database.Entities;
using Catalog.Api.Errors;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public class TagService(ILogger<TagService> logger, CatalogDbContext context) : ITagService
{
    public async Task<ErrorOr<TagResponse>> AddTagAsync(string name, string colorHex, Guid userId,
        CancellationToken ct = default)
    {
        var tag = new Tag
        {
            Name = name,
            ColorHex = colorHex,
            UserId = userId
        };

        try
        {
            await context.Tags.AddAsync(tag, ct);
            await context.SaveChangesAsync(ct);

            return new TagResponse(tag);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to create tag with name {Name} for user {UserId}", name, userId);
            return CatalogErrors.Tag.FailedToCreate;
        }
    }

    public async Task<List<TagResponse>> GetTagsByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var tags = await context.Tags.Where(x => x.UserId == userId)
            .Select(t => new TagResponse(t)).ToListAsync(ct);

        return tags;
    }

    public async Task<List<Tag>> GetTagsAsync(List<Guid> tagIds, CancellationToken ct = default)
    {
        return await context.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync(ct);
    }
}