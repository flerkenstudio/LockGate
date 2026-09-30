using System.Diagnostics;
using LockGate.Infrastructure.Windows;

namespace LockGate.Core.Tests;

public sealed class WindowRestoreTests
{
    [Test]
    public void ForceForegroundWindow_RestoresMinimizedWindow()
    {
        Process? proc = null;
        try
        {
            proc = Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
            Thread.Sleep(1500);

            // Locate actual notepad window (handles packaged Windows 11 notepad stub processes)
            IntPtr hwnd = IntPtr.Zero;
            int realPid = 0;
            foreach (var p in Process.GetProcessesByName("notepad"))
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    hwnd = p.MainWindowHandle;
                    realPid = p.Id;
                    break;
                }
            }

            Check.True(hwnd != IntPtr.Zero, "Notepad main window handle should be found");

            // Minimize
            Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE);
            Thread.Sleep(400);

            Check.True(Win32.IsIconic(hwnd), "Window should be iconic (minimized)");

            // Restore via ForceForegroundWindow
            Win32.ForceForegroundWindow(hwnd, realPid, "notepad.exe");
            Thread.Sleep(500);

            Check.False(Win32.IsIconic(hwnd), "Window should no longer be iconic after ForceForegroundWindow");
        }
        finally
        {
            foreach (var p in Process.GetProcessesByName("notepad"))
            {
                try { p.Kill(); } catch { }
            }
        }
    }

    [Test]
    public void ProcessWatcher_RestoreAndForeground_DoesNotRelock()
    {
        Process? proc = null;
        try
        {
            proc = Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
            Thread.Sleep(1500);

            IntPtr hwnd = IntPtr.Zero;
            int realPid = 0;
            foreach (var p in Process.GetProcessesByName("notepad"))
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    hwnd = p.MainWindowHandle;
                    realPid = p.Id;
                    break;
                }
            }

            Check.True(hwnd != IntPtr.Zero, "Notepad main window handle should be found");

            var config = new LockGate.Core.Configuration.AppConfig
            {
                ProtectedApps = new List<Models.ProtectedApp>
                {
                    new Models.ProtectedApp
                    {
                        AppId = "notepad",
                        Executable = "notepad.exe",
                        Enabled = true,
                        SessionMinutes = 1,
                        LockWhenFocusLost = false
                    }
                }
            };
            var registry = new AppLock.ProtectedAppRegistry(config);
            var sessions = new AppLock.SessionManager();
            var engine = new AppLock.AppLockEngine(registry, sessions);
            var watcher = new LockGate.Infrastructure.Processes.ProcessWatcher(engine);

            int authRequiredCount = 0;
            watcher.AuthenticationRequired += (p, h) => authRequiredCount++;

            // Simulate lock
            Win32.ShowWindow(hwnd, Win32.SW_MINIMIZE);
            Thread.Sleep(300);

            // Record auth & restore
            engine.RecordAuthenticated("notepad");
            watcher.RestoreLockedWindow(hwnd, realPid, "notepad.exe");
            Thread.Sleep(600);

            Check.False(Win32.IsIconic(hwnd), "Window should be restored");
            Check.Eq(0, authRequiredCount);
        }
        finally
        {
            foreach (var p in Process.GetProcessesByName("notepad"))
            {
                try { p.Kill(); } catch { }
            }
        }
    }
}
