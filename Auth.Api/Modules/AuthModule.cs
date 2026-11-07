using System.Security.Claims;
using Auth.Api.Contracts;
using Auth.Api.Extensions;
using Auth.Api.Services;
using Carter;
using Shared.Extensions;

namespace Auth.Api.Modules;

public class AuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost<RegisterRequest>("/register", HandleRegisterAsync)
            .RequireRateLimiting(RateLimitingExtensions.RegisterPolicy)
            .AllowAnonymous();

        app.MapPost<LoginRequest>("/login", HandleLoginAsync)
            .RequireRateLimiting(RateLimitingExtensions.LoginPolicy)
            .AllowAnonymous();

        app.MapPost<RefreshRequest>("/refresh", HandleRefreshAsync)
            .RequireRateLimiting(RateLimitingExtensions.RefreshPolicy)
            .AllowAnonymous();

        // Logout is only meaningful with a valid refresh token and revokes nothing else,
        // so it is not rate limited: a throttled logout would keep a session alive.
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