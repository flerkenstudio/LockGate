using LockGate.Core.Models;

namespace LockGate.Core.AppLock;

public enum LockDecision
{
    /// <summary>Not a protected app (or its rule is disabled).</summary>
    NotProtected,
    /// <summary>Protected, but a valid session / pause / cooldown lets it through.</summary>
    Allow,
    /// <summary>Protected and locked: block it and show the authentication window.</summary>
    RequireAuthentication,
}

/// <summary>
/// The decision brain, free of any Windows API so it can be unit-tested. The Windows layer feeds it
/// process/focus/session events and acts on the returned <see cref="LockDecision"/>.
/// </summary>
public sealed class AppLockEngine
{
    /// <summary>Stops a re-lock loop right after unlock in "lock immediately" mode (same idea as LockGate-Mac).</summary>
    public static readonly TimeSpan UnlockCooldown = TimeSpan.FromSeconds(10);

    readonly object _gate = new();
    readonly ProtectedAppRegistry _registry;
    readonly SessionManager _sessions;
    readonly TimeProvider _time;
    readonly Dictionary<string, long> _recentUnlock = new(StringComparer.OrdinalIgnoreCase);
    long? _pausedUntil;

    public AppLockEngine(ProtectedAppRegistry registry, SessionManager sessions, TimeProvider? time = null)
    {
        _registry = registry;
        _sessions = sessions;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>A protected-app process launched or came to the foreground.</summary>
    public LockDecision OnAppActivated(ProcessInfo process)
    {
        var app = _registry.Find(process);
        if (app is null || !app.Enabled) return LockDecision.NotProtected;

        lock (_gate)
        {
            if (IsPaused()) return LockDecision.Allow;
            _sessions.AppDidFocus(app.AppId);
            if (_sessions.HasActiveSession(app.AppId)) return LockDecision.Allow;
            if (_recentUnlock.TryGetValue(app.AppId, out var t) && _time.GetElapsedTime(t) < UnlockCooldown)
                return LockDecision.Allow;
            return LockDecision.RequireAuthentication;
        }
    }

    public void OnAppDeactivated(ProcessInfo process)
    {
        var app = _registry.Find(process);
        if (app is not null) _sessions.AppDidBlur(app.AppId);
    }

    /// <summary>Call after ANY authentication method succeeds for this app.</summary>
    public void RecordAuthenticated(string appId)
    {
        var app = _registry.FindById(appId) ?? throw new ArgumentException("Unknown app.", nameof(appId));
        lock (_gate)
        {
            _sessions.CreateSession(app.AppId, _registry.PolicyFor(app));
            _recentUnlock[app.AppId] = _time.GetTimestamp();
        }
    }

    /// <summary>Windows locked/slept, user pressed "Lock All", etc.: everything needs authentication again.</summary>
    public void LockAll()
    {
        lock (_gate) { _sessions.RevokeAll(); _recentUnlock.Clear(); }
    }

    public void PauseProtection(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        lock (_gate) _pausedUntil = _time.GetTimestamp() + (long)(duration.TotalSeconds * _time.TimestampFrequency);
    }

    public void ResumeProtection() { lock (_gate) _pausedUntil = null; }

    bool IsPaused()
    {
        if (_pausedUntil is not { } until) return false;
        if (_time.GetTimestamp() < until) return true;
        _pausedUntil = null;
        return false;
    }
}
