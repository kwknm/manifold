using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Options;

/// <summary>
/// Fixed-window limits applied per client IP so a single abusive client cannot exhaust
/// the budget of everyone else. Behind a proxy, forwarded headers must be enabled for the
/// real client address to be visible (see MVP-CONTRACT §2).
/// </summary>
public sealed class AuthRateLimitingOptions
{
    public const string SectionName = "AuthRateLimiting";

    /// <summary>
    /// Kill switch for local debugging and integration tests that intentionally hammer
    /// the auth endpoints. Keep enabled outside development.
    /// </summary>
    public bool Enabled { get; set; } = true;

    [Range(1, 100_000)]
    public int LoginPermitLimit { get; set; } = 10;

    [Range(1, 100_000)]
    public int RegisterPermitLimit { get; set; } = 5;

    [Range(1, 100_000)]
    public int RefreshPermitLimit { get; set; } = 30;

    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
