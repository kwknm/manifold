using Shared.Protos;
using Microsoft.AspNetCore.Http;

namespace Shared.Clients;

public class FilesClient(Files.FilesClient client) : IFilesClient
{
    public async Task<Guid> UploadBookAsync(IFormFile file, CancellationToken ct = default)
    {
        const int bufferSize = 32 * 1024;

        using var call = client.UploadBook(cancellationToken: ct);
        await using var stream = file.OpenReadStream();

        var buffer = new byte[bufferSize];
        int read;
        var first = true;

        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
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
        var response = await call.ResponseAsync;
        return new Guid(response.FileId);
    }
}
