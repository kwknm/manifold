using System.Security.Cryptography;
using Auth.Api.Options;
using Microsoft.Extensions.Options;

namespace Auth.Api.Services;

public sealed class Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    private readonly PasswordHashingOptions _options = options.Value;

    private static HashAlgorithmName GetAlgorithm(string algorithm)
    {
        return algorithm switch
        {
            "SHA256" => HashAlgorithmName.SHA256,
            "SHA384" => HashAlgorithmName.SHA384,
            "SHA512" => HashAlgorithmName.SHA512,
            _ => throw new InvalidOperationException($"Unsupported algorithm: {algorithm}")
        };
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(_options.SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _options.Iterations,
            GetAlgorithm(_options.Algorithm),
            _options.KeySize);

        return $"{_options.Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var parts = passwordHash.Split('.', 3);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            GetAlgorithm(_options.Algorithm),
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
