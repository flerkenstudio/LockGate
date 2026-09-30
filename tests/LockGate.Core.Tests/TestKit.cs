namespace LockGate.Core.Tests;

/// <summary>Deterministic clock. Uses the monotonic timestamp path the production code relies on.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}

public sealed class MemoryPinStore : LockGate.Core.Security.IPinStore
{
    public string? Value;
    public string? Load() => Value;
    public void Save(string verifier) => Value = verifier;
}

public static class Check
{
    public static void True(bool cond, string msg = "expected true") { if (!cond) throw new Exception(msg); }
    public static void False(bool cond, string msg = "expected false") { if (cond) throw new Exception(msg); }
    public static void Eq<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"expected <{expected}> but was <{actual}>"); }
    public static void Throws<TEx>(Action a) where TEx : Exception
    {
        try { a(); } catch (TEx) { return; }
        throw new Exception($"expected {typeof(TEx).Name}");
    }
}
