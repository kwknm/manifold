using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string SecretKey { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int AccessTokenLifetimeMinutes { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int RefreshTokenLifetimeDays { get; set; } = 30;
}
