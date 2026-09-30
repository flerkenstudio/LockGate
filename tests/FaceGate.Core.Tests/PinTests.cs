using FaceGate.Core.Security;

namespace FaceGate.Core.Tests;

public sealed class PinTests
{
    readonly ManualTimeProvider _t = new();
    static PinHasher Fast => new(1_000);   // tests only; production uses the 600k default

    PinAuthenticator New(out MemoryPinStore store)
    {
        store = new MemoryPinStore();
        var a = new PinAuthenticator(store, Fast, new AttemptLimiter(_t));
        a.SetPin("2468");
        return a;
    }

    [Test] public void DefaultIterationsMeetOwaspFloor() => Check.True(PinHasher.DefaultIterations >= 600_000);

    [Test] public void RoundTrip() { var h = Fast.Hash("2468"); Check.True(PinHasher.Verify("2468", h)); Check.False(PinHasher.Verify("2469", h)); }

    [Test] public void VerifierNeverContainsThePin()
    {
        var h = Fast.Hash("s3cret-pin");
        Check.False(h.Contains("s3cret-pin"));
        Check.True(h.StartsWith("v1$"));
    }

    [Test] public void SamePinGetsDifferentSalts() => Check.False(Fast.Hash("2468") == Fast.Hash("2468"));

    [Test] public void MalformedVerifiersFailClosed()
    {
        foreach (var bad in new[] { "", "garbage", "v1$x$y$z", "v2$1000$AAAA$AAAA", "v1$1$AAAA$AAAA", "v1$99999999999$AAAA$AAAA", "v1$1000$!!!$???" })
            Check.False(PinHasher.Verify("2468", bad), bad);
        Check.False(PinHasher.Verify("2468", null));
    }

    [Test] public void WeakPinsRejected()
    {
        Check.Throws<ArgumentException>(() => Fast.Hash("123"));
        Check.Throws<ArgumentException>(() => Fast.Hash("    "));
        Check.Throws<ArgumentException>(() => Fast.Hash(new string('9', 65)));
    }

    [Test] public void NotConfiguredBeforeSetPin()
    {
        var a = new PinAuthenticator(new MemoryPinStore(), Fast);
        Check.Eq(PinResult.NotConfigured, a.Verify("2468").Result);
    }

    [Test] public void CorrectPinSucceeds() => Check.Eq(PinResult.Success, New(out _).Verify("2468").Result);

    [Test] public void WrongPinIsIncorrect() => Check.Eq(PinResult.Incorrect, New(out _).Verify("0000").Result);

    [Test] public void LockoutAfterFiveFailuresEvenForCorrectPin()
    {
        var a = New(out _);
        for (var i = 0; i < 5; i++) Check.Eq(PinResult.Incorrect, a.Verify("0000").Result);
        Check.Eq(PinResult.Incorrect, a.Verify("0000").Result);                  // 6th failure starts the 30s lock
        var locked = a.Verify("2468");
        Check.Eq(PinResult.LockedOut, locked.Result);
        Check.True(locked.RetryAfter > TimeSpan.FromSeconds(25));
    }

    [Test] public void LockoutLiftsAndSuccessResetsCounter()
    {
        var a = New(out _);
        for (var i = 0; i < 6; i++) a.Verify("0000");
        _t.Advance(TimeSpan.FromSeconds(31));
        Check.Eq(PinResult.Success, a.Verify("2468").Result);
        for (var i = 0; i < 5; i++) Check.Eq(PinResult.Incorrect, a.Verify("0000").Result); // counter was reset: 5 free again
    }

    [Test] public void DelayDoublesAndIsCapped()
    {
        var l = new AttemptLimiter(_t);
        for (var i = 0; i < 5; i++) Check.Eq(TimeSpan.Zero, l.RecordFailure());
        Check.Eq(TimeSpan.FromSeconds(30), l.RecordFailure());
        Check.Eq(TimeSpan.FromSeconds(60), l.RecordFailure());
        Check.Eq(TimeSpan.FromSeconds(120), l.RecordFailure());
        for (var i = 0; i < 30; i++) l.RecordFailure();
        Check.Eq(TimeSpan.FromMinutes(15), l.RecordFailure());
    }

    [Test] public void StoredVerifierIsNotPlaintext()
    {
        New(out var store);
        Check.False(store.Value!.Contains("2468"));
    }
}
