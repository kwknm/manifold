using BookMetadata.Parsers;
using ErrorOr;
using Google.Protobuf.Collections;
using Grpc.Core;
using Shared;
using ContentType = Shared.ContentType;
using Metadata = Shared.Protos.Metadata;
using Files = Shared.Protos.Files;
using MetadataRequest = Shared.Protos.MetadataRequest;
using MetadataResponse = Shared.Protos.MetadataResponse;
using DownloadRequest = Shared.Protos.DownloadRequest;
using FileChunk = Shared.Protos.FileChunk;
using BookMetadataModel = BookMetadata.Parsers.BookMetadata;

namespace BookMetadata.Grpc.Services;

public class MetadataService(ILogger<MetadataService> logger, Files.FilesClient filesClient) : Metadata.MetadataBase
{
    private readonly List<string> _allowedEbookFormats =
        [ContentType.PDF.ToValue(), ContentType.EPUB.ToValue(), ContentType.FB2.ToValue()];

    public override async Task<MetadataResponse> FetchBookMetadata(
        MetadataRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.FileId, out var fileId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid file id."));
        }

        var fileName = request.FileName;
        var contentType = request.ContentType;

        if (!IsAllowedFormat(contentType))
        {
            logger.LogWarning("Unsupported file format: {ContentType}", contentType);
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unsupported file format: {contentType}"));
        }

        await using var stream = await DownloadBookAsync(fileId, context.CancellationToken);

        try
        {
            var parser = new BookParser();
            var result = parser.Parse(fileName ?? string.Empty, stream);

            if (result.IsError)
            {
                var error = result.FirstError;
                logger.LogWarning("No parser found for file {FileName}: {ErrorDescription}", fileName, error.Description);
                throw new RpcException(new Status(MapErrorToStatusCode(error.Type), error.Description));
            }

            var metadata = result.Value;

            var coverFileId = await UploadCoverAsync(metadata, context.CancellationToken);
            
            var response = new MetadataResponse
            {
                Title = metadata.Title,
                Authors = { metadata.Authors },
                Isbn = metadata.Isbn,
                PageCount = metadata.PageCount,
                CoverFileId = coverFileId
            };

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse metadata for file {FileName}", fileName);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to parse metadata"));
        }
    }
    
    private bool IsAllowedFormat(string contentType)
    {
        return _allowedEbookFormats.Contains(contentType);
    }

    private async Task<MemoryStream> DownloadBookAsync(Guid fileId, CancellationToken ct)
    {
        using var call = filesClient.DownloadFile(new DownloadRequest { FileId = fileId.ToString() }, cancellationToken: ct);

        var ms = new MemoryStream();

        await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
        {
            if (chunk.Data is not null)
            {
                await ms.WriteAsync(chunk.Data.Memory, ct);
            }
        }

        ms.Position = 0;
        return ms;
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

        const int bufferSize = 64 * 1024;
        var fileName = $"cover.{CoverMimeDetector.GetFileExtension(metadata.CoverContentType)}";

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
                Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, read)
            };

            if (first)
            {
                chunk.FileName = fileName;
                chunk.ContentType = metadata.CoverContentType;
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