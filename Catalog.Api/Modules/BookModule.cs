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
            .WithName("AddBook")
            .DisableAntiforgery();

        group.MapGet("/my", GetMyCatalogAsync)
            .WithName("GetMyCatalog");

        group.MapGet("/{id:guid}", GetBookAsync)
            .WithName("GetBook");

        group.MapGet("/{id:guid}/file", GetBookFileAsync)
            .WithName("GetBookFile");

        group.MapMethods("/{id:guid}", [HttpMethods.Patch],
                (Guid id, UpdateBookRequest request, IValidator<UpdateBookRequest> validator,
                    ICatalogService catalogService, HttpContext httpContext, CancellationToken ct) =>
                    UpdateBookAsync(id, request, validator, catalogService, httpContext, ct))
            .WithName("UpdateBook");

        group.MapDelete("/{id:guid}", DeleteBookAsync)
            .WithName("DeleteBook");
    }

    private async Task<IResult> HandleAddBookAsync(
        [FromForm] AddBookRequest request,
        IValidator<AddBookRequest> validator,
        ICatalogService service,
        HttpContext httpContext,
        IFilesClient filesClient,
        IMetadataClient metadataClient,
        ILogger<BookModule> logger,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var userId = httpContext.User.GetUserId();
        var tagIds = ParseTagIds(request.TagIds);

        Guid fileId;
        try
        {
            fileId = await filesClient.UploadBookAsync(request.File, ct);
        }
        catch (RpcException ex)
        {
            return ex.ToProblemDetails();
        }

        Guid? coverFileId = null;

        try
        {
            MetadataResponse metadata;
            try
            {
                metadata = await metadataClient.FetchBookMetadataAsync(
                    fileId, request.File.FileName, request.File.ContentType, ct);
            }
            catch (RpcException ex)
            {
                await DeleteFilesAsync(filesClient, [fileId, coverFileId], logger);
                return ex.ToProblemDetails();
            }

            // A book without a usable cover is a valid outcome, not an error.
            if (Guid.TryParse(metadata.CoverFileId, out var parsedCoverId))
            {
                coverFileId = parsedCoverId;
            }

            var title = !string.IsNullOrEmpty(metadata.Title) ? metadata.Title : "Unknown";
            var authors = metadata.Authors.ToArray();
            var isbn = !string.IsNullOrEmpty(metadata.Isbn) ? metadata.Isbn : null;

            var result = await service.AddBookAsync(
                title,
                authors,
                isbn,
                fileId,
                coverFileId,
                tagIds,
                userId,
                ct);

            if (result.IsError)
            {
                await DeleteFilesAsync(filesClient, [fileId, coverFileId], logger);
                return result.MapToResponseAsync(Results.Ok);
            }

            return Results.Created($"/books/{result.Value.Id}", result.Value);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await DeleteFilesAsync(filesClient, [fileId, coverFileId], logger);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add a book for user {UserId}", userId);
            await DeleteFilesAsync(filesClient, [fileId, coverFileId], logger);

            return Results.Problem(
                title: "Failed to add book",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetMyCatalogAsync(
        [AsParameters] GetBooksQuery query,
        ICatalogService catalogService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        var result = await catalogService.GetUserCatalogAsync(query, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBookAsync(Guid id, ICatalogService catalogService,
        HttpContext httpContext, CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        var result = await catalogService.GetBookAsync(id, userId, ct);
        return result.MapToResponseAsync(Results.Ok);
    }

    private static async Task<IResult> GetBookFileAsync(Guid id, ICatalogService catalogService,
        HttpContext httpContext, CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        try
        {
            var result = await catalogService.GetBookFileAsync(id, userId, ct);
            return result.MapToResponseAsync(Results.Ok);
        }
        catch (RpcException ex)
        {
            return ex.ToProblemDetails();
        }
    }

    private static async Task<IResult> UpdateBookAsync(Guid id, UpdateBookRequest request,
        IValidator<UpdateBookRequest> validator, ICatalogService catalogService,
        HttpContext httpContext, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var userId = httpContext.User.GetUserId();

        var result = await catalogService.UpdateBookAsync(id, request, userId, ct);
        return result.MapToResponseAsync(Results.Ok);
    }

    private static async Task<IResult> DeleteBookAsync(Guid id, ICatalogService catalogService,
        HttpContext httpContext, CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        var result = await catalogService.DeleteBookAsync(id, userId, ct);
        return result.MapToResponseAsync(_ => Results.NoContent());
    }

    private static IReadOnlyCollection<Guid>? ParseTagIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();
    }

    private static async Task DeleteFilesAsync(IFilesClient filesClient, IEnumerable<Guid?> fileIds,
        ILogger logger)
    {
        foreach (var fileId in fileIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
        {
            try
            {
                await filesClient.DeleteFileAsync(fileId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete file {FileId} during upload rollback", fileId);
            }
        }
    }
}
