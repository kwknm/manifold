using Catalog.Api.Contracts;
using ErrorOr;

namespace Catalog.Api.Services;

public interface ITagService
{
    Task<ErrorOr<TagResponse>> AddTagAsync(string name, string colorHex, Guid userId,
        CancellationToken ct = default);

    Task<List<TagResponse>> GetTagsByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<ErrorOr<TagResponse>> UpdateTagAsync(Guid tagId, string name, string colorHex, Guid userId,
        CancellationToken ct = default);

    Task<ErrorOr<Success>> DeleteTagAsync(Guid tagId, Guid userId, CancellationToken ct = default);
}
