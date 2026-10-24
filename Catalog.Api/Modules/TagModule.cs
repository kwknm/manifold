using Carter;
using Catalog.Api.Contracts;
using Catalog.Api.Services;
using FluentValidation;
using Shared.Extensions;

namespace Catalog.Api.Modules;

public class TagModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tags")
            .RequireAuthorization();

        group.MapGet("/", GetTagsAsync)
            .WithName("GetTags");

        group.MapPost<AddTagRequest>("/", AddTagAsync)
            .WithName("AddTag");

        group.MapPut<UpdateTagRequest>("/{id:guid}", UpdateTagAsync)
            .WithName("UpdateTag");

        group.MapDelete("/{id:guid}", DeleteTagAsync)
            .WithName("DeleteTag");
    }

    private static async Task<IResult> GetTagsAsync(
        ITagService service,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        var tags = await service.GetTagsByUserIdAsync(userId, ct);
        return Results.Ok(tags);
    }

    private static async Task<IResult> AddTagAsync(
        AddTagRequest request,
        IValidator<AddTagRequest> validator,
        ITagService service,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var userId = httpContext.User.GetUserId();

        var result = await service.AddTagAsync(
            request.Name,
            request.ColorHex,
            userId,
            ct);

        return result.MapToResponseAsync(response => Results.Created($"/tags/{response.Id}", response));
    }

    private static async Task<IResult> UpdateTagAsync(
        Guid id,
        UpdateTagRequest request,
        IValidator<UpdateTagRequest> validator,
        ITagService service,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var userId = httpContext.User.GetUserId();

        var result = await service.UpdateTagAsync(id, request.Name, request.ColorHex, userId, ct);
        return result.MapToResponseAsync(Results.Ok);
    }

    private static async Task<IResult> DeleteTagAsync(
        Guid id,
        ITagService service,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        var result = await service.DeleteTagAsync(id, userId, ct);
        return result.MapToResponseAsync(_ => Results.NoContent());
    }
}
