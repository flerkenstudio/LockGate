using FaceGate.Core.AppLock;

namespace FaceGate.Core.Tests;

public sealed class SessionManagerTests
{
    readonly ManualTimeProvider _t = new();
    SessionManager New() => new(_t);
    static SessionPolicy Fixed(int min) => new(min, FromFocus: false);
    static SessionPolicy Focus(int min) => new(min, FromFocus: true);

    [Test] public void NoSessionByDefault() => Check.False(New().HasActiveSession("a"));

    [Test] public void FixedSessionExpires()
    {
        var s = New(); s.CreateSession("a", Fixed(10));
        _t.Advance(TimeSpan.FromMinutes(9)); Check.True(s.HasActiveSession("a"));
        _t.Advance(TimeSpan.FromMinutes(2)); Check.False(s.HasActiveSession("a"));
    }

    [Test] public void LockImmediatelyCreatesNoSession()
    {
        var s = New(); s.CreateSession("a", Fixed(0)); Check.False(s.HasActiveSession("a"));
    }

    [Test] public void IndefiniteNeverExpires()
    {
        var s = New(); s.CreateSession("a", Fixed(-1));
        _t.Advance(TimeSpan.FromDays(365)); Check.True(s.HasActiveSession("a"));
    }

    [Test] public void SessionIdsAreCaseInsensitive()
    {
        var s = New(); s.CreateSession("Chrome", Fixed(5)); Check.True(s.HasActiveSession("chrome"));
    }

    [Test] public void FocusModeDoesNotExpireWhileFocused()
    {
        var s = New(); s.CreateSession("a", Focus(1));
        _t.Advance(TimeSpan.FromHours(3)); Check.True(s.HasActiveSession("a"));
    }

    [Test] public void FocusModeExpiresAfterBlurTimeout()
    {
        var s = New(); s.CreateSession("a", Focus(1)); s.AppDidBlur("a");
        _t.Advance(TimeSpan.FromSeconds(50)); Check.True(s.HasActiveSession("a"));
        _t.Advance(TimeSpan.FromSeconds(20)); Check.False(s.HasActiveSession("a"));
    }

    [Test] public void FocusModeReturnWithinTimeoutKeepsSession()
    {
        var s = New(); s.CreateSession("a", Focus(1)); s.AppDidBlur("a");
        _t.Advance(TimeSpan.FromSeconds(30)); s.AppDidFocus("a");
        _t.Advance(TimeSpan.FromMinutes(30)); Check.True(s.HasActiveSession("a")); // focused again, timer paused
    }

    [Test] public void FocusModeReturnAfterTimeoutRevokes()
    {
        var s = New(); s.CreateSession("a", Focus(1)); s.AppDidBlur("a");
        _t.Advance(TimeSpan.FromSeconds(90)); s.AppDidFocus("a");
        Check.False(s.HasActiveSession("a"));
    }

    [Test] public void SecondBlurDoesNotResetCountdown()
    {
        var s = New(); s.CreateSession("a", Focus(1)); s.AppDidBlur("a");
        _t.Advance(TimeSpan.FromSeconds(40)); s.AppDidBlur("a");
        _t.Advance(TimeSpan.FromSeconds(30)); Check.False(s.HasActiveSession("a"));
    }

    [Test] public void RevokeAllClearsEverything()
    {
        var s = New(); s.CreateSession("a", Fixed(5)); s.CreateSession("b", Fixed(-1)); s.RevokeAll();
        Check.False(s.HasActiveSession("a")); Check.False(s.HasActiveSession("b"));
    }

    [Test] public void ConcurrentAccessIsSafe()
    {
        var s = new SessionManager();
        Parallel.For(0, 2000, i =>
        {
            var id = "app" + (i % 8);
            s.CreateSession(id, Fixed(5)); s.HasActiveSession(id); s.AppDidBlur(id); s.AppDidFocus(id);
            if (i % 50 == 0) s.RevokeAll();
        });
    }
}
