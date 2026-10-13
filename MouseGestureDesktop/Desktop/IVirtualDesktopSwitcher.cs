using MouseGestureDesktop.Gesture;

namespace MouseGestureDesktop.Desktop;

/// <summary>Abstraction over virtual-desktop switching (COM or keyboard-backed).</summary>
public interface IVirtualDesktopSwitcher
{
    /// <summary>Human-readable name of the active backend, for the settings UI/tooltip.</summary>
    string BackendName { get; }

    /// <summary>Switch one desktop in the given direction. Returns false if it could not be performed.</summary>
    bool TrySwitch(SwitchDirection direction);
}
