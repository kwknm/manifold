using Auth.Api.Contracts;
using Auth.Api.Database;
using Auth.Api.Database.Entities;
using Auth.Api.Errors;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace Auth.Api.Services;

public sealed class AuthService(
    AuthDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<ErrorOr<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.UserName.Trim();

        var emailExists = await dbContext.Users
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (emailExists)
        {
            return AuthErrors.User.EmailAlreadyExists;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = userName,
            PasswordHash = passwordHasher.Hash(request.Password)
        };

        dbContext.Users.Add(user);

        var response = IssueTokens(user, familyId: Guid.NewGuid());
        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task<ErrorOr<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return AuthErrors.Credentials.Invalid;
        }

        var response = IssueTokens(user, familyId: Guid.NewGuid());
        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task<ErrorOr<AuthResponse>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingToken = await dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return AuthErrors.RefreshToken.Invalid;
        }

        // Refresh token reuse detection: a revoked token presented again
        // means the token was likely stolen — revoke the whole family.
        if (existingToken.IsRevoked)
        {
            await RevokeTokenFamilyAsync(existingToken.FamilyId, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return AuthErrors.RefreshToken.ReuseDetected;
        }

        if (existingToken.IsExpired)
        {
            existingToken.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return AuthErrors.RefreshToken.Expired;
        }

        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var newTokenHash = tokenService.HashRefreshToken(rawRefreshToken);
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenLifetimeDays);

        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.ReplacedByTokenHash = newTokenHash;

        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingToken.UserId,
            TokenHash = newTokenHash,
            FamilyId = existingToken.FamilyId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = refreshExpiresAt
        };

        dbContext.RefreshTokens.Add(replacement);

        var access = tokenService.CreateAccessToken(existingToken.User);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AuthResponse(
            access.Token,
            rawRefreshToken,
            access.ExpiresAt,
            refreshExpiresAt);
    }

    public async Task<ErrorOr<Success>> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);

        var existingToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return Result.Success;
        }

        if (!existingToken.IsRevoked)
        {
            existingToken.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success;
    }

    private AuthResponse IssueTokens(User user, Guid familyId)
    {
        var access = tokenService.CreateAccessToken(user);
        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenLifetimeDays);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(rawRefreshToken),
            FamilyId = familyId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = refreshExpiresAt
        };

        dbContext.RefreshTokens.Add(refreshToken);

        return new AuthResponse(
            access.Token,
            rawRefreshToken,
            access.ExpiresAt,
            refreshExpiresAt);
    }

    private async Task RevokeTokenFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        await dbContext.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.RevokedAt, now),
                cancellationToken);
    }
}
