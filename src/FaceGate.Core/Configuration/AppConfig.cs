using FaceGate.Core.Models;

namespace FaceGate.Core.Configuration;

public sealed record AppConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>Global default session length in minutes (FaceGate-Mac default: 5).</summary>
    public int DefaultSessionMinutes { get; init; } = 5;

    public bool LockOnWindowsLock { get; init; } = true;

    public List<ProtectedApp> ProtectedApps { get; init; } = new();

    /// <summary>Throws <see cref="InvalidDataException"/> if the config is not safe to use.</summary>
    public void Validate()
    {
        if (Version < 1 || Version > CurrentVersion)
            throw new InvalidDataException($"Unsupported config version {Version}.");
        if (DefaultSessionMinutes < -1)
            throw new InvalidDataException("DefaultSessionMinutes must be >= -1.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in ProtectedApps)
        {
            if (string.IsNullOrWhiteSpace(app.AppId) || string.IsNullOrWhiteSpace(app.Executable))
                throw new InvalidDataException("Protected app needs an AppId and Executable.");
            if (app.SessionMinutes is < -1)
                throw new InvalidDataException($"Invalid SessionMinutes for '{app.AppId}'.");
            if (!ids.Add(app.AppId))
                throw new InvalidDataException($"Duplicate AppId '{app.AppId}'.");
        }
    }
}
