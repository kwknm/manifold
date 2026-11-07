using BookMetadata.Parsers;
using ErrorOr;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using Shared;
using Metadata = Shared.Protos.Metadata;
using Files = Shared.Protos.Files;
using MetadataRequest = Shared.Protos.MetadataRequest;
using MetadataResponse = Shared.Protos.MetadataResponse;
using DownloadRequest = Shared.Protos.DownloadRequest;
using FileChunk = Shared.Protos.FileChunk;
using BookMetadataModel = BookMetadata.Parsers.BookMetadata;

namespace BookMetadata.Grpc.Services;

public class MetadataService(
    ILogger<MetadataService> logger,
    Files.FilesClient filesClient,
    ParserConcurrencyLimiter concurrencyLimiter) : Metadata.MetadataBase
{
    public override async Task<MetadataResponse> FetchBookMetadata(
        MetadataRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.FileId, out var fileId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid file id."));
        }

        var fileName = request.FileName;
        var contentType = request.ContentType;

        // Content-Type and file name arrive as independent client values and the parser is
        // selected by extension, so a mismatch would run the wrong parser over the file.
        var format = BookFormats.Resolve(fileName, contentType);
        if (format is null)
        {
            logger.LogWarning(
                "Rejected book metadata request: unsupported or inconsistent format {ContentType} / {FileName}",
                contentType, fileName);

            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                $"Unsupported file format. Expected one of: {BookFormats.DescribeAllowed()}."));
        }

        using var lease = await concurrencyLimiter.AcquireAsync(context.CancellationToken);

        return await ParseWithBudgetAsync(fileId, format, context);
    }

    private async Task<MetadataResponse> ParseWithBudgetAsync(
        Guid fileId,
        BookFormats.Format format,
        ServerCallContext context)
    {
        await using var stream = await DownloadBookAsync(fileId, context.CancellationToken);

        try
        {
            var parser = new BookParser();
            var result = parser.Parse(format, stream);

            if (result.IsError)
            {
                var error = result.FirstError;

                // The parser failure is described in the log with full detail; the caller
                // receives only a stable public message (see PublicErrorFor).
                logger.LogWarning(
                    "Failed to parse book {FileId} ({Format}): {ErrorCode} {ErrorDescription}",
                    fileId, format.Kind, error.Code, error.Description);

                throw new RpcException(new Status(
                    MapErrorToStatusCode(error.Type),
                    PublicErrorFor(error.Type)));
            }

            var metadata = result.Value;

            var coverFileId = await UploadCoverAsync(metadata, context.CancellationToken);

            return new MetadataResponse
            {
                Title = metadata.Title,
                Authors = { metadata.Authors },
                Isbn = metadata.Isbn,
                PageCount = metadata.PageCount,
                CoverFileId = coverFileId
            };
        }
        catch (RpcException)
        {
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Metadata extraction was cancelled."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse metadata for file {FileId}", fileId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to extract book metadata."));
        }
    }

    /// <summary>
    /// Maps an internal parser failure to a message safe to return to callers. Parser
    /// exception text is logged but never echoed, as it leaks library internals.
    /// </summary>
    private static string PublicErrorFor(ErrorType errorType) => errorType switch
    {
        ErrorType.NotFound => "The file is not a readable book of the declared format.",
        ErrorType.Validation => "The book file is invalid.",
        _ => "Failed to extract book metadata."
    };

    /// <summary>
    /// Streams the book from Storage into a size-capped temp file. The cap is enforced against
    /// bytes actually received, so an oversized object is rejected; spooling to disk instead of
    /// a <see cref="MemoryStream"/> keeps the memory of the service flat while several parses
    /// (<see cref="UploadLimits.MaxConcurrentMetadataParses"/>) hold a book each.
    /// </summary>
    private async Task<FileStream> DownloadBookAsync(Guid fileId, CancellationToken ct)
    {
        using var call = filesClient.DownloadFile(new DownloadRequest { FileId = fileId.ToString() }, cancellationToken: ct);

        var spool = SpoolFile.Create();

        try
        {
            long totalBytes = 0;

            await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
            {
                if (chunk.Data is null || chunk.Data.Length == 0)
                {
                    continue;
                }

                totalBytes += chunk.Data.Length;

                if (totalBytes > UploadLimits.MaxBookFileSizeBytes)
                {
                    throw new RpcException(new Status(
                        StatusCode.ResourceExhausted,
                        $"File exceeds the maximum allowed size of {UploadLimits.MaxBookFileSizeBytes} bytes."));
                }

                await spool.WriteAsync(chunk.Data.Memory, ct);
            }

            if (totalBytes == 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "The stored file is empty."));
            }

            spool.Position = 0;

            return spool;
        }
        catch
        {
            await spool.DisposeAsync();
            throw;
        }
    }

    private async Task<string> UploadCoverAsync(BookMetadataModel metadata, CancellationToken ct)
    {
        var coverBytes = metadata.CoverImage;

        if (coverBytes is not { Length: > 0 })
        {
            return string.Empty;
        }

        if (!CoverMimeDetector.IsRaster(metadata.CoverContentType))
        {
            return string.Empty;
        }

        // Signature bytes are not proof that the image decodes. Validation re-encodes the
        // image and rejects anything oversized, so a corrupt cover never reaches storage.
        var validated = CoverValidator.Validate(coverBytes, metadata.CoverContentType);
        if (validated is null)
        {
            logger.LogWarning("Discarded unusable cover image ({Bytes} bytes)", coverBytes.Length);
            return string.Empty;
        }

        coverBytes = validated.Bytes;
        var coverContentType = validated.ContentType;

        const int bufferSize = 64 * 1024;
        var fileName = $"cover.{CoverMimeDetector.GetFileExtension(coverContentType)}";

        using var call = filesClient.UploadCover(cancellationToken: ct);
        await using var coverStream = new MemoryStream(coverBytes);

        var buffer = new byte[bufferSize];
        var first = true;

        while (true)
        {
            var read = await coverStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0)
            {
                break;
            }

            var chunk = new FileChunk
            {
                Data = ByteString.CopyFrom(buffer, 0, read)
            };

            if (first)
            {
                chunk.FileName = fileName;
                chunk.ContentType = coverContentType;
                chunk.TotalSize = coverBytes.Length;
                first = false;
            }

            await call.RequestStream.WriteAsync(chunk, ct);
        }

        await call.RequestStream.CompleteAsync();
        var response = await call.ResponseAsync;

        logger.LogInformation("Cover uploaded: {FileName} ({Bytes} bytes)", fileName, coverBytes.Length);
        return response.FileId;
    }

    private static StatusCode MapErrorToStatusCode(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.NotFound => StatusCode.NotFound,
            ErrorType.Validation => StatusCode.InvalidArgument,
            ErrorType.Conflict => StatusCode.AlreadyExists,
            ErrorType.Unauthorized => StatusCode.Unauthenticated,
            ErrorType.Forbidden => StatusCode.PermissionDenied,
            _ => StatusCode.Internal
        };
    }
}
