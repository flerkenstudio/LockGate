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

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    public const uint GW_OWNER = 4;
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;
    public const long WS_EX_APPWINDOW = 0x00040000L;

    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    /// <summary>
    /// Checks if a window is an actual user-facing application window and not an internal
    /// background/helper window (such as QTrayIconMessageWindow, Cicero, message-only, or tool windows).
    /// </summary>
    public static bool IsRealAppWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;

        // Check window class name for known background/helper/tray windows
        var classSb = new StringBuilder(256);
        GetClassName(hWnd, classSb, classSb.Capacity);
        var className = classSb.ToString();

        if (className.Contains("QTrayIcon", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("Message", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("WorkerW", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("CiceroUIWndFrame", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("DummyDWMListener", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("GDI+ Hook", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check window title for known helper windows
        var titleSb = new StringBuilder(256);
        GetWindowText(hWnd, titleSb, titleSb.Capacity);
        var title = titleSb.ToString();
        if (title.Contains("QTrayIcon", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("MessageWindow", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Filter out tool windows unless explicitly marked as app windows
        long exStyle = (long)GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Forcefully restores and brings a window (and its process) to the active foreground.
    /// Safely targets only genuine application windows and completely ignores Qt/system message windows.
    /// </summary>
    public static void ForceForegroundWindow(IntPtr hWnd, int pid = 0, string? processName = null)
    {
        AllowSetForegroundWindow(ASFW_ANY);

        // 1. If we have the locked window handle and it's a real app window, restore it immediately!
        if (hWnd != IntPtr.Zero && IsRealAppWindow(hWnd))
        {
            RestoreSingleWindow(hWnd);
            return;
        }

        // 2. Otherwise try the process's MainWindowHandle
        if (pid > 0)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                var mainHwnd = p.MainWindowHandle;
                if (mainHwnd != IntPtr.Zero && IsRealAppWindow(mainHwnd))
                {
                    RestoreSingleWindow(mainHwnd);
                    return;
                }
            }
            catch { }
        }

        // 3. Fallback: Find the main visible window of this process using EnumWindows (only real app windows)
        if (pid > 0)
        {
            try
            {
                EnumWindows((h, lParam) =>
                {
                    if (IsWindow(h))
                    {
                        GetWindowThreadProcessId(h, out var winPid);
                        if ((int)winPid == pid && IsRealAppWindow(h))
                        {
                            if (IsIconic(h) || IsWindowVisible(h))
                            {
                                RestoreSingleWindow(h);
                                return false; // Stop after restoring the main real app window
                            }
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }
    }

    private static void RestoreSingleWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd) || !IsRealAppWindow(hWnd)) return;

        // Un-minimize if iconic or minimized
        if (IsIconic(hWnd))
        {
            OpenIcon(hWnd);
            ShowWindow(hWnd, SW_RESTORE);
        }
        else
        {
            ShowWindow(hWnd, SW_SHOWNORMAL);
        }

        AllowSetForegroundWindow(ASFW_ANY);
        uint targetThreadId = GetWindowThreadProcessId(hWnd, out var targetProcessId);
        AllowSetForegroundWindow(targetProcessId);

        // Try direct SetForegroundWindow first — if it works cleanly without assistance, we're done!
        if (SetForegroundWindow(hWnd))
        {
            SetFocus(hWnd);
            return;
        }

        // Attach thread input queues to smoothly transfer focus without UI lag
        uint currentThreadId = GetCurrentThreadId();
        IntPtr fgHwnd = GetForegroundWindow();
        uint fgThreadId = fgHwnd != IntPtr.Zero ? GetWindowThreadProcessId(fgHwnd, out _) : 0;

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
