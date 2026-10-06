using LockGate.Core.Models;

namespace LockGate.Core.Configuration;

public sealed record AppConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>Global default session length in minutes (LockGate-Mac default: 5).</summary>
    public int DefaultSessionMinutes { get; init; } = 5;

    public bool LockOnWindowsLock { get; init; } = true;

    /// <summary>Active unlock method: "Auto", "WindowsHello", or "LocalCode".</summary>
    public string ActiveSecurityMethod { get; init; } = "Auto";

    /// <summary>Schedule during which all protected apps must be locked.</summary>
    public TimeSchedule LockSchedule { get; init; } = new() { Enabled = false, StartTime = "22:00", EndTime = "07:00" };

    /// <summary>Schedule during which all protected apps stay unlocked without prompting.</summary>
    public TimeSchedule UnlockSchedule { get; init; } = new() { Enabled = false, StartTime = "07:00", EndTime = "22:00" };

    /// <summary>Schedule during which face unlock is disabled (falls back to PIN).</summary>
    public TimeSchedule DisableFaceSchedule { get; init; } = new() { Enabled = false, StartTime = "23:00", EndTime = "06:00" };

    /// <summary>Preferred video camera device ID for face capture.</summary>
    public string? PreferredCameraId { get; init; }

    /// <summary>Preferred camera device friendly name.</summary>
    public string? PreferredCameraName { get; init; }

    /// <summary>Multi-face enrollment profiles (up to 3 slots: e.g. Primary, Glasses/Alternate, Secondary).</summary>
    public List<FaceProfile> FaceProfiles { get; init; } = new()
    {
        new FaceProfile { Slot = 1, Name = "Primary Face", Enrolled = true }
    };

    public List<ProtectedApp> ProtectedApps { get; init; } = new();

    /// <summary>Throws <see cref="InvalidDataException"/> if the config is not safe to use.</summary>
    public void Validate()
    {
        if (Version < 1 || Version > CurrentVersion)
            throw new InvalidDataException($"Unsupported config version {Version}.");
        if (DefaultSessionMinutes < -1)
            throw new InvalidDataException("DefaultSessionMinutes must be >= -1.");

        if (!string.Equals(ActiveSecurityMethod, "Auto", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ActiveSecurityMethod, "WindowsHello", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ActiveSecurityMethod, "LocalCode", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported ActiveSecurityMethod '{ActiveSecurityMethod}'.");
        }

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
