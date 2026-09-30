using System.IO;
using FaceGate.Core.AppLock;
using FaceGate.Core.Configuration;
using FaceGate.Core.Models;
using FaceGate.Core.Security;
using FaceGate.Infrastructure.Processes;
using FaceGate.Infrastructure.Storage;
using FaceGate.Infrastructure.Windows;

namespace FaceGate.App.Services;

public sealed class FaceGateService : IDisposable
{
    static FaceGateService? _instance;
    public static FaceGateService Instance => _instance ?? throw new InvalidOperationException("FaceGateService not initialized.");

    readonly string _configPath;
    readonly ConfigStore _configStore;
    readonly DpapiPinStore _pinStore;
    readonly ProtectedAppRegistry _registry;
    readonly SessionManager _sessionManager;
    readonly AppLockEngine _engine;
    readonly PinAuthenticator _pinAuth;
    readonly ProcessWatcher _processWatcher;
    readonly PowerSessionListener _powerListener;
    bool _disposed;

    public event Action<ProcessInfo, IntPtr>? AuthenticationRequested;
    public event Action? StateChanged;

    public AppConfig Config => _registry.Config;
    public bool IsPinConfigured => _pinAuth.IsConfigured;
    public bool IsPaused { get; private set; }
    public DateTime? PausedUntil { get; private set; }

    public static void Initialize()
    {
        _instance ??= new FaceGateService();
    }

    private FaceGateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "FaceGate");
        Directory.CreateDirectory(dir);

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

        _processWatcher.Start();
    }

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

    public void RecordAuthenticated(string appId, IntPtr hwnd)
    {
        _engine.RecordAuthenticated(appId);
        _processWatcher.RestoreLockedWindow(hwnd);
        StateChanged?.Invoke();
    }

    public void CancelAuthentication(int pid)
    {
        // On cancel, minimize or terminate the locked process to protect user privacy
        _processWatcher.TerminateLockedProcess(pid);
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
