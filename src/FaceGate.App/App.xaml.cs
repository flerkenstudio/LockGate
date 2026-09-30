using System.Threading;
using System.Windows;
using FaceGate.App.Services;
using FaceGate.App.Views;
using FaceGate.Core.Models;
using Forms = System.Windows.Forms;

namespace FaceGate.App;

public partial class App : System.Windows.Application
{
    const string MutexName = "FaceGate_Windows_SingleInstance_Mutex";
    Mutex? _singleInstanceMutex;
    Forms.NotifyIcon? _trayIcon;
    Views.MainWindow? _dashboardWindow;
    AuthWindow? _currentAuthWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);


        _singleInstanceMutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "FaceGate is already running in the background. Check your System Tray.",
                "FaceGate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Initialize background engine and services
        FaceGateService.Initialize();
        FaceGateService.Instance.AuthenticationRequested += OnAuthenticationRequested;

        // Initialize System Tray
        InitTrayIcon();

        // Create and show Main Window
        _dashboardWindow = new Views.MainWindow();
        _dashboardWindow.Show();
    }

    void InitTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            Text = "FaceGate - Application Locker",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();

        var openItem = new Forms.ToolStripMenuItem("🛡️ Open Dashboard", null, (s, e) => ShowDashboard());
        var lockAllItem = new Forms.ToolStripMenuItem("🔒 Lock All Applications", null, (s, e) => FaceGateService.Instance.LockAll());
        var pauseItem = new Forms.ToolStripMenuItem("⏸️ Pause Protection (5m)", null, (s, e) => FaceGateService.Instance.PauseProtection(TimeSpan.FromMinutes(5)));
        var resumeItem = new Forms.ToolStripMenuItem("▶️ Resume Protection", null, (s, e) => FaceGateService.Instance.ResumeProtection());
        var exitItem = new Forms.ToolStripMenuItem("❌ Exit FaceGate", null, (s, e) => ExitApplication());

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
            var result = _currentAuthWindow.ShowDialog();
            _currentAuthWindow = null;

            if (result == true)
            {
                // Authenticated! AuthWindow has completely exited modal state.
                var app = FaceGateService.Instance.Config.ProtectedApps
                    .FirstOrDefault(a => string.Equals(a.Executable, process.ExecutableName, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(a.AppId, process.ExecutableName, StringComparison.OrdinalIgnoreCase));

                var appId = app?.AppId ?? process.ExecutableName;
                FaceGateService.Instance.RecordAuthenticated(appId, hwnd, process.Pid, process.ExecutableName);
            }
            else
            {
                FaceGateService.Instance.CancelAuthentication(process.Pid);
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
            FaceGateService.Instance.Dispose();
        }
        catch { /* Ignore */ }

        if (_singleInstanceMutex != null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
