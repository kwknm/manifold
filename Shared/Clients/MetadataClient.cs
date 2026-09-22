using Shared.Protos;
using Microsoft.AspNetCore.Http;

namespace Shared.Clients;

public class MetadataClient(Shared.Protos.Metadata.MetadataClient client) : IMetadataClient
{
    public async Task<MetadataResponse> FetchBookMetadataAsync(IFormFile file, CancellationToken ct = default)
    {
        const int bufferSize = 32 * 1024;

        using var call = client.FetchBookMetadata(cancellationToken: ct);
        await using var stream = file.OpenReadStream();

        var buffer = new byte[bufferSize];
        var first = true;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0)
            {
                break;
            }

            var chunk = new FileChunk
            {
                Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, read)
            };

            if (first)
            {
                chunk.FileName = file.FileName;
                chunk.ContentType = file.ContentType;
                chunk.TotalSize = file.Length;
                first = false;
            }

            await call.RequestStream.WriteAsync(chunk, ct);
        }

        await call.RequestStream.CompleteAsync();
        return await call.ResponseAsync;
    }
}
