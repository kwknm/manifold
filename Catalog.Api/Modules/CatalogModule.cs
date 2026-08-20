using Carter;
using Catalog.Api.Clients;
using Catalog.Api.Contracts;
using Catalog.Api.Extensions;
using Catalog.Api.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;

namespace Catalog.Api.Modules;

public class CatalogModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/books");
            // .RequireAuthorization();

        group.MapPost<AddBookRequest>("/", HandleAddBookAsync)
            .DisableAntiforgery();
    }

    private async Task<IResult> HandleAddBookAsync(
        [FromForm] AddBookRequest request,
        ICatalogService service,
        HttpContext httpContext,
        IFilesClient filesClient,
        CancellationToken ct)
    {
        // var userId = httpContext.User.GetUserId();
        var userId = Guid.Empty;
        
        Guid fileId;

        try
        {
            fileId = await filesClient.UploadBookAsync(request.File, ct);
        }
        catch (RpcException ex)
        {
            return ex.ToProblemDetails();
        }

        var result = await service.AddBookAsync(
            request.Title, 
            request.Author, 
            request.Isbn, 
            request.Tags,
            fileId,
            userId, 
            ct);
        
        return result.MapToResponseAsync(response => Results.Created("/books", response));
    }
}