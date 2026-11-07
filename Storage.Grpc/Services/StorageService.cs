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
        return UploadAsync(request, _options.BooksBucketName, UploadLimits.MaxBookFileSizeBytes, callContext);
    }

    public override Task<UploadResponse> UploadCover(IAsyncStreamReader<FileChunk> request,
        ServerCallContext callContext)
    {
        return UploadAsync(request, _options.CoversBucketName, UploadLimits.MaxCoverFileSizeBytes, callContext);
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
            // Deleting an already-deleted file is not an error: callers retry cleanup and
            // must be able to do so idempotently.
            logger.LogInformation("Delete requested for unknown file {FileId}; treating as already deleted", fileId);
            return new Empty();
        }

        // The row is removed first: the database is the source of truth for which files
        // should exist, and a row without an object is a broken reference that no cleanup
        // can detect from the catalog side. The reverse order leaves exactly that.
        dbContext.Files.Remove(storageFile);

        try
        {
            await dbContext.SaveChangesAsync(callContext.CancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to remove file metadata for {FileId}.", fileId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to delete file."));
        }

        // Object removal is best-effort: a failure leaves an orphaned object, which is
        // inert (unreferenced, not user-visible) and preferable to failing a delete that
        // already committed its database change.
        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = storageFile.BucketName,
                Key = storageFile.ObjectName
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Removed metadata for {FileId} but failed to delete object {ObjectName} in bucket {Bucket}",
                fileId, storageFile.ObjectName, storageFile.BucketName);
        }

        logger.LogInformation("Deleted file {FileId} from bucket {Bucket}", fileId, storageFile.BucketName);
        return new Empty();
    }

    public override async Task<PresignedUrlResponse> GetPresignedUrl(PresignedUrlRequest request,
        ServerCallContext callContext)
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
            var response = await BuildPresignedUrlAsync(storageFile);

            logger.LogInformation("Generated presigned URL for file {FileId}", fileId);

            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to generate presigned URL for file {FileId}.", fileId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to generate presigned URL."));
        }
    }

    public override async Task<PresignedUrlsResponse> GetPresignedUrls(PresignedUrlsRequest request,
        ServerCallContext callContext)
    {
        var fileIds = new List<Guid>();

        foreach (var rawFileId in request.FileIds)
        {
            if (!Guid.TryParse(rawFileId, out var fileId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid file id '{rawFileId}'."));
            }

            fileIds.Add(fileId);
        }

        var storageFiles = await dbContext.Files
            .AsNoTracking()
            .Where(f => fileIds.Contains(f.Id))
            .ToListAsync(callContext.CancellationToken);

        var response = new PresignedUrlsResponse();

        try
        {
            foreach (var storageFile in storageFiles)
            {
                response.Urls[storageFile.Id.ToString()] = await BuildPresignedUrlAsync(storageFile);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to generate presigned URLs for {Count} files.", fileIds.Count);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to generate presigned URLs."));
        }

        logger.LogInformation("Generated {Count} presigned URLs", response.Urls.Count);
        return response;
    }

    private async Task<PresignedUrlResponse> BuildPresignedUrlAsync(StorageFile storageFile)
    {
        var url = await client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = storageFile.BucketName,
            Key = storageFile.ObjectName,
            Verb = HttpVerb.GET,
            Protocol = _options.PresignedUrlUseHttps ? Protocol.HTTPS : Protocol.HTTP,
            Expires = DateTime.UtcNow.AddSeconds(_options.PresignedUrlExpirySeconds)
        });

        return new PresignedUrlResponse
        {
            Url = url,
            FileName = storageFile.OriginalFileName,
            ContentType = storageFile.ContentType
        };
    }

    private async Task<UploadResponse> UploadAsync(IAsyncStreamReader<FileChunk> request, string bucketName,
        long maxSizeBytes, ServerCallContext callContext)
    {
        var objectName = string.Empty;
        var objectUploaded = false;

        try
        {
            await EnsureBucketExistsAsync(bucketName, callContext.CancellationToken);

            string? fileName = null;
            string? contentType = null;

            // The declared TotalSize is a client hint and is deliberately not trusted:
            // the limit is enforced against bytes actually received. The buffer is capped
            // at maxSizeBytes, so a lying or runaway client cannot exhaust memory.
            await using var ms = new MemoryStream(capacity: (int)Math.Min(maxSizeBytes, 4L * 1024 * 1024));
            long totalBytes = 0;

            await foreach (var chunk in request.ReadAllAsync(callContext.CancellationToken))
            {
                if (fileName is null)
                {
                    fileName = chunk.FileName;
                    contentType = chunk.ContentType;
                }

                var data = chunk.Data.Memory;
                if (data.Length == 0)
                {
                    continue;
                }

                totalBytes += data.Length;

                if (totalBytes > maxSizeBytes)
                {
                    logger.LogWarning(
                        "Rejected upload to bucket {Bucket}: exceeded {Limit} bytes",
                        bucketName, maxSizeBytes);

                    throw new RpcException(new Status(
                        StatusCode.ResourceExhausted,
                        $"File exceeds the maximum allowed size of {maxSizeBytes} bytes."));
                }

                await ms.WriteAsync(data, callContext.CancellationToken);
            }

            if (totalBytes == 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "File is empty."));
            }

            ms.Position = 0;
            contentType ??= ContentType.DEFAULT.ToValue();
            fileName ??= string.Empty;

            objectName = Guid.CreateVersion7().ToString("N") + Path.GetExtension(fileName);
            var sizeBytes = ms.Length;

            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
                InputStream = ms,
                ContentType = contentType
            }, callContext.CancellationToken);

            objectUploaded = true;

            var storageFile = new StorageFile
            {
                BucketName = bucketName,
                ContentType = contentType,
                ObjectName = objectName,
                OriginalFileName = TruncateFileName(fileName),
                SizeBytes = sizeBytes
            };

            await dbContext.AddAsync(storageFile, callContext.CancellationToken);
            await dbContext.SaveChangesAsync(callContext.CancellationToken);

            return new UploadResponse
            {
                FileId = storageFile.Id.ToString()
            };
        }
        catch (RpcException)
        {
            await TryDeleteUploadedObjectAsync(bucketName, objectName, objectUploaded);
            throw;
        }
        catch (OperationCanceledException)
        {
            await TryDeleteUploadedObjectAsync(bucketName, objectName, objectUploaded);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to upload file to bucket {Bucket}.", bucketName);

            await TryDeleteUploadedObjectAsync(bucketName, objectName, objectUploaded);

            throw new RpcException(new Status(
                StatusCode.Internal,
                "Failed to upload file."));
        }
    }

    /// <summary>
    /// Removes an object that was written to S3 but whose metadata was not persisted.
    /// Runs for every failure after the put (DB error, cancellation, unexpected error),
    /// not just <see cref="DbUpdateException"/>, otherwise the object stays orphaned.
    /// </summary>
    private async Task TryDeleteUploadedObjectAsync(string bucketName, string objectName, bool objectUploaded)
    {
        if (!objectUploaded || string.IsNullOrEmpty(objectName))
        {
            return;
        }

        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = objectName
            });
        }
        catch (Exception cleanupException)
        {
            // The original failure is more important than a failed cleanup; leaving the
            // object is recoverable, losing the original error is not.
            logger.LogWarning(cleanupException,
                "Failed to clean up orphaned object {ObjectName} in bucket {Bucket}", objectName, bucketName);
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