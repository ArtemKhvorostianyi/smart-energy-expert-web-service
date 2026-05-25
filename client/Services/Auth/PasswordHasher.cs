using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SmartEnergyExpert.Client.Services.Auth;

public static class PasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = HashCore(password, salt);
        return $"argon2id${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split('$', 3);
        if (parts.Length != 3 || !string.Equals(parts[0], "argon2id", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = HashCore(password, salt);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] HashCore(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = 4,
            Iterations = 3,
            MemorySize = 64 * 1024
        };
        return argon2.GetBytes(HashBytes);
    }
}
