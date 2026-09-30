namespace LockGate.Core.AppLock;

/// <summary>How long an unlock lasts. Minutes: 0 = lock immediately, -1 = keep unlocked, N = N minutes.</summary>
public readonly record struct SessionPolicy(int Minutes, bool FromFocus)
{
    public const int Indefinite = -1;
    public bool IsImmediate => Minutes == 0;
    public bool IsIndefinite => Minutes < 0;
    public TimeSpan Duration => TimeSpan.FromMinutes(Math.Max(Minutes, 0));
}

/// <summary>
/// Per-app unlock sessions. Behaviour mirrors LockGate-Mac's SessionManager but is thread-safe
/// and uses a monotonic clock (TimeProvider timestamps), so changing the Windows system clock
/// cannot extend a session.
/// </summary>
public sealed class SessionManager
{
    sealed class Session
    {
        public required long StartedAt;
        public long? BlurredAt;
        public required SessionPolicy Policy;
    }

    readonly object _gate = new();
    readonly Dictionary<string, Session> _sessions = new(StringComparer.OrdinalIgnoreCase);
    readonly TimeProvider _time;

    public SessionManager(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <summary>Record a successful authentication. "Lock immediately" creates no session.</summary>
    public void CreateSession(string appId, SessionPolicy policy)
    {
        lock (_gate)
        {
            _sessions.Remove(appId);
            if (policy.IsImmediate) return;
            _sessions[appId] = new Session { StartedAt = _time.GetTimestamp(), Policy = policy };
        }
    }

    public bool HasActiveSession(string appId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(appId, out var s)) return false;
            if (IsValid(s)) return true;
            _sessions.Remove(appId);
            return false;
        }
    }

    /// <summary>App gained focus. In focus mode, returning after longer than the timeout revokes the session.</summary>
    public void AppDidFocus(string appId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(appId, out var s) || !s.Policy.FromFocus || s.Policy.IsIndefinite) return;
            if (s.BlurredAt is { } b && _time.GetElapsedTime(b) > s.Policy.Duration)
            {
                _sessions.Remove(appId);
                return;
            }
            s.BlurredAt = null;
        }
    }

    /// <summary>App lost focus. In focus mode this starts the countdown.</summary>
    public void AppDidBlur(string appId)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(appId, out var s) && s.Policy.FromFocus && !s.Policy.IsIndefinite)
                s.BlurredAt ??= _time.GetTimestamp();
        }
    }

    public void Revoke(string appId) { lock (_gate) _sessions.Remove(appId); }

    public void RevokeAll() { lock (_gate) _sessions.Clear(); }

    bool IsValid(Session s)
    {
        if (s.Policy.IsIndefinite) return true;
        if (s.Policy.FromFocus)
            return s.BlurredAt is not { } b || _time.GetElapsedTime(b) <= s.Policy.Duration;
        return _time.GetElapsedTime(s.StartedAt) < s.Policy.Duration;
    }
}
