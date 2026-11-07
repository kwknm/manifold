using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Options;

public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    /// <summary>
    /// Algorithms accepted by <see cref="Services.Pbkdf2PasswordHasher"/>. The algorithm is
    /// stored inside the hash string, so changing this setting keeps existing passwords verifiable.
    /// </summary>
    public static readonly string[] SupportedAlgorithms = ["SHA256", "SHA384", "SHA512"];

    [Range(16, 1024)]
    public int SaltSize { get; set; } = 16;

    [Range(16, 1024)]
    public int KeySize { get; set; } = 32;

    [Range(10_000, 10_000_000)]
    public int Iterations { get; set; } = 100_000;

    [Required]
    public string Algorithm { get; set; } = "SHA256";
}
