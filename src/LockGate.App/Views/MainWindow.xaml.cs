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
        try
        {
            LockGateService.Instance.StateChanged += OnStateChanged;

            RefreshAppsList();
            LoadSettings();
            await ScanSystemSecurityAsync();
            await LoadCamerasAsync();
            RefreshFaceProfiles();
            UpdateStatusHeader();
        }
        catch (Exception ex)
        {
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockGate", "crash.log");
            try { System.IO.File.AppendAllText(path, $"[{DateTime.UtcNow}] Window_Loaded Error: {ex}\n\n"); } catch { }
            MessageBox.Show($"Initialization Error: {ex.Message}\n\n{ex.StackTrace}", "LockGate Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isInitializing = false;
        }
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

            ScanExplanationText.Text = "Your PC supports Windows Hello. It is selected as your primary unlock method.";

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

            ScanExplanationText.Text = "Windows Hello is not set up on this account. Master PIN will be used to unlock applications.";

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
            "WindowsHello" => "Hello",
            "LocalCode" => "PIN",
            _ => "Auto"
        };

        if (service.IsPaused)
        {
            StatusPill.Background = new SolidColorBrush(Color.FromRgb(120, 53, 15));
            StatusPill.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            StatusPillText.Text = "Protection Paused";
            StatusPillText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            PauseResumeButton.Content = "▶ Resume Protection";
        }
        else
        {
            StatusPill.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
            StatusPill.BorderBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105));
            StatusPillText.Text = $"Protected ({methodLabel})";
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
            Background = new SolidColorBrush(Color.FromRgb(13, 19, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(34, 48, 74)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 10)
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
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252))
        };
        var exeText = new TextBlock
        {
            Text = $"({app.Executable})",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Margin = new Thickness(6, 2, 0, 0)
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
        string sessionLabel = app.LockWhenFocusLost && app.SessionMinutes != 0
            ? "Focus Mode"
            : app.SessionMinutes switch
            {
                0 => "Immediate",
                -1 => "Indefinite",
                null => "Default (5m)",
                var m => $"{m} Min"
            };
        var sessionBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(22, 32, 54)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(34, 48, 74)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 16, 0),
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

        // Enable / Disable Checkbox (Toggle Switch style)
        var toggleCheck = new CheckBox
        {
            Content = string.Empty,
            IsChecked = app.Enabled,
            Style = (Style)FindResource("ToggleSwitchStyle"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
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

        bool isFocusMode = SessionModeCombo.SelectedIndex == 5;
        int? sessionMinutes = SessionModeCombo.SelectedIndex switch
        {
            0 => 0,   // Immediate
            1 => 1,   // 1 Min
            2 => 5,   // 5 Min
            3 => 15,  // 15 Min
            4 => 30,  // 30 Min
            5 => 5,   // Focus Mode (5 min blur countdown)
            6 => -1,  // Indefinite
            _ => 5
        };

        var newApp = new ProtectedApp
        {
            AppId = appId,
            Executable = exeName,
            ExecutablePath = exePath,
            Enabled = true,
            SessionMinutes = sessionMinutes,
            LockWhenFocusLost = isFocusMode || (sessionMinutes == 0)
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

    sealed class CameraItem
    {
        public string? Id { get; init; }
        public required string DisplayName { get; init; }
        public override string ToString() => DisplayName;
    }

    async Task LoadCamerasAsync()
    {
        if (CameraPickerCombo == null) return;

        CameraPickerCombo.Items.Clear();
        CameraPickerCombo.Items.Add(new CameraItem { Id = null, DisplayName = "Default System Camera (Automatic)" });

        try
        {
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                Windows.Devices.Enumeration.DeviceClass.VideoCapture);

            foreach (var dev in devices)
            {
                if (dev.IsEnabled)
                {
                    CameraPickerCombo.Items.Add(new CameraItem { Id = dev.Id, DisplayName = dev.Name });
                }
            }
        }
        catch
        {
            // Device enumeration handled gracefully
        }

        var savedCamId = LockGateService.Instance.Config.PreferredCameraId;
        int selectIdx = 0;
        if (!string.IsNullOrEmpty(savedCamId))
        {
            for (int i = 1; i < CameraPickerCombo.Items.Count; i++)
            {
                if (CameraPickerCombo.Items[i] is CameraItem item && item.Id == savedCamId)
                {
                    selectIdx = i;
                    break;
                }
            }
        }

        CameraPickerCombo.SelectedIndex = selectIdx;
    }

    void CameraPickerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || CameraPickerCombo.SelectedItem is not CameraItem selected) return;
        LockGateService.Instance.UpdateCamera(selected.Id, selected.Id == null ? null : selected.DisplayName);
    }

    async void RefreshCameras_Click(object sender, RoutedEventArgs e)
    {
        await LoadCamerasAsync();
    }

    void OpenWindowsHelloSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:signinoptions",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open Windows Sign-in options: {ex.Message}", "LockGate", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    void RefreshFaceProfiles()
    {
        if (FaceProfilesContainer == null) return;
        FaceProfilesContainer.Children.Clear();

        var config = LockGateService.Instance.Config;
        var existingProfiles = config.FaceProfiles ?? new();

        for (int slot = 1; slot <= 3; slot++)
        {
            var profile = existingProfiles.FirstOrDefault(p => p.Slot == slot);
            FaceProfilesContainer.Children.Add(CreateFaceSlotCard(slot, profile));
        }
    }

    Border CreateFaceSlotCard(int slot, FaceProfile? profile)
    {
        var isEnrolled = profile != null && profile.Enrolled;
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(13, 19, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(34, 48, 74)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Slot Badge
        var slotBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        slotBadge.Child = new TextBlock
        {
            Text = $"Slot {slot}",
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };
        Grid.SetColumn(slotBadge, 0);
        grid.Children.Add(slotBadge);

        // Details
        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var defaultName = slot switch
        {
            1 => "Primary Face (Standard)",
            2 => "Alternate Look (Glasses / Hat)",
            _ => "Secondary User Profile"
        };
        var nameText = new TextBlock
        {
            Text = profile?.Name ?? defaultName,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252))
        };
        var statusText = new TextBlock
        {
            Text = isEnrolled ? $"Active • Enrolled {profile!.EnrolledAt.ToLocalTime():yyyy-MM-dd}" : "Empty • Slot available for alternate look",
            FontSize = 11,
            Foreground = new SolidColorBrush(isEnrolled ? Color.FromRgb(52, 211, 153) : Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 2, 0, 0)
        };
        details.Children.Add(nameText);
        details.Children.Add(statusText);
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        // Action Buttons
        var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (isEnrolled)
        {
            if (slot > 1) // Slot 1 is Primary, keep it always enrolled
            {
                var removeBtn = new Button
                {
                    Content = "Remove",
                    Style = (Style)FindResource("DangerButton"),
                    Height = 28,
                    Padding = new Thickness(10, 2, 10, 2),
                    FontSize = 11,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                removeBtn.Click += (_, _) =>
                {
                    var config = LockGateService.Instance.Config;
                    var updated = config.FaceProfiles.Where(p => p.Slot != slot).ToList();
                    LockGateService.Instance.UpdateFaceProfiles(updated);
                    RefreshFaceProfiles();
                };
                actionStack.Children.Add(removeBtn);
            }
            else
            {
                var verifiedBadge = new TextBlock
                {
                    Text = "✓ Enrolled",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                actionStack.Children.Add(verifiedBadge);
            }
        }
        else
        {
            var enrollBtn = new Button
            {
                Content = "+ Enroll Slot",
                Style = (Style)FindResource("PrimaryButton"),
                Height = 28,
                Padding = new Thickness(10, 2, 10, 2),
                FontSize = 11
            };
            enrollBtn.Click += async (_, _) =>
            {
                var verified = await WindowsHelloService.VerifyAsync($"Enroll Biometric Appearance for Slot {slot}");
                if (verified)
                {
                    var config = LockGateService.Instance.Config;
                    var list = new List<FaceProfile>(config.FaceProfiles);
                    list.RemoveAll(p => p.Slot == slot);
                    list.Add(new FaceProfile
                    {
                        Slot = slot,
                        Name = defaultName,
                        Enrolled = true,
                        EnrolledAt = DateTime.UtcNow
                    });
                    LockGateService.Instance.UpdateFaceProfiles(list);
                    RefreshFaceProfiles();
                    MessageBox.Show($"Biometric profile for Slot {slot} ({defaultName}) registered successfully!", "LockGate", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            actionStack.Children.Add(enrollBtn);
        }

        Grid.SetColumn(actionStack, 2);
        grid.Children.Add(actionStack);

        border.Child = grid;
        return border;
    }

    void LoadSettings()
    {
        var config = LockGateService.Instance.Config;
        var mins = config.DefaultSessionMinutes;
        if (mins >= 0 && mins <= 30)
        {
            SessionTimeoutSlider.Value = mins;
            SessionTimeoutValueText.Text = mins == 0 ? "0 min (Immediate)" : $"{mins} min";
        }
        else if (mins == -1)
        {
            SessionTimeoutSlider.Value = 30;
            SessionTimeoutValueText.Text = "Keep Unlocked";
        }

        LockOnWindowsLockCheck.IsChecked = config.LockOnWindowsLock;

        ScheduleLockCheck.IsChecked = config.LockSchedule?.Enabled ?? false;
        ScheduleUnlockCheck.IsChecked = config.UnlockSchedule?.Enabled ?? false;
        DisableFaceScheduleCheck.IsChecked = config.DisableFaceSchedule?.Enabled ?? false;
    }

    void SessionTimeoutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SessionTimeoutValueText == null) return;
        int val = (int)e.NewValue;
        if (val == 0)
        {
            SessionTimeoutValueText.Text = "0 min (Immediate)";
        }
        else if (val == 30)
        {
            SessionTimeoutValueText.Text = "30 min";
        }
        else
        {
            SessionTimeoutValueText.Text = $"{val} min";
        }
    }

    void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        int defaultSession = (int)SessionTimeoutSlider.Value;
        var lockOnLock = LockOnWindowsLockCheck.IsChecked == true;
        LockGateService.Instance.UpdateGeneralSettings(defaultSession, lockOnLock);

        var curConfig = LockGateService.Instance.Config;
        var lockSchedule = (curConfig.LockSchedule ?? new TimeSchedule { StartTime = "22:00", EndTime = "07:00" }) with
        {
            Enabled = ScheduleLockCheck.IsChecked == true
        };
        var unlockSchedule = (curConfig.UnlockSchedule ?? new TimeSchedule { StartTime = "07:00", EndTime = "22:00" }) with
        {
            Enabled = ScheduleUnlockCheck.IsChecked == true
        };
        var disableFaceSchedule = (curConfig.DisableFaceSchedule ?? new TimeSchedule { StartTime = "23:00", EndTime = "06:00" }) with
        {
            Enabled = DisableFaceScheduleCheck.IsChecked == true
        };

        LockGateService.Instance.UpdateSchedules(lockSchedule, unlockSchedule, disableFaceSchedule);

        MessageBox.Show("Settings and schedules saved successfully.", "LockGate", MessageBoxButton.OK, MessageBoxImage.Information);
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

    void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
    }

    void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    void NavTab_Checked(object sender, RoutedEventArgs e)
    {
        if (AppsView == null || SecurityView == null || SettingsView == null) return;

        AppsView.Visibility = Visibility.Collapsed;
        SecurityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        if (AboutView != null) AboutView.Visibility = Visibility.Collapsed;
        if (AuditLogsView != null) AuditLogsView.Visibility = Visibility.Collapsed;

        if (TabApps.IsChecked == true)
        {
            AppsView.Visibility = Visibility.Visible;
            if (PageTitleText != null) PageTitleText.Text = "Locked Apps";
            if (PageSubtitleText != null) PageSubtitleText.Text = "Manage protected applications, session durations, and instant lock rules.";
        }
        else if (TabSecurity.IsChecked == true)
        {
            SecurityView.Visibility = Visibility.Visible;
            if (PageTitleText != null) PageTitleText.Text = "Authentication";
            if (PageSubtitleText != null) PageSubtitleText.Text = "Configure Windows Hello biometrics and Master PIN fallback credentials.";
        }
        else if (TabSettings.IsChecked == true)
        {
            SettingsView.Visibility = Visibility.Visible;
            if (PageTitleText != null) PageTitleText.Text = "Behavior";
            if (PageSubtitleText != null) PageSubtitleText.Text = "Adjust launch, locking, schedules, and emergency controls.";
        }
        else if (TabAuditLogs != null && TabAuditLogs.IsChecked == true)
        {
            if (AuditLogsView != null) AuditLogsView.Visibility = Visibility.Visible;
            if (PageTitleText != null) PageTitleText.Text = "Security Audit Logs";
            if (PageSubtitleText != null) PageSubtitleText.Text = "Real-time authentication event stream, failed attempts, and intruder snapshots.";
            RefreshAuditLogs();
        }
        else if (TabAbout != null && TabAbout.IsChecked == true)
        {
            if (AboutView != null) AboutView.Visibility = Visibility.Visible;
            if (PageTitleText != null) PageTitleText.Text = "About";
            if (PageSubtitleText != null) PageSubtitleText.Text = "LockGate for Windows system architecture and security standards.";
        }
    }

    void RefreshAuditLogs_Click(object sender, RoutedEventArgs e)
    {
        RefreshAuditLogs();
    }

    void OpenSnapshotsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = IntruderCaptureService.GetSnapshotsDirectory();
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open snapshots directory: {ex.Message}", "LockGate", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void RefreshAuditLogs()
    {
        if (AuditLogsContainer == null) return;
        AuditLogsContainer.Children.Clear();

        var logs = LockGateService.Instance.GetRecentAuditLogs(100);
        if (AuditLogsCountText != null) AuditLogsCountText.Text = $"({logs.Count} events)";

        if (logs.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "No security events recorded yet. Authentication attempts and unlocks will appear here in real-time.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 13,
                Margin = new Thickness(0, 24, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            AuditLogsContainer.Children.Add(emptyText);
            return;
        }

        foreach (var log in logs)
        {
            AuditLogsContainer.Children.Add(CreateAuditLogRow(log));
        }
    }

    Border CreateAuditLogRow(SecurityAuditLog log)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(13, 19, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Column 0: Timestamp
        var timeText = new TextBlock
        {
            Text = log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(timeText, 0);
        grid.Children.Add(timeText);

        // Column 1: Event Type Badge
        var (badgeBg, badgeFg, badgeBorder, badgeText) = log.EventType switch
        {
            "SuccessUnlock" => (Color.FromRgb(6, 78, 59), Color.FromRgb(52, 211, 153), Color.FromRgb(5, 150, 105), "Unlocked"),
            "FailedPin" => (Color.FromRgb(127, 29, 29), Color.FromRgb(248, 113, 113), Color.FromRgb(220, 38, 38), "Wrong PIN"),
            "FailedHello" => (Color.FromRgb(127, 29, 29), Color.FromRgb(248, 113, 113), Color.FromRgb(220, 38, 38), "Hello Failed"),
            "PinLockedOut" => (Color.FromRgb(127, 29, 29), Color.FromRgb(252, 165, 165), Color.FromRgb(239, 68, 68), "Lockout"),
            "AppLocked" => (Color.FromRgb(30, 58, 138), Color.FromRgb(96, 165, 250), Color.FromRgb(37, 99, 235), "App Locked"),
            _ => (Color.FromRgb(30, 41, 59), Color.FromRgb(203, 213, 225), Color.FromRgb(51, 65, 85), log.EventType)
        };

        var badge = new Border
        {
            Background = new SolidColorBrush(badgeBg),
            BorderBrush = new SolidColorBrush(badgeBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(10, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = badgeText,
            Foreground = new SolidColorBrush(badgeFg),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);

        // Column 2: App & Details
        var detailsStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0) };
        var appNameText = new TextBlock
        {
            Text = string.IsNullOrEmpty(log.AppId) ? "System" : log.AppId,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))
        };
        detailsStack.Children.Add(appNameText);

        if (!string.IsNullOrEmpty(log.Details))
        {
            var detailSubText = new TextBlock
            {
                Text = log.Details,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            detailsStack.Children.Add(detailSubText);
        }
        Grid.SetColumn(detailsStack, 2);
        grid.Children.Add(detailsStack);

        // Column 3: Snapshot indicator / button if available
        if (!string.IsNullOrEmpty(log.SnapshotPath) && File.Exists(log.SnapshotPath))
        {
            var snapBtn = new Button
            {
                Content = "📷 Photo",
                Style = (Style)FindResource("SecondaryButton"),
                Height = 26,
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "View intruder webcam capture"
            };
            var snapPath = log.SnapshotPath;
            snapBtn.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = snapPath, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open photo: {ex.Message}", "LockGate", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            Grid.SetColumn(snapBtn, 3);
            grid.Children.Add(snapBtn);
        }

        border.Child = grid;
        return border;
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
