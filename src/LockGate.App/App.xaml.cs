using System.Threading;
using System.Windows;
using LockGate.App.Services;
using LockGate.App.Views;
using LockGate.Core.Models;
using Forms = System.Windows.Forms;

namespace LockGate.App;

public partial class App : System.Windows.Application
{
    const string MutexName = "LockGate_Windows_SingleInstance_Mutex";
    const string EventName = "LockGate_Windows_ShowDashboard_Event";
    Mutex? _singleInstanceMutex;
    bool _hasMutexOwnership;
    EventWaitHandle? _showDashboardEvent;
    RegisteredWaitHandle? _registeredWait;
    Forms.NotifyIcon? _trayIcon;
    Views.MainWindow? _dashboardWindow;
    AuthWindow? _currentAuthWindow;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockGate", "crash.log");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.AppendAllText(path, $"[{DateTime.UtcNow}] AppDomain Crash: {ex}\n\n");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            var ex = args.Exception;
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockGate", "crash.log");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.AppendAllText(path, $"[{DateTime.UtcNow}] Dispatcher Crash: {ex}\n\n");
            }
            catch { }
            System.Windows.MessageBox.Show(
                $"Startup Error: {ex.Message}\n\n{ex.StackTrace}",
                "LockGate Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // Another instance is already running — wake it up and show the dashboard!
            try
            {
                using var existingEvent = EventWaitHandle.OpenExisting(EventName);
                existingEvent.Set();
            }
            catch { }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;

            Shutdown();
            return;
        }

        _hasMutexOwnership = true;

        try
        {
            _showDashboardEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            _registeredWait = ThreadPool.RegisterWaitForSingleObject(_showDashboardEvent, (state, timedOut) =>
            {
                Dispatcher.Invoke(() => ShowDashboard());
            }, null, -1, false);
        }
        catch { }

        // Initialize background engine and services
        LockGateService.Initialize();
        LockGateService.Instance.AuthenticationRequested += OnAuthenticationRequested;

        // Initialize System Tray
        InitTrayIcon();

        // Create and show Main Window
        _dashboardWindow = new Views.MainWindow();
        MainWindow = _dashboardWindow;
        _dashboardWindow.Show();
    }

    void InitTrayIcon()
    {
        System.Drawing.Icon appIcon;
        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/app.ico");
            var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
            appIcon = new System.Drawing.Icon(streamInfo.Stream);
        }
        catch
        {
            appIcon = System.Drawing.SystemIcons.Shield;
        }

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = appIcon,
            Text = "FaceGate - Application Locker",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();

        var openItem = new Forms.ToolStripMenuItem("🛡️ Open Dashboard", null, (s, e) => ShowDashboard());
        var lockAllItem = new Forms.ToolStripMenuItem("🔒 Lock All Applications", null, (s, e) => LockGateService.Instance.LockAll());
        var pauseItem = new Forms.ToolStripMenuItem("⏸️ Pause Protection (5m)", null, (s, e) => LockGateService.Instance.PauseProtection(TimeSpan.FromMinutes(5)));
        var resumeItem = new Forms.ToolStripMenuItem("▶️ Resume Protection", null, (s, e) => LockGateService.Instance.ResumeProtection());
        var exitItem = new Forms.ToolStripMenuItem("❌ Exit LockGate", null, (s, e) => ExitApplication());

        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(lockAllItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(resumeItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) => ShowDashboard();
    }

    void ShowDashboard()
    {
        if (_dashboardWindow == null)
        {
            _dashboardWindow = new Views.MainWindow();
        }

        _dashboardWindow.Show();
        if (_dashboardWindow.WindowState == WindowState.Minimized)
        {
            _dashboardWindow.WindowState = WindowState.Normal;
        }
        _dashboardWindow.Activate();
    }

    void OnAuthenticationRequested(ProcessInfo process, IntPtr hwnd)
    {
        Dispatcher.Invoke(() =>
        {
            if (_currentAuthWindow != null && _currentAuthWindow.IsVisible)
            {
                _currentAuthWindow.Activate();
                return;
            }

            _currentAuthWindow = new AuthWindow(process, hwnd);
            bool? result;
            try
            {
                result = _currentAuthWindow.ShowDialog();
            }
            finally
            {
                _currentAuthWindow = null;
            }

            if (result == true)
            {
                // Authenticated! Find the matching protected app to get the correct AppId.
                var app = LockGateService.Instance.Config.ProtectedApps
                    .FirstOrDefault(a => string.Equals(a.Executable, process.ExecutableName, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(a.AppId, process.ExecutableName, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(a.AppId + ".exe", process.ExecutableName, StringComparison.OrdinalIgnoreCase));

                var appId = app?.AppId ?? process.ExecutableName;

                try
                {
                    LockGateService.Instance.RecordAuthenticated(appId, hwnd, process.Pid, process.ExecutableName);
                }
                catch (Exception ex)
                {
                    // Log but don't crash — always attempt restore even if session recording fails
                    System.Diagnostics.Debug.WriteLine($"RecordAuthenticated failed: {ex.Message}");
                    LockGateService.Instance.ForceRestore(hwnd, process.Pid, process.ExecutableName);
                }
            }
            else
            {
                LockGateService.Instance.CancelAuthentication(process.Pid);
            }
        });
    }

    void ExitApplication()
    {
        if (_dashboardWindow != null)
        {
            _dashboardWindow.AllowClose = true;
            _dashboardWindow.Close();
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        try
        {
            LockGateService.Instance.Dispose();
        }
        catch { /* Ignore */ }

        _registeredWait?.Unregister(null);
        _showDashboardEvent?.Dispose();

        if (_singleInstanceMutex != null)
        {
            if (_hasMutexOwnership)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch { /* Ignore */ }
            }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }
}
