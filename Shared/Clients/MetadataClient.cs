using Shared.Protos;

namespace Shared.Clients;

public class MetadataClient(Shared.Protos.Metadata.MetadataClient client) : IMetadataClient
{
    public async Task<MetadataResponse> FetchBookMetadataAsync(Guid fileId, string fileName, string contentType, CancellationToken ct = default)
    {
        var request = new MetadataRequest
        {
            FileId = fileId.ToString(),
            FileName = fileName,
            ContentType = contentType
        };

        return await client.FetchBookMetadataAsync(request, cancellationToken: ct);
    }
}