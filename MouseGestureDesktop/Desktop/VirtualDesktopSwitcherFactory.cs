using MouseGestureDesktop.Config;
using MouseGestureDesktop.Gesture;
using MouseGestureDesktop.Input;

namespace MouseGestureDesktop.Desktop;

/// <summary>
/// Builds an <see cref="IVirtualDesktopSwitcher"/> per the configured <see cref="SwitchMethod"/>,
/// and provides a runtime-degrading wrapper: COM-first with automatic fallback to keyboard.
/// </summary>
public static class VirtualDesktopSwitcherFactory
{
    public static IVirtualDesktopSwitcher Create(SwitchMethod method, InputSynthesizer input)
    {
        var keyboard = new KeyboardVirtualDesktopSwitcher(input);

        switch (method)
        {
            case SwitchMethod.KeyboardOnly:
                return keyboard;

            case SwitchMethod.ComOnly:
                return (IVirtualDesktopSwitcher?)ComVirtualDesktopSwitcher.TryCreate() ?? keyboard;

            case SwitchMethod.Auto:
            default:
                ComVirtualDesktopSwitcher? com = ComVirtualDesktopSwitcher.TryCreate();
                return com is null ? keyboard : new FallbackSwitcher(com, keyboard);
        }
    }

    /// <summary>Uses COM; on the first failed switch, permanently degrades to keyboard.</summary>
    private sealed class FallbackSwitcher : IVirtualDesktopSwitcher
    {
        private readonly IVirtualDesktopSwitcher _primary;
        private readonly IVirtualDesktopSwitcher _fallback;
        private bool _degraded;

        public FallbackSwitcher(IVirtualDesktopSwitcher primary, IVirtualDesktopSwitcher fallback)
        {
            _primary = primary;
            _fallback = fallback;
        }

        public string BackendName => _degraded ? _fallback.BackendName : $"{_primary.BackendName} → {_fallback.BackendName}";

        public bool TrySwitch(SwitchDirection direction)
        {
            if (!_degraded && _primary.TrySwitch(direction))
                return true;

            _degraded = true;
            return _fallback.TrySwitch(direction);
        }
    }
}
