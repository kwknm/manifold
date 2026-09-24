using Shared.Protos;
using Grpc.Core;
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

    public async Task<DownloadedFile> DownloadFileAsync(Guid fileId, CancellationToken ct = default)
    {
        using var call = client.DownloadFile(new DownloadRequest { FileId = fileId.ToString() }, cancellationToken: ct);

        var ms = new MemoryStream();
        var fileName = string.Empty;
        var contentType = ContentType.DEFAULT.ToValue();
        var totalSize = 0L;
        var first = true;

        await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
        {
            if (first)
            {
                fileName = chunk.FileName;
                if (!string.IsNullOrEmpty(chunk.ContentType))
                {
                    contentType = chunk.ContentType;
                }

                totalSize = chunk.TotalSize;
                first = false;
            }

            if (chunk.Data is not null)
            {
                await ms.WriteAsync(chunk.Data.Memory, ct);
            }
        }

        ms.Position = 0;
        return new DownloadedFile(ms, fileName, contentType, totalSize);
    }

    public async Task DeleteFileAsync(Guid fileId, CancellationToken ct = default)
    {
        await client.DeleteFileAsync(new DeleteRequest { FileId = fileId.ToString() }, cancellationToken: ct);
    }
}
