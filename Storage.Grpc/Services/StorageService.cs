using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Protos;
using Storage.Grpc.Database;
using Storage.Grpc.Database.Entities;
using Storage.Grpc.Options;

namespace Storage.Grpc.Services;

public sealed class StorageService(
    IAmazonS3 client,
    IOptions<SeaweedOptions> options,
    StorageDbContext dbContext,
    ILogger<StorageService> logger) : Files.FilesBase
{
    private readonly SeaweedOptions _options = options.Value;

    public override Task<UploadResponse> UploadBook(IAsyncStreamReader<FileChunk> request,
        ServerCallContext callContext)
    {
        return UploadAsync(request, _options.BooksBucketName, callContext);
    }

    public override Task<UploadResponse> UploadCover(IAsyncStreamReader<FileChunk> request,
        ServerCallContext callContext)
    {
        return UploadAsync(request, _options.CoversBucketName, callContext);
    }

    public override async Task DownloadFile(DownloadRequest request,
        IServerStreamWriter<FileChunk> responseStream, ServerCallContext callContext)
    {
        if (!Guid.TryParse(request.FileId, out var fileId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid file id."));
        }

        var storageFile = await dbContext.Files
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId, callContext.CancellationToken);

        if (storageFile is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"File '{request.FileId}' not found."));
        }

        try
        {
            using var objectResponse = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = storageFile.BucketName,
                Key = storageFile.ObjectName
            }, callContext.CancellationToken);

            const int bufferSize = 32 * 1024;
            var buffer = new byte[bufferSize];
            var totalSent = 0L;
            var first = true;

            await using var stream = objectResponse.ResponseStream;

            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), callContext.CancellationToken);
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
                    chunk.FileName = storageFile.OriginalFileName;
                    chunk.ContentType = storageFile.ContentType;
                    chunk.TotalSize = storageFile.SizeBytes;
                    first = false;
                }

                totalSent += read;
                await responseStream.WriteAsync(chunk, callContext.CancellationToken);
            }

            logger.LogInformation("Downloaded file {FileId} ({Bytes} bytes)", fileId, totalSent);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to download file {FileId}.", fileId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to download file."));
        }
    }

    public override async Task<Empty> DeleteFile(DeleteRequest request, ServerCallContext callContext)
    {
        if (!Guid.TryParse(request.FileId, out var fileId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid file id."));
        }

        var storageFile = await dbContext.Files
            .FirstOrDefaultAsync(f => f.Id == fileId, callContext.CancellationToken);

        if (storageFile is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"File '{request.FileId}' not found."));
        }

        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = storageFile.BucketName,
                Key = storageFile.ObjectName
            }, callContext.CancellationToken);

            dbContext.Files.Remove(storageFile);
            await dbContext.SaveChangesAsync(callContext.CancellationToken);

            logger.LogInformation("Deleted file {FileId} from bucket {Bucket}", fileId, storageFile.BucketName);
            return new Empty();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete file {FileId}.", fileId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to delete file."));
        }
    }

    private async Task<UploadResponse> UploadAsync(IAsyncStreamReader<FileChunk> request, string bucketName,
        ServerCallContext callContext)
    {
        var objectName = Guid.CreateVersion7().ToString("N");

        try
        {
            await EnsureBucketExistsAsync(bucketName, callContext.CancellationToken);

            string? fileName = null;
            string? contentType = null;

            await using var ms = new MemoryStream();

            await foreach (var chunk in request.ReadAllAsync())
            {
                if (fileName is null)
                {
                    fileName = chunk.FileName;
                    contentType = chunk.ContentType;
                }

                await ms.WriteAsync(chunk.Data.Memory);
            }

            ms.Position = 0;
            contentType ??= ContentType.DEFAULT.ToValue();
            fileName ??= string.Empty;

            var sizeBytes = ms.Length;

            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
                InputStream = ms,
                ContentType = contentType
            }, callContext.CancellationToken);

            var storageFile = new StorageFile
            {
                BucketName = bucketName,
                ContentType = contentType,
                ObjectName = objectName,
                OriginalFileName = TruncateFileName(fileName),
                SizeBytes = sizeBytes
            };

            await dbContext.AddAsync(storageFile);
            await dbContext.SaveChangesAsync(callContext.CancellationToken);

            return new UploadResponse
            {
                FileId = storageFile.Id.ToString()
            };
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to upload file (DB exception).");

            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = objectName
            }, CancellationToken.None);

            throw new RpcException(new Status(
                StatusCode.Internal,
                "Failed to upload file."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to upload file.");

            throw new RpcException(new Status(
                StatusCode.Internal,
                "Failed to upload file."));
        }
    }

    private async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct)
    {
        var bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(client, bucketName);

        if (bucketExists)
        {
            return;
        }

        await client.PutBucketAsync(new PutBucketRequest
        {
            BucketName = bucketName
        }, ct);
    }

    private static string TruncateFileName(string fileName, int maxLength = 512)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.Length <= maxLength)
        {
            return fileName;
        }

        var ext = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);

        var maxNameLength = Math.Max(1, maxLength - ext.Length);
        if (name.Length > maxNameLength)
        {
            name = name[..maxNameLength];
        }

        return name + ext;
    }
}