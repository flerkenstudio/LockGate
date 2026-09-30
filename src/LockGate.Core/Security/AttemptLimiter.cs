namespace LockGate.Core.Security;

/// <summary>
/// Failed-attempt throttle. The first <see cref="FreeAttempts"/> failures are free (LockGate-Mac uses 5),
/// then lockouts start at 30s and double per further failure, capped at 15 minutes.
/// State is in memory only: restarting the process resets it. Persisting it is a Phase 10 item.
/// </summary>
public sealed class AttemptLimiter
{
    public const int FreeAttempts = 5;
    static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30), MaxDelay = TimeSpan.FromMinutes(15);

    readonly object _gate = new();
    readonly TimeProvider _time;
    int _failures;
    long? _lockedUntil;

    public AttemptLimiter(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <summary>true if an attempt may proceed now; otherwise how long to wait.</summary>
    public bool TryBegin(out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            retryAfter = TimeSpan.Zero;
            if (_lockedUntil is not { } until) return true;
            var now = _time.GetTimestamp();
            if (now >= until) return true;
            retryAfter = TimeSpan.FromSeconds((double)(until - now) / _time.TimestampFrequency);
            return false;
        }
    }

    public TimeSpan RecordFailure()
    {
        lock (_gate)
        {
            _failures++;
            if (_failures <= FreeAttempts) return TimeSpan.Zero;
            var exp = Math.Min(_failures - FreeAttempts - 1, 20);
            var delay = TimeSpan.FromTicks(Math.Min(BaseDelay.Ticks * (1L << exp), MaxDelay.Ticks));
            _lockedUntil = _time.GetTimestamp() + (long)(delay.TotalSeconds * _time.TimestampFrequency);
            return delay;
        }
    }

    public void Reset() { lock (_gate) { _failures = 0; _lockedUntil = null; } }
}
