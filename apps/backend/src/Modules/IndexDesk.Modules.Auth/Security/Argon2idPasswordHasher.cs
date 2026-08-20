using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace IndexDesk.Modules.Auth.Security;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySizeKb = 65536; // 64 MB
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 4;

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySizeKb,
        };

        var hash = argon2.GetBytes(HashSize);

        var saltBase64 = Convert.ToBase64String(salt);
        var hashBase64 = Convert.ToBase64String(hash);

        return $"$argon2id$v=19$m={MemorySizeKb},t={Iterations},p={DegreeOfParallelism}${saltBase64}${hashBase64}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        try
        {
            var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            // Expected segments: ["argon2id", "v=19", "m=65536,t=3,p=4", "<salt-base64>", "<hash-base64>"]
            if (parts.Length != 5)
            {
                return false;
            }

            var algorithm = parts[0];
            if (!string.Equals(algorithm, "argon2id", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var memory = MemorySizeKb;
            var iterations = Iterations;
            var parallelism = DegreeOfParallelism;

            var paramTokens = parts[2].Split(',');
            foreach (var token in paramTokens)
            {
                var kv = token.Split('=');
                if (kv.Length == 2)
                {
                    if (kv[0] == "m" && int.TryParse(kv[1], out var m))
                    {
                        memory = m;
                    }
                    else if (kv[0] == "t" && int.TryParse(kv[1], out var t))
                    {
                        iterations = t;
                    }
                    else if (kv[0] == "p" && int.TryParse(kv[1], out var p))
                    {
                        parallelism = p;
                    }
                }
            }

            var salt = Convert.FromBase64String(parts[3]);
            var expectedHash = Convert.FromBase64String(parts[4]);

            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = parallelism,
                Iterations = iterations,
                MemorySize = memory,
            };

            var actualHash = argon2.GetBytes(expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
