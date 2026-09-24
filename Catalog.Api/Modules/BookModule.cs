using Carter;
using Shared.Clients;
using Catalog.Api.Contracts;
using Catalog.Api.Extensions;
using Catalog.Api.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;
using Shared.Protos;

namespace Catalog.Api.Modules;

public class BookModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/books")
            .RequireAuthorization();

        group.MapPost<AddBookRequest>("/", HandleAddBookAsync)
            .DisableAntiforgery();
    }

    private async Task<IResult> HandleAddBookAsync(
        [FromForm] AddBookRequest request,
        ICatalogService service,
        HttpContext httpContext,
        IFilesClient filesClient,
        IMetadataClient metadataClient,
        CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        Guid fileId;
        try
        {
            fileId = await filesClient.UploadBookAsync(request.File, ct);
        }
        catch (RpcException ex)
        {
            return ex.ToProblemDetails();
        }

        MetadataResponse metadata;
        try
        {
            metadata = await metadataClient.FetchBookMetadataAsync(request.File, ct);
        }
        catch (RpcException ex)
        {
            return ex.ToProblemDetails();
        }

        var title = string.IsNullOrWhiteSpace(metadata.Title) ? request.Title : metadata.Title;
        var author = string.IsNullOrWhiteSpace(metadata.Author) ? request.Author : metadata.Author;
        var isbn = string.IsNullOrWhiteSpace(metadata.Isbn) ? request.Isbn : metadata.Isbn;
        Guid.TryParse(metadata.CoverFileId, out var coverFileId);
        
        var result = await service.AddBookAsync(
            title,
            author,
            isbn,
            request.TagIds,
            fileId,
            coverFileId,
            userId,
            ct);

        return result.MapToResponseAsync(response => Results.Created("/books", response));
    }
}