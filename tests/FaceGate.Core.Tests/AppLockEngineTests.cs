using FaceGate.Core.AppLock;
using FaceGate.Core.Configuration;
using FaceGate.Core.Models;

namespace FaceGate.Core.Tests;

public sealed class AppLockEngineTests
{
    readonly ManualTimeProvider _t = new();
    static readonly ProcessInfo Chrome = new(100, "chrome.exe");
    static readonly ProcessInfo Notepad = new(200, "notepad.exe");

    AppLockEngine New(params ProtectedApp[] apps) => New(5, apps);
    AppLockEngine New(int defaultMinutes, params ProtectedApp[] apps)
    {
        var cfg = new AppConfig { DefaultSessionMinutes = defaultMinutes, ProtectedApps = apps.ToList() };
        return new AppLockEngine(new ProtectedAppRegistry(cfg), new SessionManager(_t), _t);
    }
    static ProtectedApp ChromeRule(int? minutes = null, bool focus = false, bool enabled = true) =>
        new() { AppId = "chrome", Executable = "chrome.exe", SessionMinutes = minutes, LockWhenFocusLost = focus, Enabled = enabled };

    [Test] public void UnprotectedAppIsNotProtected() =>
        Check.Eq(LockDecision.NotProtected, New(ChromeRule()).OnAppActivated(Notepad));

    [Test] public void DisabledRuleIsNotProtected() =>
        Check.Eq(LockDecision.NotProtected, New(ChromeRule(enabled: false)).OnAppActivated(Chrome));

    [Test] public void ProtectedAppWithoutSessionRequiresAuth() =>
        Check.Eq(LockDecision.RequireAuthentication, New(ChromeRule()).OnAppActivated(Chrome));

    [Test] public void MatchingIgnoresCaseAndDirectory() =>
        Check.Eq(LockDecision.RequireAuthentication,
            New(ChromeRule()).OnAppActivated(new ProcessInfo(1, @"C:\temp\CHROME.EXE")));

    [Test] public void CopiedExeInOtherFolderStillLocked() =>
        Check.Eq(LockDecision.RequireAuthentication,
            New(new ProtectedApp { AppId = "chrome", Executable = "chrome.exe", ExecutablePath = @"C:\Program Files\Google\Chrome\chrome.exe" })
                .OnAppActivated(new ProcessInfo(1, "chrome.exe", @"D:\evil\chrome.exe")));

    [Test] public void AuthenticationOpensSessionThenExpires()
    {
        var e = New(ChromeRule(minutes: 10));
        e.RecordAuthenticated("chrome");
        _t.Advance(TimeSpan.FromMinutes(5)); Check.Eq(LockDecision.Allow, e.OnAppActivated(Chrome));
        _t.Advance(TimeSpan.FromMinutes(6)); Check.Eq(LockDecision.RequireAuthentication, e.OnAppActivated(Chrome));
    }

    [Test] public void GlobalDefaultUsedWhenNoPerAppValue()
    {
        var e = New(2, ChromeRule());
        e.RecordAuthenticated("chrome");
        _t.Advance(TimeSpan.FromMinutes(3));
        Check.Eq(LockDecision.RequireAuthentication, e.OnAppActivated(Chrome));
    }

    [Test] public void LockImmediatelyRelocksAfterCooldown()
    {
        var e = New(ChromeRule(minutes: 0));
        e.RecordAuthenticated("chrome");
        Check.Eq(LockDecision.Allow, e.OnAppActivated(Chrome));                 // inside cooldown: no re-lock loop
        _t.Advance(AppLockEngine.UnlockCooldown + TimeSpan.FromMilliseconds(1));
        Check.Eq(LockDecision.RequireAuthentication, e.OnAppActivated(Chrome));
    }

    [Test] public void LockAllInvalidatesSessionsAndCooldown()
    {
        var e = New(ChromeRule(minutes: 30));
        e.RecordAuthenticated("chrome");
        e.LockAll();                                                            // simulates Windows lock / sleep
        Check.Eq(LockDecision.RequireAuthentication, e.OnAppActivated(Chrome));
    }

    [Test] public void FocusModeThroughEngine()
    {
        var e = New(ChromeRule(minutes: 1, focus: true));
        e.RecordAuthenticated("chrome");
        _t.Advance(TimeSpan.FromMinutes(20)); Check.Eq(LockDecision.Allow, e.OnAppActivated(Chrome));
        e.OnAppDeactivated(Chrome);
        _t.Advance(TimeSpan.FromMinutes(2));  Check.Eq(LockDecision.RequireAuthentication, e.OnAppActivated(Chrome));
    }

    [Test] public void PauseProtectionAllowsThenExpires()
    {
        var e = New(ChromeRule());
        e.PauseProtection(TimeSpan.FromMinutes(5));
        Check.Eq(LockDecision.Allow, e.OnAppActivated(Chrome));
        _t.Advance(TimeSpan.FromMinutes(6));
        Check.Eq(LockDecision.RequireAuthentication, e.OnAppActivated(Chrome));
    }

    [Test] public void ResumeProtectionEndsPause()
    {
        var e = New(ChromeRule());
        e.PauseProtection(TimeSpan.FromHours(1)); e.ResumeProtection();
        Check.Eq(LockDecision.RequireAuthentication, e.OnAppActivated(Chrome));
    }

    [Test] public void UnknownAppIdOnAuthThrows() =>
        Check.Throws<ArgumentException>(() => New(ChromeRule()).RecordAuthenticated("nope"));
}
