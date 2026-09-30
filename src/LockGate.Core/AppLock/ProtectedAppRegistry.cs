using LockGate.Core.Configuration;
using LockGate.Core.Models;

namespace LockGate.Core.AppLock;

/// <summary>Thread-safe, swap-on-update view of the protected-app config.</summary>
public sealed class ProtectedAppRegistry
{
    volatile AppConfig _config;

    public ProtectedAppRegistry(AppConfig config) { config.Validate(); _config = config; }

    public AppConfig Config => _config;

    public void Update(AppConfig config) { config.Validate(); _config = config; }

    /// <summary>
    /// Match by executable file name (case-insensitive). Also matches by AppId for flexibility.
    /// Fails closed: a stored path is NOT required to match, otherwise a copy of the exe in another folder
    /// would slip past. Hash verification is future work.
    /// </summary>
    public ProtectedApp? Find(ProcessInfo p)
    {
        var name = FileNameOf(p.ExecutableName);
        var nameNoExt = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

        foreach (var app in _config.ProtectedApps)
        {
            var appExe = FileNameOf(app.Executable);
            var appExeNoExt = appExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? appExe[..^4] : appExe;

            // Match by exact executable name or filename
            if (string.Equals(app.Executable, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(appExe, name, StringComparison.OrdinalIgnoreCase))
                return app;

            // Match without extension (e.g. "telegram" vs "Telegram.exe")
            if (string.Equals(appExeNoExt, nameNoExt, StringComparison.OrdinalIgnoreCase))
                return app;

            // Match AppId
            if (string.Equals(app.AppId, nameNoExt, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(app.AppId, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(app.AppId + ".exe", name, StringComparison.OrdinalIgnoreCase))
                return app;
        }
        return null;
    }

    /// <summary>File-name part of a path, splitting on both separators so results don't depend on the host OS.</summary>
    static string FileNameOf(string path) => path[(path.LastIndexOfAny(new[] { '\\', '/' }) + 1)..];

    public ProtectedApp? FindById(string appId)
    {
        var idNoExt = appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? appId[..^4] : appId;

        return _config.ProtectedApps.FirstOrDefault(a =>
        {
            var appExe = FileNameOf(a.Executable);
            var appExeNoExt = appExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? appExe[..^4] : appExe;

            return string.Equals(a.AppId, appId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(a.AppId, idNoExt, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(a.Executable, appId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(appExe, appId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(appExeNoExt, idNoExt, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(a.Executable, FileNameOf(appId), StringComparison.OrdinalIgnoreCase);
        });
    }

    public SessionPolicy PolicyFor(ProtectedApp app) =>
        new(app.SessionMinutes ?? _config.DefaultSessionMinutes, app.LockWhenFocusLost);
}
