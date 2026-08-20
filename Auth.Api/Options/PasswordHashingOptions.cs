using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Options;

public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    [Range(1, int.MaxValue)]
    public int SaltSize { get; set; } = 16;

    [Range(1, int.MaxValue)]
    public int KeySize { get; set; } = 32;

    [Range(1000, int.MaxValue)]
    public int Iterations { get; set; } = 100_000;

    public string Algorithm { get; set; } = "SHA256";
}