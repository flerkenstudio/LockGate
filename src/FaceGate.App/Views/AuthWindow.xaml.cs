using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FaceGate.Core.Models;
using FaceGate.Core.Security;
using FaceGate.App.Services;

namespace FaceGate.App.Views;

public partial class AuthWindow : Window
{
    readonly ProcessInfo _process;
    readonly IntPtr _hwnd;
    readonly DispatcherTimer _lockoutTimer;
    DateTime _lockoutEndTime;
    bool _authenticated;

    public AuthWindow(ProcessInfo process, IntPtr hwnd)
    {
        InitializeComponent();
        _process = process;
        _hwnd = hwnd;

        _lockoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _lockoutTimer.Tick += LockoutTimer_Tick;

        AppNameText.Text = $"Application: {process.ExecutableName}";
    }

    async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PinBox.Focus();

        var helloAvailable = await WindowsHelloService.IsAvailableAsync();
        if (helloAvailable)
        {
            WindowsHelloButton.Visibility = Visibility.Visible;
            // Optionally try Windows Hello immediately
            TryWindowsHello();
        }
    }

    async void TryWindowsHello()
    {
        var verified = await WindowsHelloService.VerifyAsync($"FaceGate: Unlock {_process.ExecutableName}");
        if (verified)
        {
            OnSuccess();
        }
    }

    void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SubmitPin();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelAndClose();
            e.Handled = true;
        }
    }

    void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(StatusMessage.Text) && !_lockoutTimer.IsEnabled)
        {
            StatusMessage.Text = string.Empty;
        }
    }

    void Digit_Click(object sender, RoutedEventArgs e)
    {
        if (_lockoutTimer.IsEnabled) return;

        if (sender is Button btn && btn.Content is string digit)
        {
            PinBox.Password += digit;
            PinBox.Focus();
        }
    }

    void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_lockoutTimer.IsEnabled) return;
        PinBox.Password = string.Empty;
        PinBox.Focus();
    }

    void Unlock_Click(object sender, RoutedEventArgs e)
    {
        SubmitPin();
    }

    void SubmitPin()
    {
        if (_lockoutTimer.IsEnabled) return;

        var pin = PinBox.Password;
        if (string.IsNullOrWhiteSpace(pin))
        {
            StatusMessage.Text = "Please enter your PIN.";
            return;
        }

        var attempt = FaceGateService.Instance.VerifyPin(pin);
        switch (attempt.Result)
        {
            case PinResult.Success:
                OnSuccess();
                break;

            case PinResult.Incorrect:
                StatusMessage.Text = "Incorrect PIN. Please try again.";
                PinBox.Password = string.Empty;
                PinBox.Focus();
                break;

            case PinResult.LockedOut:
                StartLockout(attempt.RetryAfter);
                break;

            case PinResult.NotConfigured:
                StatusMessage.Text = "PIN is not set yet. Configure it in Dashboard.";
                break;
        }
    }

    void StartLockout(TimeSpan retryAfter)
    {
        _lockoutEndTime = DateTime.UtcNow.Add(retryAfter);
        PinBox.IsEnabled = false;
        PinBox.Password = string.Empty;
        UpdateLockoutMessage();
        _lockoutTimer.Start();
    }

    void LockoutTimer_Tick(object? sender, EventArgs e)
    {
        var remaining = _lockoutEndTime - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _lockoutTimer.Stop();
            PinBox.IsEnabled = true;
            StatusMessage.Text = "You may try again now.";
            PinBox.Focus();
        }
        else
        {
            UpdateLockoutMessage();
        }
    }

    void UpdateLockoutMessage()
    {
        var remaining = _lockoutEndTime - DateTime.UtcNow;
        var secs = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
        StatusMessage.Text = $"Too many failed attempts. Try again in {secs}s.";
    }

    async void WindowsHello_Click(object sender, RoutedEventArgs e)
    {
        await WindowsHelloService.VerifyAsync($"FaceGate: Unlock {_process.ExecutableName}");
    }

    void OnSuccess()
    {
        _authenticated = true;
        _lockoutTimer.Stop();

        // Match the app in registry by process
        var app = FaceGateService.Instance.Config.ProtectedApps
            .FirstOrDefault(a => string.Equals(a.Executable, _process.ExecutableName, StringComparison.OrdinalIgnoreCase));

        var appId = app?.AppId ?? _process.ExecutableName;
        FaceGateService.Instance.RecordAuthenticated(appId, _hwnd);

        Close();
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelAndClose();
    }

    void CancelAndClose()
    {
        if (!_authenticated)
        {
            FaceGateService.Instance.CancelAuthentication(_process.Pid);
        }
        _lockoutTimer.Stop();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_authenticated)
        {
            FaceGateService.Instance.CancelAuthentication(_process.Pid);
        }
        _lockoutTimer.Stop();
    }
}
