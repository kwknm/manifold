using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Storage.Grpc.Database;
using Storage.Grpc.Database.Entities;
using Storage.Grpc.Options;

namespace Storage.Grpc.Services;

public sealed class StorageService(
    IMinioClient minioClient,
    IOptions<MinioOptions> options,
    StorageDbContext dbContext,
    ILogger<StorageService> logger) : Files.FilesBase
{
    private readonly MinioOptions _options = options.Value;

    public override async Task<UploadResponse> UploadBook(IAsyncStreamReader<FileChunk> request,
        ServerCallContext callContext)
    {
        var objectName = $"{Guid.CreateVersion7():N}";

        try
        {
            await EnsureBucketExistsAsync(callContext.CancellationToken);

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
            contentType ??= "application/octet-stream";
            fileName ??= "Unknown";

            await minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName)
                .WithStreamData(ms)
                .WithObjectSize(ms.Length)
                .WithContentType(contentType));

            var storageFile = new StorageFile
            {
                BucketName = _options.BucketName,
                ContentType = contentType,
                ObjectName = objectName,
                OriginalFileName = TruncateFileName(fileName),
                SizeBytes = ms.Length
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

            await minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName));

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

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        var bucketExists = await minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_options.BucketName),
            ct);

        if (bucketExists)
        {
            return;
        }

        await minioClient.MakeBucketAsync(
            new MakeBucketArgs().WithBucket(_options.BucketName),
            ct);
    }

    private static string TruncateFileName(string fileName, int maxLength = 512)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.Length <= maxLength)
            return fileName;

        var ext = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);

        var maxNameLength = Math.Max(1, maxLength - ext.Length);
        if (name.Length > maxNameLength)
            name = name[..maxNameLength];

        return name + ext;
    }
}