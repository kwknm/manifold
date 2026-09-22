using Carter;
using FluentValidation;
using FluentValidation.Results;
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

        group.MapPost("/", HandleAddBookAsync)
            .DisableAntiforgery();
    }

    private async Task<IResult> HandleAddBookAsync(
        [FromForm] string? Title,
        [FromForm] string? Author,
        [FromForm] string? Isbn,
        [FromForm] List<Guid>? TagIds,
        IFormFile? File,
        ICatalogService service,
        HttpContext httpContext,
        IFilesClient filesClient,
        IMetadataClient metadataClient,
        IValidator<AddBookRequest> validator,
        CancellationToken ct)
    {
        var request = new AddBookRequest(Title, Author, Isbn, File, TagIds);

        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var userId = httpContext.User.GetUserId();

        Guid fileId;
        try
        {
            fileId = await filesClient.UploadBookAsync(request.File!, ct);
        }
        catch (RpcException ex)
        {
            return ex.ToProblemDetails();
        }

        MetadataResponse metadata;
        try
        {
            metadata = await metadataClient.FetchBookMetadataAsync(request.File!, ct);
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
            request.TagIds ?? [],
            fileId,
            coverFileId,
            metadata.PageCount,
            userId,
            ct);

        return result.MapToResponseAsync(response => Results.Created("/books", response));
    }
}