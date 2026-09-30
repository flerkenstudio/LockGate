using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LockGate.App.Services;
using LockGate.Core.Models;
using Microsoft.Win32;

namespace LockGate.App.Views;

public partial class MainWindow : Window
{
    public bool AllowClose { get; set; }
    bool _isInitializing = true;

    public MainWindow()
    {
        InitializeComponent();
    }

    async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LockGateService.Instance.StateChanged += OnStateChanged;

        RefreshAppsList();
        LoadSettings();
        await ScanSystemSecurityAsync();
        UpdateStatusHeader();

        _isInitializing = false;
    }

    void OnStateChanged()
    {
        Dispatcher.Invoke(() =>
        {
            RefreshAppsList();
            UpdateStatusHeader();
        });
    }

    async Task ScanSystemSecurityAsync()
    {
        WindowsHelloStatusText.Text = "Scanning...";
        WindowsHelloStatusText.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

        var helloAvailable = await WindowsHelloService.IsAvailableAsync();
        var effective = await LockGateService.Instance.GetEffectiveSecurityMethodAsync();

        if (helloAvailable)
        {
            WindowsHelloStatusText.Text = "Available & Configured (Face / Fingerprint / PIN)";
            WindowsHelloStatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));

            ScanBadge.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
            ScanBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105));
            ScanBadgeText.Text = "✓ Windows Hello Ready";
            ScanBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));

            ScanExplanationText.Text = "Your PC has native Windows Hello security. It is selected as your primary unlock method.";

            RadioHello.IsEnabled = true;
            if (string.Equals(effective, "WindowsHello", StringComparison.OrdinalIgnoreCase))
            {
                RadioHello.IsChecked = true;
            }
            else
            {
                RadioPin.IsChecked = true;
            }
        }
        else
        {
            WindowsHelloStatusText.Text = "Not Detected or Not Set Up";
            WindowsHelloStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));

            ScanBadge.Background = new SolidColorBrush(Color.FromRgb(120, 53, 15));
            ScanBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            ScanBadgeText.Text = "⚠ Master PIN Active";
            ScanBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));

            ScanExplanationText.Text = "Windows Hello is not set up on this Windows account. Master PIN will be used to unlock applications.";

            RadioHello.IsEnabled = false;
            RadioPin.IsChecked = true;
        }
    }

    async void RescanSystem_Click(object sender, RoutedEventArgs e)
    {
        await ScanSystemSecurityAsync();
        UpdateStatusHeader();
    }

    void LockMethod_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        if (RadioHello.IsChecked == true)
        {
            LockGateService.Instance.SetActiveSecurityMethod("WindowsHello");
        }
        else if (RadioPin.IsChecked == true)
        {
            LockGateService.Instance.SetActiveSecurityMethod("LocalCode");
        }

        UpdateStatusHeader();
    }

    void UpdateStatusHeader()
    {
        var service = LockGateService.Instance;
        var configMethod = service.Config.ActiveSecurityMethod;
        string methodLabel = configMethod switch
        {
            "WindowsHello" => "Windows Hello",
            "LocalCode" => "Master PIN",
            _ => "Windows Hello (Auto)"
        };

        if (service.IsPaused)
        {
            StatusPill.Background = new SolidColorBrush(Color.FromRgb(120, 53, 15));
            StatusPill.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            StatusPillText.Text = "⏸ Protection Paused";
            StatusPillText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            PauseResumeButton.Content = "▶ Resume Protection";
        }
        else
        {
            StatusPill.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
            StatusPill.BorderBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105));
            StatusPillText.Text = $"● Protection Active ({methodLabel})";
            StatusPillText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            PauseResumeButton.Content = "⏸ Pause 5m";
        }
    }

    void RefreshAppsList()
    {
        AppsContainer.Children.Clear();
        var apps = LockGateService.Instance.Config.ProtectedApps;
        AppsCountText.Text = $"({apps.Count} guarded)";

        if (apps.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "No applications are currently protected. Add one above to begin.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 13,
                Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            AppsContainer.Children.Add(emptyText);
            return;
        }

        foreach (var app in apps)
        {
            var card = CreateAppRow(app);
            AppsContainer.Children.Add(card);
        }
    }

    Border CreateAppRow(ProtectedApp app)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // App details
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
        var titleText = new TextBlock
        {
            Text = app.AppId,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252))
        };
        var exeText = new TextBlock
        {
            Text = $"({app.Executable})",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(6, 1, 0, 0)
        };
        titleStack.Children.Add(titleText);
        titleStack.Children.Add(exeText);
        infoStack.Children.Add(titleStack);

        if (!string.IsNullOrEmpty(app.ExecutablePath))
        {
            var pathText = new TextBlock
            {
                Text = app.ExecutablePath,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoStack.Children.Add(pathText);
        }
        Grid.SetColumn(infoStack, 0);
        grid.Children.Add(infoStack);

        // Session mode badge
        string sessionLabel = app.SessionMinutes switch
        {
            0 => "Immediate",
            -1 => "Indefinite",
            null => "Default (5m)",
            var m => $"{m} Min"
        };
        var sessionBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"⏱ {sessionLabel}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontWeight = FontWeights.Medium
            }
        };
        Grid.SetColumn(sessionBadge, 1);
        grid.Children.Add(sessionBadge);

        // Enable / Disable Checkbox
        var toggleCheck = new CheckBox
        {
            Content = "Guarded",
            IsChecked = app.Enabled,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };
        toggleCheck.Click += (s, e) =>
        {
            LockGateService.Instance.ToggleApp(app.AppId, toggleCheck.IsChecked == true);
        };
        Grid.SetColumn(toggleCheck, 2);
        grid.Children.Add(toggleCheck);

        // Remove button
        var removeBtn = new Button
        {
            Content = "Delete",
            Style = (Style)FindResource("DangerButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        removeBtn.Click += (s, e) =>
        {
            var res = MessageBox.Show($"Remove protection for '{app.AppId}'?", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                LockGateService.Instance.RemoveApp(app.AppId);
            }
        };
        Grid.SetColumn(removeBtn, 3);
        grid.Children.Add(removeBtn);

        border.Child = grid;
        return border;
    }

    void ProcessPicker_DropDownOpened(object? sender, EventArgs e)
    {
        ProcessPickerCombo.Items.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var procs = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
            .OrderBy(p => p.ProcessName);

        foreach (var p in procs)
        {
            var exe = p.ProcessName + ".exe";
            if (seen.Add(exe))
            {
                ProcessPickerCombo.Items.Add($"{exe} - {p.MainWindowTitle}");
            }
        }
    }

    void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select Application Executable"
        };
        if (dlg.ShowDialog() == true)
        {
            var exe = Path.GetFileName(dlg.FileName);
            ProcessPickerCombo.Text = $"{exe} - {dlg.FileName}";
        }
    }

    void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var text = ProcessPickerCombo.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Please select or type an application executable name.", "LockGate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string exeName;
        string? exePath = null;

        var parts = text.Split(new[] { " - " }, StringSplitOptions.None);
        exeName = parts[0].Trim();
        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            exeName += ".exe";
        }

        if (parts.Length > 1 && File.Exists(parts[1]))
        {
            exePath = parts[1].Trim();
        }

        var appId = Path.GetFileNameWithoutExtension(exeName).ToLowerInvariant();

        int? sessionMinutes = SessionModeCombo.SelectedIndex switch
        {
            0 => 0,   // Immediate
            1 => 1,   // 1 Min
            2 => 5,   // 5 Min
            3 => 15,  // 15 Min
            4 => 30,  // 30 Min
            5 => -1,  // Indefinite
            _ => 5
        };

        var newApp = new ProtectedApp
        {
            AppId = appId,
            Executable = exeName,
            ExecutablePath = exePath,
            Enabled = true,
            SessionMinutes = sessionMinutes,
            LockWhenFocusLost = (sessionMinutes == 0)
        };

        LockGateService.Instance.AddOrUpdateApp(newApp);
        ProcessPickerCombo.Text = string.Empty;
    }

    void SavePin_Click(object sender, RoutedEventArgs e)
    {
        var pin = NewPinBox.Password;
        var confirm = ConfirmPinBox.Password;

        if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
        {
            PinStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            PinStatusMessage.Text = "PIN must be at least 4 digits.";
            return;
        }

        if (!string.Equals(pin, confirm, StringComparison.Ordinal))
        {
            PinStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            PinStatusMessage.Text = "PINs do not match. Please re-enter.";
            return;
        }

        LockGateService.Instance.SetMasterPin(pin);
        NewPinBox.Password = string.Empty;
        ConfirmPinBox.Password = string.Empty;
        PinStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        PinStatusMessage.Text = "✓ Master PIN saved securely using Windows DPAPI.";
    }

    async void TestHello_Click(object sender, RoutedEventArgs e)
    {
        var verified = await WindowsHelloService.VerifyAsync("LockGate Verification Test");
        if (verified)
        {
            MessageBox.Show("Windows Hello authenticated successfully!", "LockGate Verification", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Windows Hello verification cancelled or failed.", "LockGate Verification", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void LoadSettings()
    {
        var config = LockGateService.Instance.Config;
        DefaultSessionCombo.SelectedIndex = config.DefaultSessionMinutes switch
        {
            0 => 0,
            1 => 1,
            5 => 2,
            15 => 3,
            30 => 4,
            -1 => 5,
            _ => 2
        };
        LockOnWindowsLockCheck.IsChecked = config.LockOnWindowsLock;
    }

    void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var defaultSession = DefaultSessionCombo.SelectedIndex switch
        {
            0 => 0,
            1 => 1,
            2 => 5,
            3 => 15,
            4 => 30,
            5 => -1,
            _ => 5
        };

        var lockOnLock = LockOnWindowsLockCheck.IsChecked == true;
        LockGateService.Instance.UpdateGeneralSettings(defaultSession, lockOnLock);

        MessageBox.Show("Settings saved successfully.", "LockGate", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        var service = LockGateService.Instance;
        if (service.IsPaused)
        {
            service.ResumeProtection();
        }
        else
        {
            service.PauseProtection(TimeSpan.FromMinutes(5));
        }
    }

    void LockAll_Click(object sender, RoutedEventArgs e)
    {
        LockGateService.Instance.LockAll();
        MessageBox.Show("All application sessions locked immediately.", "LockGate", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    void NavTab_Checked(object sender, RoutedEventArgs e)
    {
        if (AppsView == null || SecurityView == null || SettingsView == null) return;

        if (TabApps.IsChecked == true)
        {
            AppsView.Visibility = Visibility.Visible;
            SecurityView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
        }
        else if (TabSecurity.IsChecked == true)
        {
            AppsView.Visibility = Visibility.Collapsed;
            SecurityView.Visibility = Visibility.Visible;
            SettingsView.Visibility = Visibility.Collapsed;
        }
        else if (TabSettings.IsChecked == true)
        {
            AppsView.Visibility = Visibility.Collapsed;
            SecurityView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
        }
    }

    void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!AllowClose && MinimizeToTrayCheck.IsChecked == true)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
