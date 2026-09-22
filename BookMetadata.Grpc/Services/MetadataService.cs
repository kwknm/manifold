using BookMetadata.Parsers;
using ErrorOr;
using Grpc.Core;
using Shared;
using ContentType = Shared.ContentType;
using Metadata = Shared.Protos.Metadata;
using FileChunk = Shared.Protos.FileChunk;
using Files = Shared.Protos.Files;
using BookMetadataModel = BookMetadata.Parsers.BookMetadata;

namespace BookMetadata.Grpc.Services;

public class MetadataService(ILogger<MetadataService> logger, Files.FilesClient filesClient) : Metadata.MetadataBase
{
    private readonly List<string> _allowedEbookFormats =
        [ContentType.PDF.ToValue(), ContentType.EPUB.ToValue(), ContentType.FB2.ToValue()];

    public override async Task<Shared.Protos.MetadataResponse> FetchBookMetadata(
        IAsyncStreamReader<FileChunk> request, ServerCallContext context)
    {
        var fileName = string.Empty;
        var contentType = ContentType.DEFAULT.ToValue();
        
        using var ms = new MemoryStream();
        
        await foreach (var chunk in request.ReadAllAsync())
        {
            if (!string.IsNullOrWhiteSpace(chunk.FileName) && fileName == string.Empty)
            {
                fileName = chunk.FileName;
            }

            if (!string.IsNullOrWhiteSpace(chunk.ContentType) && contentType == ContentType.DEFAULT.ToValue())
            {
                contentType = chunk.ContentType;
            }
            
            if (!IsAllowedFormat(contentType))
            {
                logger.LogWarning("Unsupported file format: {ContentType}", contentType);
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unsupported file format: {contentType}"));
            }

            if (chunk.Data is not null)
            {
                await ms.WriteAsync(chunk.Data.Memory, context.CancellationToken);
            }
        }

        ms.Position = 0;

        try
        {
            var parser = new Parsers.BookParser();
            var result = parser.Parse(fileName ?? string.Empty, ms);

            if (result.IsError)
            {
                var error = result.FirstError;
                logger.LogWarning("No parser found for file {FileName}: {ErrorDescription}", fileName, error.Description);
                throw new RpcException(new Status(MapErrorToStatusCode(error.Type), error.Description));
            }

            var metadata = result.Value;

            var coverFileId = await UploadCoverAsync(metadata, context.CancellationToken);

            var response = new Shared.Protos.MetadataResponse
            {
                Title = metadata.Title,
                Author = metadata.Author,
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