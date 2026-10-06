using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LockGate.Core.Models;
using LockGate.Core.Security;
using LockGate.App.Services;

namespace LockGate.App.Views;

public partial class AuthWindow : Window
{
    readonly ProcessInfo _process;
    readonly IntPtr _hwnd;
    readonly DispatcherTimer _lockoutTimer;
    DateTime _lockoutEndTime;
    string _currentMode = "WindowsHello";
    int _consecutiveFailures;

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
        var effectiveMode = await LockGateService.Instance.GetEffectiveSecurityMethodAsync();

        if (effectiveMode == "WindowsHello" && LockGateService.Instance.IsFaceUnlockDisabledNow())
        {
            effectiveMode = "LocalCode";
        }

        SetAuthMode(effectiveMode);

        if (_currentMode == "WindowsHello")
        {
            // Auto-trigger Windows Hello prompt immediately upon appearance
            await Dispatcher.Yield();
            TryWindowsHello();
        }
    }

    void SetAuthMode(string mode)
    {
        _currentMode = mode;

        if (mode == "WindowsHello")
        {
            HelloPanel.Visibility = Visibility.Visible;
            PinPanel.Visibility = Visibility.Collapsed;

            HeaderTitle.Text = "Windows Hello";
            HeaderSubtitle.Text = "Verify your identity to unlock";

            if (LockGateService.Instance.IsPinConfigured)
            {
                SwitchModeButton.Content = "Use Master PIN instead";
                SwitchModeButton.Visibility = Visibility.Visible;
            }
            else
            {
                SwitchModeButton.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            HelloPanel.Visibility = Visibility.Collapsed;
            PinPanel.Visibility = Visibility.Visible;

            HeaderTitle.Text = "Master PIN";
            HeaderSubtitle.Text = "Enter PIN to unlock";

            PinBox.Focus();

            SwitchModeButton.Content = "Use Windows Hello instead";
            SwitchModeButton.Visibility = Visibility.Visible;
        }
    }

    void SwitchMode_Click(object sender, RoutedEventArgs e)
    {
        StatusMessage.Text = string.Empty;
        if (_currentMode == "WindowsHello")
        {
            SetAuthMode("LocalCode");
        }
        else
        {
            SetAuthMode("WindowsHello");
            TryWindowsHello();
        }
    }

    async void WindowsHello_Click(object sender, RoutedEventArgs e)
    {
        TryWindowsHello();
    }

    async void TryWindowsHello()
    {
        StatusMessage.Foreground = System.Windows.Media.Brushes.SkyBlue;
        StatusMessage.Text = "Waiting for Windows Hello prompt...";

        var verified = await WindowsHelloService.VerifyAsync($"LockGate: Unlock {_process.ExecutableName}");
        if (verified)
        {
            _consecutiveFailures = 0;
            LockGateService.Instance.LogSecurityEvent("SuccessUnlock", _process.ExecutableName, _process.ExecutableName, "Unlocked via Windows Hello");
            StatusMessage.Foreground = System.Windows.Media.Brushes.MediumSpringGreen;
            StatusMessage.Text = "✓ Identity verified";
            OnSuccess();
        }
        else
        {
            _consecutiveFailures++;
            LockGateService.Instance.LogSecurityEvent("FailedHello", _process.ExecutableName, _process.ExecutableName, $"Windows Hello verification failed (count: {_consecutiveFailures})");

            if (_consecutiveFailures >= 3)
            {
                _ = Task.Run(async () =>
                {
                    var snapshot = await IntruderCaptureService.CaptureSnapshotAsync($"Intruder detected on {_process.ExecutableName}");
                    if (snapshot != null)
                    {
                        LockGateService.Instance.LogSecurityEvent("IntruderSnapshot", _process.ExecutableName, _process.ExecutableName, "Webcam photo captured after 3 failed Hello attempts", snapshot);
                    }
                });
            }

            StatusMessage.Foreground = System.Windows.Media.Brushes.IndianRed;
            StatusMessage.Text = "Windows Hello cancelled or failed. Click to retry.";
        }
    }

    void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_currentMode == "WindowsHello")
            {
                TryWindowsHello();
            }
            else
            {
                SubmitPin();
            }
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
            StatusMessage.Foreground = System.Windows.Media.Brushes.IndianRed;
            StatusMessage.Text = "Please enter your PIN.";
            return;
        }

        var attempt = LockGateService.Instance.VerifyPin(pin);
        switch (attempt.Result)
        {
            case PinResult.Success:
                _consecutiveFailures = 0;
                LockGateService.Instance.LogSecurityEvent("SuccessUnlock", _process.ExecutableName, _process.ExecutableName, "Unlocked via Master PIN");
                OnSuccess();
                break;

            case PinResult.Incorrect:
                _consecutiveFailures++;
                LockGateService.Instance.LogSecurityEvent("FailedPin", _process.ExecutableName, _process.ExecutableName, $"Incorrect PIN attempt (count: {_consecutiveFailures})");

                if (_consecutiveFailures >= 3)
                {
                    _ = Task.Run(async () =>
                    {
                        var snapshot = await IntruderCaptureService.CaptureSnapshotAsync($"Intruder detected on {_process.ExecutableName}");
                        if (snapshot != null)
                        {
                            LockGateService.Instance.LogSecurityEvent("IntruderSnapshot", _process.ExecutableName, _process.ExecutableName, "Webcam photo captured after 3 failed attempts", snapshot);
                        }
                    });
                }

                StatusMessage.Foreground = System.Windows.Media.Brushes.IndianRed;
                StatusMessage.Text = "Incorrect PIN. Please try again.";
                PinBox.Password = string.Empty;
                PinBox.Focus();
                break;

            case PinResult.LockedOut:
                _consecutiveFailures++;
                LockGateService.Instance.LogSecurityEvent("PinLockedOut", _process.ExecutableName, _process.ExecutableName, $"Lockout triggered for {attempt.RetryAfter.TotalSeconds}s");
                StartLockout(attempt.RetryAfter);
                break;

            case PinResult.NotConfigured:
                StatusMessage.Foreground = System.Windows.Media.Brushes.IndianRed;
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
        StatusMessage.Foreground = System.Windows.Media.Brushes.IndianRed;
        StatusMessage.Text = $"Too many failed attempts. Try again in {secs}s.";
    }

    void OnSuccess()
    {
        _lockoutTimer.Stop();
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelAndClose();
    }

    void CancelAndClose()
    {
        _lockoutTimer.Stop();
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _lockoutTimer.Stop();
    }
}
