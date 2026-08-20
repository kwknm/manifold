using System.Security.Claims;
using Auth.Api.Contracts;
using Auth.Api.Services;
using Carter;
using Shared.Extensions;

namespace Auth.Api.Modules;

public class AuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost<RegisterRequest>("/register", HandleRegisterAsync)
            .AllowAnonymous();

        app.MapPost<LoginRequest>("/login", HandleLoginAsync)
            .AllowAnonymous();

        app.MapPost<RefreshRequest>("/refresh", HandleRefreshAsync)
            .AllowAnonymous();

        app.MapPost<LogoutRequest>("/logout", HandleLogoutAsync)
            .AllowAnonymous();

        app.MapGet("/me", HandleMeAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> HandleRegisterAsync(
        RegisterRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.MapToResponseAsync(response => Results.Created("/api/auth/me", response));
    }

    private static async Task<IResult> HandleLoginAsync(
        LoginRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result.MapToResponseAsync(Results.Ok);
    }

    private static async Task<IResult> HandleRefreshAsync(
        RefreshRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);
        return result.MapToResponseAsync(Results.Ok);
    }

    private static async Task<IResult> HandleLogoutAsync(
        LogoutRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(request, cancellationToken);
        return result.MapToResponseAsync(_ => Results.NoContent());
    }

    private static IResult HandleMeAsync(ClaimsPrincipal user)
    {
        var id = user.GetUserId();
        var email = user.FindFirstValue(ClaimTypes.Email);
        var userName = user.FindFirstValue(ClaimTypes.Name);

        return Results.Ok(new
        {
            id,
            email,
            userName
        });
    }
}