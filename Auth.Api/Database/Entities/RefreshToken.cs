using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Database.Entities;

[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(FamilyId))]
[Index(nameof(UserId))]
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// SHA-256 hash of the refresh token value (never store the raw token).
    /// </summary>
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Groups rotated tokens for the same login session. Reuse of a revoked
    /// token in the family revokes the entire family.
    /// </summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    [MaxLength(64)]
    public string? ReplacedByTokenHash { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive => !IsRevoked && !IsExpired;
}
