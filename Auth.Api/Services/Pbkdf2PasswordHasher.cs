using System.Security.Cryptography;
using Auth.Api.Options;
using Microsoft.Extensions.Options;

namespace Auth.Api.Services;

public sealed class Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    private const char Separator = '.';

    private readonly PasswordHashingOptions _options = options.Value;

    private static HashAlgorithmName GetAlgorithm(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA256" => HashAlgorithmName.SHA256,
            "SHA384" => HashAlgorithmName.SHA384,
            "SHA512" => HashAlgorithmName.SHA512,
            _ => throw new InvalidOperationException($"Unsupported algorithm: {algorithm}")
        };
    }

    /// <summary>
    /// Format: <c>algorithm.iterations.base64(salt).base64(hash)</c>.
    /// The algorithm is stored per hash so rotating <see cref="PasswordHashingOptions.Algorithm"/>
    /// does not invalidate existing passwords.
    /// </summary>
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

        return string.Join(
            Separator,
            _options.Algorithm.ToUpperInvariant(),
            _options.Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var parts = passwordHash.Split(Separator);
        if (parts.Length != 4)
        {
            return false;
        }

        if (!PasswordHashingOptions.SupportedAlgorithms.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expectedHash.Length == 0)
        {
            return false;
        }

        // Uses the algorithm recorded in the hash, not the currently configured one.
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            GetAlgorithm(parts[0]),
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
