using System.IO;
using LockGate.Core.AppLock;
using LockGate.Core.Configuration;
using LockGate.Core.Models;
using LockGate.Core.Security;
using LockGate.Infrastructure.Processes;
using LockGate.Infrastructure.Storage;
using LockGate.Infrastructure.Windows;

namespace LockGate.App.Services;

public sealed class LockGateService : IDisposable
{
    static LockGateService? _instance;
    public static LockGateService Instance => _instance ?? throw new InvalidOperationException("LockGateService not initialized.");

    readonly string _configPath;
    readonly ConfigStore _configStore;
    readonly DpapiPinStore _pinStore;
    readonly ProtectedAppRegistry _registry;
    readonly SessionManager _sessionManager;
    readonly AppLockEngine _engine;
    readonly PinAuthenticator _pinAuth;
    readonly ProcessWatcher _processWatcher;
    readonly PowerSessionListener _powerListener;
    readonly AuditLogStore _auditLogStore;
    bool _disposed;

    public event Action<ProcessInfo, IntPtr>? AuthenticationRequested;
    public event Action? StateChanged;

    public AppConfig Config => _registry.Config;
    public bool IsPinConfigured => _pinAuth.IsConfigured;
    public bool IsPaused { get; private set; }
    public DateTime? PausedUntil { get; private set; }

    public static void Initialize()
    {
        _instance ??= new LockGateService();
    }

    private LockGateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "LockGate");
        Directory.CreateDirectory(dir);

        var legacyDir = Path.Combine(appData, "FaceGate");
        if (Directory.Exists(legacyDir))
        {
            var legacyConfig = Path.Combine(legacyDir, "config.json");
            var newConfig = Path.Combine(dir, "config.json");
            if (File.Exists(legacyConfig) && !File.Exists(newConfig))
            {
                try { File.Copy(legacyConfig, newConfig, overwrite: false); } catch { }
            }

            var legacyPin = Path.Combine(legacyDir, "pin.dat");
            var newPin = Path.Combine(dir, "pin.dat");
            if (File.Exists(legacyPin) && !File.Exists(newPin))
            {
                try { File.Copy(legacyPin, newPin, overwrite: false); } catch { }
            }
        }

        _configPath = Path.Combine(dir, "config.json");
        _configStore = new ConfigStore(_configPath);
        _pinStore = new DpapiPinStore(Path.Combine(dir, "pin.dat"));

        var config = _configStore.Load();
        _registry = new ProtectedAppRegistry(config);
        _sessionManager = new SessionManager();
        _engine = new AppLockEngine(_registry, _sessionManager);
        _pinAuth = new PinAuthenticator(_pinStore);

        _processWatcher = new ProcessWatcher(_engine);
        _processWatcher.AuthenticationRequired += OnAuthenticationRequired;

        _powerListener = new PowerSessionListener(_engine, () => _registry.Config.LockOnWindowsLock);
        _auditLogStore = new AuditLogStore(Path.Combine(dir, "audit_logs.json"));

        _processWatcher.Start();
    }

    public void LogSecurityEvent(string eventType, string appId, string? processName = null, string? details = null, string? snapshotPath = null)
    {
        _auditLogStore.Append(new SecurityAuditLog
        {
            EventType = eventType,
            AppId = appId,
            ProcessName = processName,
            Details = details,
            SnapshotPath = snapshotPath
        });
    }

    public List<SecurityAuditLog> GetRecentAuditLogs(int limit = 50) => _auditLogStore.LoadRecent(limit);

    void OnAuthenticationRequired(ProcessInfo process, IntPtr hwnd)
    {
        AuthenticationRequested?.Invoke(process, hwnd);
    }

    public PinAttempt VerifyPin(string pin)
    {
        return _pinAuth.Verify(pin);
    }

    public void SetMasterPin(string pin)
    {
        _pinAuth.SetPin(pin);
        StateChanged?.Invoke();
    }

    public void RecordAuthenticated(string appId, IntPtr hwnd, int pid = 0, string? processName = null)
    {
        try
        {
            _engine.RecordAuthenticated(appId);
        }
        catch (ArgumentException)
        {
            var regApp = _registry.Config.ProtectedApps.FirstOrDefault(a =>
                string.Equals(a.AppId, appId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Executable, appId, StringComparison.OrdinalIgnoreCase));
            if (regApp != null)
            {
                _engine.RecordAuthenticated(regApp.AppId);
            }
        }

        _processWatcher.RestoreLockedWindow(hwnd, pid, processName);
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Fallback restore when RecordAuthenticated fails. Ensures the window is always brought back
    /// even if session recording had an error.
    /// </summary>
    public void ForceRestore(IntPtr hwnd, int pid, string? processName)
    {
        _processWatcher.RestoreLockedWindow(hwnd, pid, processName);
    }

    public void CancelAuthentication(int pid)
    {
        // On cancel, keep the app minimized but don't kill it.
        // The user can re-activate it to trigger auth again.
        _processWatcher.CancelAuthentication(pid);
        StateChanged?.Invoke();
    }

    public void AddOrUpdateApp(ProtectedApp app)
    {
        var currentApps = Config.ProtectedApps.Where(a => !string.Equals(a.AppId, app.AppId, StringComparison.OrdinalIgnoreCase)).ToList();
        currentApps.Add(app);

        var newConfig = Config with { ProtectedApps = currentApps };
        _configStore.Save(newConfig);
        _registry.Update(newConfig);
        StateChanged?.Invoke();
    }

    public void RemoveApp(string appId)
    {
        var currentApps = Config.ProtectedApps.Where(a => !string.Equals(a.AppId, appId, StringComparison.OrdinalIgnoreCase)).ToList();
        var newConfig = Config with { ProtectedApps = currentApps };
        _configStore.Save(newConfig);
        _registry.Update(newConfig);
        StateChanged?.Invoke();
    }

    public void ToggleApp(string appId, bool enabled)
    {
        var app = _registry.FindById(appId);
        if (app is not null)
        {
            AddOrUpdateApp(app with { Enabled = enabled });
        }
    }

    public void UpdateGeneralSettings(int defaultSessionMinutes, bool lockOnWindowsLock)
    {
        var newConfig = Config with
        {
            DefaultSessionMinutes = defaultSessionMinutes,
            LockOnWindowsLock = lockOnWindowsLock
        };
        _configStore.Save(newConfig);
        _registry.Update(newConfig);
        StateChanged?.Invoke();
    }

    public void UpdateSchedules(TimeSchedule lockSchedule, TimeSchedule unlockSchedule, TimeSchedule disableFaceSchedule)
    {
        var newConfig = Config with
        {
            LockSchedule = lockSchedule,
            UnlockSchedule = unlockSchedule,
            DisableFaceSchedule = disableFaceSchedule
        };
        _configStore.Save(newConfig);
        _registry.Update(newConfig);
        StateChanged?.Invoke();
    }

    public void UpdateCamera(string? cameraId, string? cameraName)
    {
        var newConfig = Config with
        {
            PreferredCameraId = cameraId,
            PreferredCameraName = cameraName
        };
        _configStore.Save(newConfig);
        _registry.Update(newConfig);
        StateChanged?.Invoke();
    }

    public void UpdateFaceProfiles(List<FaceProfile> profiles)
    {
        var newConfig = Config with
        {
            FaceProfiles = profiles
        };
        _configStore.Save(newConfig);
        _registry.Update(newConfig);
        StateChanged?.Invoke();
    }

    public bool IsFaceUnlockDisabledNow()
    {
        return Config.DisableFaceSchedule?.IsActiveAt(DateTime.Now) == true;
    }

    public async Task<string> GetEffectiveSecurityMethodAsync()
    {
        if (string.Equals(Config.ActiveSecurityMethod, "WindowsHello", StringComparison.OrdinalIgnoreCase))
            return "WindowsHello";

        if (string.Equals(Config.ActiveSecurityMethod, "LocalCode", StringComparison.OrdinalIgnoreCase))
            return "LocalCode";

        // Auto: detect system hardware and capabilities
        var helloAvailable = await WindowsHelloService.IsAvailableAsync();
        return helloAvailable ? "WindowsHello" : "LocalCode";
    }

    public void SetActiveSecurityMethod(string method)
    {
        var newConfig = Config with { ActiveSecurityMethod = method };
        _configStore.Save(newConfig);
        _registry.Update(newConfig);
        StateChanged?.Invoke();
    }

    public void LockAll()
    {
        _engine.LockAll();
        StateChanged?.Invoke();
    }

    public void PauseProtection(TimeSpan duration)
    {
        _engine.PauseProtection(duration);
        IsPaused = true;
        PausedUntil = DateTime.UtcNow.Add(duration);
        StateChanged?.Invoke();
    }

    public void ResumeProtection()
    {
        _engine.ResumeProtection();
        IsPaused = false;
        PausedUntil = null;
        StateChanged?.Invoke();
    }

    public bool HasActiveSession(string appId) => _sessionManager.HasActiveSession(appId);

    public void Dispose()
    {
        if (!_disposed)
        {
            _processWatcher.Stop();
            _processWatcher.Dispose();
            _powerListener.Dispose();
            _disposed = true;
        }
    }
}
