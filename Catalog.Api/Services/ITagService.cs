using Catalog.Api.Contracts;
using Catalog.Api.Database.Entities;
using ErrorOr;

namespace Catalog.Api.Services;

public interface ITagService
{
    Task<ErrorOr<TagResponse>> AddTagAsync(string name, string colorHex, Guid userId,
        CancellationToken ct = default);
    
    Task<List<TagResponse>> GetTagsByUserIdAsync(Guid userId, CancellationToken ct = default);
    
    Task<List<Tag>> GetTagsAsync(List<Guid> tagIds, CancellationToken ct = default);
}