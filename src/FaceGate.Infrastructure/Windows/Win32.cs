using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FaceGate.Infrastructure.Windows;

public static class Win32
{
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;

    public const uint ASFW_ANY = 0xFFFFFFFF;

    public delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    /// <summary>
    /// Forcefully restores and brings a window (and its process) to the active foreground.
    /// Overcomes Windows 10/11 foreground locking using AllowSetForegroundWindow and AttachThreadInput.
    /// </summary>
    public static void ForceForegroundWindow(IntPtr hWnd, int pid = 0)
    {
        AllowSetForegroundWindow(ASFW_ANY);

        // If a PID is provided, also locate and restore the process's main window handle
        if (pid > 0)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.Refresh();
                var mainHwnd = p.MainWindowHandle;
                if (mainHwnd != IntPtr.Zero && mainHwnd != hWnd)
                {
                    RestoreSingleWindow(mainHwnd);
                }
            }
            catch { /* Process may have exited or access restricted */ }
        }

        if (hWnd != IntPtr.Zero && IsWindow(hWnd))
        {
            RestoreSingleWindow(hWnd);
        }
    }

    private static void RestoreSingleWindow(IntPtr hWnd)
    {
        ShowWindow(hWnd, SW_RESTORE);
        ShowWindowAsync(hWnd, SW_RESTORE);

        uint currentThreadId = GetCurrentThreadId();
        uint targetThreadId = GetWindowThreadProcessId(hWnd, out var targetProcessId);

        AllowSetForegroundWindow(targetProcessId);

        if (currentThreadId != targetThreadId && targetThreadId != 0)
        {
            AttachThreadInput(currentThreadId, targetThreadId, true);
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            SetFocus(hWnd);
            AttachThreadInput(currentThreadId, targetThreadId, false);
        }
        else
        {
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            SetFocus(hWnd);
        }
    }

    /// <summary>
    /// Safely gets the executable name and path for a given process ID, handling protected system processes.
    /// </summary>
    public static (string Name, string? Path) GetProcessDetails(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            var name = proc.ProcessName + ".exe";
            string? path = null;
            try
            {
                path = proc.MainModule?.FileName;
            }
            catch
            {
                // Access denied on certain system or elevated processes
            }
            return (name, path);
        }
        catch
        {
            return (string.Empty, null);
        }
    }
}
