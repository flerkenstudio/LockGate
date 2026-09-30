using System.Diagnostics;
using FaceGate.Core.AppLock;
using FaceGate.Core.Models;
using FaceGate.Infrastructure.Windows;

namespace FaceGate.Infrastructure.Processes;

public sealed class ProcessWatcher : IDisposable
{
    readonly AppLockEngine _engine;
    readonly Win32.WinEventDelegate _procDelegate;
    IntPtr _hookHandle;
    ProcessInfo _currentForegroundProcess;
    IntPtr _lastLockedHwnd;
    bool _disposed;

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

    void ProcessWindow(IntPtr hwnd)
    {
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == Environment.ProcessId) return;

        var (name, path) = Win32.GetProcessDetails((int)pid);
        if (string.IsNullOrEmpty(name)) return;

        var newProcess = new ProcessInfo((int)pid, name, path);

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
            // Minimize target window to protect screen contents
            Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE);
            AuthenticationRequired?.Invoke(newProcess, hwnd);
        }
    }

    public void RestoreLockedWindow(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
            Win32.SetForegroundWindow(hwnd);
        }
    }

    public void TerminateLockedProcess(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            proc.Kill();
        }
        catch
        {
            // Process may have already exited
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
