using FaceGate.Core.AppLock;
using FaceGate.Core.Configuration;
using Microsoft.Win32;

namespace FaceGate.Infrastructure.Windows;

public sealed class PowerSessionListener : IDisposable
{
    readonly AppLockEngine _engine;
    readonly Func<bool> _shouldLockOnWindowsLock;
    bool _disposed;

    public PowerSessionListener(AppLockEngine engine, Func<bool> shouldLockOnWindowsLock)
    {
        _engine = engine;
        _shouldLockOnWindowsLock = shouldLockOnWindowsLock;

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff)
        {
            if (_shouldLockOnWindowsLock())
            {
                _engine.LockAll();
            }
        }
    }

    void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend)
        {
            if (_shouldLockOnWindowsLock())
            {
                _engine.LockAll();
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _disposed = true;
        }
    }
}
