using System.Security.Cryptography;

namespace RestaurantAI.Identity.Api.Security;

/// <summary>
/// PBKDF2-SHA256 password hashing. The stored format is
/// <c>pbkdf2-sha256$iterations$salt$hash</c> (base64), so the cost can be raised later
/// without invalidating existing hashes.
/// </summary>
public sealed class PasswordHasher
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    private const string Algorithm = "pbkdf2-sha256";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>Returns a problem description, or null when the password is acceptable.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            return $"Password must be at least {MinimumLength} characters.";
        }

        if (password.Length > MaximumLength)
        {
            return $"Password must be at most {MaximumLength} characters.";
        }

        return password.Any(char.IsLetter) && password.Any(c => !char.IsLetter(c))
            ? null
            : "Password must contain letters and at least one number or symbol.";
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string? stored)
    {
        var parts = stored?.Split('$');
        if (parts is not [Algorithm, var iterationText, var saltText, var hashText]
            || !int.TryParse(iterationText, out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(saltText);
        var expected = Convert.FromBase64String(hashText);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
