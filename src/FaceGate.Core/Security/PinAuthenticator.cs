namespace FaceGate.Core.Security;

/// <summary>Where the PIN verifier lives. In memory for tests; DPAPI / Credential Manager on Windows (Phase 8).</summary>
public interface IPinStore
{
    string? Load();
    void Save(string verifier);
}

public enum PinResult { Success, Incorrect, LockedOut, NotConfigured }

public readonly record struct PinAttempt(PinResult Result, TimeSpan RetryAfter = default);

/// <summary>PIN fallback = verifier check + throttling. Never logs or retains the PIN.</summary>
public sealed class PinAuthenticator
{
    readonly IPinStore _store;
    readonly PinHasher _hasher;
    readonly AttemptLimiter _limiter;

    public PinAuthenticator(IPinStore store, PinHasher? hasher = null, AttemptLimiter? limiter = null)
    {
        _store = store;
        _hasher = hasher ?? new PinHasher();
        _limiter = limiter ?? new AttemptLimiter();
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_store.Load());

    public void SetPin(string pin) => _store.Save(_hasher.Hash(pin));

    public PinAttempt Verify(string pin)
    {
        var stored = _store.Load();
        if (string.IsNullOrEmpty(stored)) return new(PinResult.NotConfigured);

        // While locked out, don't even run the KDF (also keeps a flood of guesses cheap for us).
        if (!_limiter.TryBegin(out var wait)) return new(PinResult.LockedOut, wait);

        if (PinHasher.Verify(pin, stored))
        {
            _limiter.Reset();
            return new(PinResult.Success);
        }
        return new(PinResult.Incorrect, _limiter.RecordFailure());
    }
}
