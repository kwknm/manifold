using Auth.Api.Database.Entities;

namespace Auth.Api.Services;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
