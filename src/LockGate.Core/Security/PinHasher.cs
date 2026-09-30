using System.Security.Cryptography;

namespace LockGate.Core.Security;

/// <summary>
/// PIN/password verifier: PBKDF2-HMAC-SHA-256 + random 16-byte salt, stored as
/// "v1$iterations$saltBase64$hashBase64". (Argon2id needs a NuGet package; PBKDF2 is the plan's approved fallback.)
/// Note: a short PIN is weak against OFFLINE guessing whatever the KDF is. The verifier must also sit in
/// DPAPI / Credential Manager storage (Phase 8) so it is not readable by other users or processes.
/// </summary>
public sealed class PinHasher
{
    public const int DefaultIterations = 600_000;
    const int SaltBytes = 16, HashBytes = 32, MinIterations = 1_000, MaxIterations = 10_000_000;
    public const int MinPinLength = 4, MaxPinLength = 64;

    readonly int _iterations;

    /// <param name="iterations">Only tests should lower this.</param>
    public PinHasher(int iterations = DefaultIterations)
    {
        if (iterations < MinIterations) throw new ArgumentOutOfRangeException(nameof(iterations));
        _iterations = iterations;
    }

    public static bool IsAcceptablePin(string? pin) =>
        pin is not null && pin.Length is >= MinPinLength and <= MaxPinLength && !string.IsNullOrWhiteSpace(pin);

    public string Hash(string pin)
    {
        if (!IsAcceptablePin(pin))
            throw new ArgumentException($"PIN must be {MinPinLength}-{MaxPinLength} characters.", nameof(pin));
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(pin, salt, _iterations);
        return $"v1${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Fails closed: any malformed or tampered verifier returns false.</summary>
    public static bool Verify(string pin, string? stored)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length > MaxPinLength || string.IsNullOrEmpty(stored)) return false;
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "v1") return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations is < MinIterations or > MaxIterations) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltBytes || expected.Length != HashBytes) return false;
            return CryptographicOperations.FixedTimeEquals(Derive(pin, salt, iterations), expected);
        }
        catch (FormatException) { return false; }
    }

    static byte[] Derive(string pin, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
