using MouseGestureDesktop.Gesture;
using MouseGestureDesktop.Input;

namespace MouseGestureDesktop.Desktop;

/// <summary>
/// Switches desktops by synthesizing the Ctrl+Win+Left/Right shortcut. Works on every
/// Windows 10/11 build; cannot read the current desktop index, so it relies on the OS to
/// no-op at the first/last desktop.
/// </summary>
public sealed class KeyboardVirtualDesktopSwitcher : IVirtualDesktopSwitcher
{
    private readonly InputSynthesizer _input;

    public KeyboardVirtualDesktopSwitcher(InputSynthesizer input) => _input = input;

    public string BackendName => "Keyboard (Ctrl+Win+Arrow)";

    public bool TrySwitch(SwitchDirection direction)
    {
        try
        {
            if (direction == SwitchDirection.Right)
                _input.SwitchDesktopRight();
            else
                _input.SwitchDesktopLeft();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
