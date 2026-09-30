using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LockGate.Infrastructure.Windows;

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
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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
    public static extern bool OpenIcon(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public const byte VK_MENU = 0x12; // Alt key
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    /// <summary>
    /// Forcefully restores and brings a window (and its process) to the active foreground.
    /// Uses OpenIcon, ShowWindow, SetWindowPos, AllowSetForegroundWindow, and AttachThreadInput.
    /// </summary>
    public static void ForceForegroundWindow(IntPtr hWnd, int pid = 0, string? processName = null)
    {
        AllowSetForegroundWindow(ASFW_ANY);

        if (hWnd != IntPtr.Zero && IsWindow(hWnd))
        {
            RestoreSingleWindow(hWnd);
        }

        var pids = new HashSet<int>();
        if (pid > 0) pids.Add(pid);

        if (!string.IsNullOrEmpty(processName))
        {
            var cleanName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;

            try
            {
                foreach (var p in Process.GetProcessesByName(cleanName))
                {
                    pids.Add(p.Id);
                }
            }
            catch { }
        }

        foreach (var targetPid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(targetPid);
                p.Refresh();
                var mainHwnd = p.MainWindowHandle;
                if (mainHwnd != IntPtr.Zero && mainHwnd != hWnd)
                {
                    RestoreSingleWindow(mainHwnd);
                }
            }
            catch { }
        }

        try
        {
            EnumWindows((h, lParam) =>
            {
                if (IsWindow(h))
                {
                    GetWindowThreadProcessId(h, out var winPid);
                    if (pids.Contains((int)winPid))
                    {
                        if (IsIconic(h) || IsWindowVisible(h) || GetWindowTextLength(h) > 0)
                        {
                            RestoreSingleWindow(h);
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
    }

    private static void RestoreSingleWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return;

        // Unconditionally restore: restores if minimized, displays if normal
        OpenIcon(hWnd);
        ShowWindow(hWnd, SW_RESTORE);
        ShowWindowAsync(hWnd, SW_RESTORE);
        ShowWindow(hWnd, SW_SHOWNORMAL);

        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        uint currentThreadId = GetCurrentThreadId();
        uint targetThreadId = GetWindowThreadProcessId(hWnd, out var targetProcessId);
        IntPtr fgHwnd = GetForegroundWindow();
        uint fgThreadId = fgHwnd != IntPtr.Zero ? GetWindowThreadProcessId(fgHwnd, out _) : 0;

        AllowSetForegroundWindow(ASFW_ANY);
        AllowSetForegroundWindow(targetProcessId);

        bool attachedFg = false;
        bool attachedTarget = false;

        try
        {
            if (fgThreadId != 0 && fgThreadId != currentThreadId)
            {
                attachedFg = AttachThreadInput(currentThreadId, fgThreadId, true);
            }
            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                attachedTarget = AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            // Simulate Alt keypress to bypass Windows 10/11 foreground restrictions
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            SetFocus(hWnd);
        }
        finally
        {
            if (attachedTarget) AttachThreadInput(currentThreadId, targetThreadId, false);
            if (attachedFg) AttachThreadInput(currentThreadId, fgThreadId, false);
        }
    }

    /// <summary>
    /// Safely gets the executable name and path for a given process ID, handling protected, elevated,
    /// and UWP/ApplicationFrameHost hosted processes.
    /// </summary>
    public static (string Name, string? Path) GetProcessDetails(int pid)
    {
        if (pid <= 0) return (string.Empty, null);

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

            // Fallback for elevated processes where MainModule is inaccessible
            if (string.IsNullOrEmpty(path))
            {
                var hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProc != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024);
                        int size = sb.Capacity;
                        if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                        {
                            path = sb.ToString();
                        }
                    }
                    finally
                    {
                        CloseHandle(hProc);
                    }
                }
            }

            if (!string.IsNullOrEmpty(path))
            {
                var exeName = System.IO.Path.GetFileName(path);
                if (!string.IsNullOrEmpty(exeName))
                {
                    name = exeName;
                }
            }

            return (name, path);
        }
        catch
        {
            return (string.Empty, null);
        }
    }
}
