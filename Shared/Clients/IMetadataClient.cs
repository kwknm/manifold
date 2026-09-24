using Shared.Protos;

namespace Shared.Clients;

public interface IMetadataClient
{
    Task<MetadataResponse> FetchBookMetadataAsync(Guid fileId, string fileName, string contentType, CancellationToken ct = default);
}
