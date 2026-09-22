using Shared.Protos;
using Microsoft.AspNetCore.Http;

namespace Shared.Clients;

public interface IMetadataClient
{
    Task<MetadataResponse> FetchBookMetadataAsync(IFormFile file, CancellationToken ct = default);
}
