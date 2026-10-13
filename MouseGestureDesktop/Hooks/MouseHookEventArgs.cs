using System.Drawing;

namespace MouseGestureDesktop.Hooks;

/// <summary>The relevant mouse messages the hook forwards.</summary>
public enum MouseMessageType
{
    Move,
    XButtonDown,
    XButtonUp,
    Other,
}

/// <summary>Which X (side) button an event refers to.</summary>
public enum XButton
{
    None,
    XButton1,
    XButton2,
}

/// <summary>Event payload raised by <see cref="LowLevelMouseHook"/>.</summary>
public sealed class MouseHookEventArgs : EventArgs
{
    public required MouseMessageType Type { get; init; }
    public required XButton Button { get; init; }
    public required Point Point { get; init; }
    public required long Timestamp { get; init; }

    /// <summary>True when the event was synthesized (SendInput) — must be passed through to avoid self-triggering.</summary>
    public required bool Injected { get; init; }

    /// <summary>Set to true to swallow the event (the hook returns 1).</summary>
    public bool Handled { get; set; }
}
