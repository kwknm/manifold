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
        var trimmedName = name.Trim();

        if (await NameExistsAsync(trimmedName, userId, excludeTagId: null, ct))
        {
            return CatalogErrors.Tag.AlreadyExists;
        }

        var tag = new Tag
        {
            Name = trimmedName,
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
        return await context.Tags
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(t => new TagResponse
            {
                Id = t.Id,
                Name = t.Name,
                ColorHex = t.ColorHex
            })
            .ToListAsync(ct);
    }

    public async Task<ErrorOr<TagResponse>> UpdateTagAsync(Guid tagId, string name, string colorHex,
        Guid userId, CancellationToken ct = default)
    {
        var tag = await context.Tags
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId, ct);

        if (tag is null)
        {
            return CatalogErrors.Tag.NotFound;
        }

        var trimmedName = name.Trim();

        if (await NameExistsAsync(trimmedName, userId, tagId, ct))
        {
            return CatalogErrors.Tag.AlreadyExists;
        }

        tag.Name = trimmedName;
        tag.ColorHex = colorHex;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update tag {TagId} for user {UserId}", tagId, userId);
            return CatalogErrors.Tag.FailedToUpdate;
        }

        return new TagResponse(tag);
    }

    public async Task<ErrorOr<Success>> DeleteTagAsync(Guid tagId, Guid userId, CancellationToken ct = default)
    {
        var tag = await context.Tags
            .FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId, ct);

        if (tag is null)
        {
            return CatalogErrors.Tag.NotFound;
        }

        context.Tags.Remove(tag);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete tag {TagId} for user {UserId}", tagId, userId);
            return CatalogErrors.Tag.FailedToDelete;
        }

        return Result.Success;
    }

    private Task<bool> NameExistsAsync(string name, Guid userId, Guid? excludeTagId, CancellationToken ct)
    {
        return context.Tags
            .AnyAsync(t => t.UserId == userId
                           && t.Name == name
                           && (excludeTagId == null || t.Id != excludeTagId), ct);
    }
}
