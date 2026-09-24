using Carter;
using Shared.Clients;
using Catalog.Api.Contracts;
using Catalog.Api.Extensions;
using Catalog.Api.Services;
using FluentValidation;
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
        IValidator<AddBookRequest> validator,
        ICatalogService service,
        HttpContext httpContext,
        IFilesClient filesClient,
        IMetadataClient metadataClient,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

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
            metadata = await metadataClient.FetchBookMetadataAsync(
                fileId, request.File.FileName, request.File.ContentType, ct);
        }
        catch (RpcException ex)
        {
            return ex.ToProblemDetails();
        }

        var title = !string.IsNullOrEmpty(metadata.Title) ? metadata.Title : "Unknown";
        var author = metadata.Authors.ToArray();
        var isbn = !string.IsNullOrEmpty(metadata.Isbn) ? metadata.Isbn : null;

        var coverFileId = Guid.Parse(metadata.CoverFileId);

        var result = await service.AddBookAsync(
            title,
            author,
            isbn,
            fileId,
            coverFileId,
            userId,
            ct);

        return result.MapToResponseAsync(response => Results.Created("/books", response));
    }
}