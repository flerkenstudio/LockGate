namespace LockGate.Core.Models;

/// <summary>A process the user wants LockGate to guard.</summary>
public sealed record ProtectedApp
{
    /// <summary>Stable identifier (e.g. "chrome"). Session state is keyed by this.</summary>
    public required string AppId { get; init; }

    /// <summary>Executable file name, e.g. "chrome.exe". Matching is case-insensitive.</summary>
    public required string Executable { get; init; }

    /// <summary>Full path captured when the app was added (display / future verification; never used to weaken name matching).</summary>
    public string? ExecutablePath { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>Per-app session length. null = use the global default, 0 = lock immediately, -1 = keep unlocked.</summary>
    public int? SessionMinutes { get; init; }

    /// <summary>true = the session timer runs only while the app is out of focus.</summary>
    public bool LockWhenFocusLost { get; init; }
}

/// <summary>What the watcher knows about a process at detection time.</summary>
public readonly record struct ProcessInfo(int Pid, string ExecutableName, string? ExecutablePath = null);
