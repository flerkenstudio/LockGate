using FaceGate.Core.Configuration;
using FaceGate.Core.Models;

namespace FaceGate.Core.AppLock;

/// <summary>Thread-safe, swap-on-update view of the protected-app config.</summary>
public sealed class ProtectedAppRegistry
{
    volatile AppConfig _config;

    public ProtectedAppRegistry(AppConfig config) { config.Validate(); _config = config; }

    public AppConfig Config => _config;

    public void Update(AppConfig config) { config.Validate(); _config = config; }

    /// <summary>
    /// Match by executable file name (case-insensitive). Fails closed: a stored path is NOT required to
    /// match, otherwise a copy of the exe in another folder would slip past. Hash verification is future work.
    /// </summary>
    public ProtectedApp? Find(ProcessInfo p)
    {
        var name = FileNameOf(p.ExecutableName);
        foreach (var app in _config.ProtectedApps)
            if (string.Equals(app.Executable, name, StringComparison.OrdinalIgnoreCase)) return app;
        return null;
    }

    /// <summary>File-name part of a path, splitting on both separators so results don't depend on the host OS.</summary>
    static string FileNameOf(string path) => path[(path.LastIndexOfAny(new[] { '\\', '/' }) + 1)..];

    public ProtectedApp? FindById(string appId) =>
        _config.ProtectedApps.FirstOrDefault(a => string.Equals(a.AppId, appId, StringComparison.OrdinalIgnoreCase));

    public SessionPolicy PolicyFor(ProtectedApp app) =>
        new(app.SessionMinutes ?? _config.DefaultSessionMinutes, app.LockWhenFocusLost);
}
