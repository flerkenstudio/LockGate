using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using LockGate.Core.AppLock;
using LockGate.Core.Models;
using LockGate.Infrastructure.Windows;

namespace LockGate.Infrastructure.Processes;

public sealed class ProcessWatcher : IDisposable
{
    /// <summary>How long after unlock a process is immune to re-locking.</summary>
    static readonly TimeSpan AntiRelockWindow = TimeSpan.FromSeconds(10);

    readonly AppLockEngine _engine;
    readonly Win32.WinEventDelegate _procDelegate;
    IntPtr _hookHandle;
    ProcessInfo _currentForegroundProcess;
    IntPtr _lastLockedHwnd;
    bool _disposed;
    bool _isAuthInProgress;

    // Track multiple recently-unlocked apps concurrently (keyed by lowercase process name)
    readonly ConcurrentDictionary<string, long> _recentlyUnlockedByName = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<int, long> _recentlyUnlockedByPid = new();

    public event Action<ProcessInfo, IntPtr>? AuthenticationRequired;
    public event Action<ProcessInfo>? ForegroundChanged;

    public ProcessWatcher(AppLockEngine engine)
    {
        _engine = engine;
        // Keep delegate alive as GC root to prevent callback failure
        _procDelegate = OnForegroundEvent;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;

        _hookHandle = Win32.SetWinEventHook(
            Win32.EVENT_SYSTEM_FOREGROUND,
            Win32.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _procDelegate,
            0,
            0,
            Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);

        // Check the initial foreground window
        CheckCurrentForeground();
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    void OnForegroundEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;
        ProcessWindow(hwnd);
    }

    public void CheckCurrentForeground()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd != IntPtr.Zero)
        {
            ProcessWindow(hwnd);
        }
    }

    bool IsRecentlyUnlocked(int pid, string processName)
    {
        // Check by PID
        if (_recentlyUnlockedByPid.TryGetValue(pid, out var pidTs)
            && Stopwatch.GetElapsedTime(pidTs) < AntiRelockWindow)
            return true;

        // Check by process name (handles multi-process apps like Electron)
        if (!string.IsNullOrEmpty(processName)
            && _recentlyUnlockedByName.TryGetValue(processName, out var nameTs)
            && Stopwatch.GetElapsedTime(nameTs) < AntiRelockWindow)
            return true;

        return false;
    }

    void ProcessWindow(IntPtr hwnd)
    {
        // Don't intercept windows while an auth dialog is already showing
        if (_isAuthInProgress) return;

        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == Environment.ProcessId) return;

        var (name, path) = Win32.GetProcessDetails((int)pid);
        if (string.IsNullOrEmpty(name)) return;

        var newProcess = new ProcessInfo((int)pid, name, path);

        // If this window belongs to a process we recently authenticated,
        // let it take foreground smoothly without re-locking!
        if (IsRecentlyUnlocked((int)pid, name))
        {
            _currentForegroundProcess = newProcess;
            ForegroundChanged?.Invoke(newProcess);
            return;
        }

        if (_currentForegroundProcess.Pid != newProcess.Pid)
        {
            if (_currentForegroundProcess.Pid != 0)
            {
                _engine.OnAppDeactivated(_currentForegroundProcess);
            }
            _currentForegroundProcess = newProcess;
            ForegroundChanged?.Invoke(newProcess);
        }

        var decision = _engine.OnAppActivated(newProcess);
        if (decision == LockDecision.RequireAuthentication)
        {
            _lastLockedHwnd = hwnd;
            _isAuthInProgress = true;
            // Minimize target window to protect screen contents
            Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE);
            AuthenticationRequired?.Invoke(newProcess, hwnd);
        }
    }

    /// <summary>
    /// Marks auth as complete and records the unlock so that the restored window won't trigger re-lock.
    /// Must be called from UI thread (WPF Dispatcher).
    /// </summary>
    public void RestoreLockedWindow(IntPtr hwnd, int pid = 0, string? processName = null)
    {
        _isAuthInProgress = false;

        var now = Stopwatch.GetTimestamp();

        // Record anti-relock by PID
        if (pid > 0)
            _recentlyUnlockedByPid[pid] = now;

        // Record anti-relock by process name (handles multi-process apps)
        if (!string.IsNullOrEmpty(processName))
            _recentlyUnlockedByName[processName] = now;

        // Also record all PIDs that share this process name
        if (!string.IsNullOrEmpty(processName))
        {
            var cleanName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;
            try
            {
                foreach (var p in Process.GetProcessesByName(cleanName))
                {
                    _recentlyUnlockedByPid[p.Id] = now;
                }
            }
            catch { }
        }

        // First restore pass (on current thread — should be UI/Dispatcher thread)
        Win32.ForceForegroundWindow(hwnd, pid, processName);

        // Schedule one gentle follow-up pass using captured SynchronizationContext (to ensure window settled)
        var syncContext = SynchronizationContext.Current;
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            if (syncContext != null)
            {
                syncContext.Post(_ => Win32.ForceForegroundWindow(hwnd, pid, processName), null);
            }
            else
            {
                Win32.ForceForegroundWindow(hwnd, pid, processName);
            }
        });

        // Cleanup stale entries after the anti-relock window expires
        _ = Task.Run(async () =>
        {
            await Task.Delay((int)AntiRelockWindow.TotalMilliseconds + 1000);
            CleanupStaleEntries();
        });
    }

    /// <summary>Called when auth is cancelled — keeps app minimized but does NOT kill it.</summary>
    public void CancelAuthentication(int pid)
    {
        _isAuthInProgress = false;
        // Just keep the window minimized. Do NOT kill the process.
        // The user can re-activate it and try again.
    }

    [Obsolete("Use CancelAuthentication instead. Kept for backward compatibility.")]
    public void TerminateLockedProcess(int pid)
    {
        CancelAuthentication(pid);
    }

    void CleanupStaleEntries()
    {
        foreach (var kvp in _recentlyUnlockedByPid)
        {
            if (Stopwatch.GetElapsedTime(kvp.Value) > AntiRelockWindow)
                _recentlyUnlockedByPid.TryRemove(kvp.Key, out _);
        }
        foreach (var kvp in _recentlyUnlockedByName)
        {
            if (Stopwatch.GetElapsedTime(kvp.Value) > AntiRelockWindow)
                _recentlyUnlockedByName.TryRemove(kvp.Key, out _);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
