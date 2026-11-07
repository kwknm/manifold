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
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>
    /// EF Core default name for the unique index on Users.Email (see the Initial migration).
    /// </summary>
    private const string UserEmailIndexName = "IX_Users_Email";

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<ErrorOr<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.UserName.Trim();

        // Fast path: gives a clean conflict without touching the unique index.
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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolationOn(UserEmailIndexName))
        {
            // Lost the race against a concurrent registration of the same email;
            // the unique index is the source of truth.
            logger.LogInformation("Concurrent registration for email {Email} rejected by unique index", email);
            return AuthErrors.User.EmailAlreadyExists;
        }

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
        var now = DateTimeOffset.UtcNow;

        // Read the token together with its owner. No transaction is needed here: the
        // rotation itself is guarded by the atomic consume below.
        var existingToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return AuthErrors.RefreshToken.Invalid;
        }

        // A revoked token presented again means the token was likely stolen.
        // This must be checked before the atomic consume, otherwise the second
        // concurrent request would see a revoked token and report plain invalid.
        if (existingToken.IsRevoked)
        {
            await RevokeTokenFamilyAsync(existingToken.FamilyId, cancellationToken);
            return AuthErrors.RefreshToken.ReuseDetected;
        }

        if (existingToken.IsExpired)
        {
            await RevokeExpiredTokenAsync(existingToken.Id, now, cancellationToken);
            return AuthErrors.RefreshToken.Expired;
        }

        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var newTokenHash = tokenService.HashRefreshToken(rawRefreshToken);
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays);

        // Atomically claim the token: only a request that still sees RevokedAt == null
        // may rotate it. Concurrent refreshes therefore cannot both succeed.
        var claimed = await dbContext.RefreshTokens
            .Where(t => t.Id == existingToken.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.ReplacedByTokenHash, newTokenHash),
                cancellationToken);

        if (claimed == 0)
        {
            // Another request consumed this token first. Treat it as replay.
            await RevokeTokenFamilyAsync(existingToken.FamilyId, cancellationToken);
            return AuthErrors.RefreshToken.ReuseDetected;
        }

        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingToken.UserId,
            TokenHash = newTokenHash,
            FamilyId = existingToken.FamilyId,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt
        };

        dbContext.RefreshTokens.Add(replacement);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The consume above already revoked the presented token, so the caller must
            // retry with a fresh login rather than reuse a token that no longer rotates.
            logger.LogError(ex, "Failed to persist rotated refresh token for user {UserId}", existingToken.UserId);
            return AuthErrors.RefreshToken.Invalid;
        }

        var access = tokenService.CreateAccessToken(existingToken.User);

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

        // Revocation is idempotent and never reports whether the token existed.
        await dbContext.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow),
                cancellationToken);

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

    private async Task RevokeExpiredTokenAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens
            .Where(t => t.Id == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.RevokedAt, now),
                cancellationToken);
    }
}
