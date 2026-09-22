using Carter;
using Catalog.Api.Contracts;
using Catalog.Api.Services;
using Shared.Extensions;

namespace Catalog.Api.Modules;

public class TagModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tags")
            .RequireAuthorization();

        group.MapPost<AddTagRequest>("/", HandleAddTagAsync);
    }

    private async Task<IResult> HandleAddTagAsync(
        AddTagRequest request,
        ITagService service,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        var result = await service.AddTagAsync(
            request.Name,
            request.ColorHex,
            userId,
            ct);

        return result.MapToResponseAsync(response => Results.Created("/tags", response));
    }
}