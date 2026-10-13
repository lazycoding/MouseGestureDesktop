namespace MouseGestureDesktop.Gesture;

public enum SwitchDirection
{
    Left,
    Right,
}

/// <summary>Phase of the trajectory overlay, driven by the state machine.</summary>
public enum OverlayPhase
{
    None,
    Begin,
    Update,
    Step,
    End,
}

/// <summary>
/// Outcome of feeding one input event to <see cref="GestureStateMachine"/>.
/// Pure data so the state machine can be unit-tested without Win32.
/// </summary>
public readonly struct GestureAction
{
    /// <summary>True if the originating input event must be swallowed (hook returns 1).</summary>
    public bool Swallow { get; init; }

    /// <summary>Replay the trigger button as a real click (down+up) — preserves single/double click.</summary>
    public bool EmitClick { get; init; }

    /// <summary>Number of desktop switches to perform this event (continuous stepping).</summary>
    public int SwitchCount { get; init; }

    /// <summary>Direction of the switches (valid when <see cref="SwitchCount"/> &gt; 0).</summary>
    public SwitchDirection SwitchDirection { get; init; }

    /// <summary>Overlay instruction for visual feedback.</summary>
    public OverlayPhase Overlay { get; init; }

    /// <summary>Signed locked-axis offset from origin (px), for overlay rendering.</summary>
    public int OverlayOffset { get; init; }

    public static GestureAction PassThrough => new() { Swallow = false };
    public static GestureAction Swallowed => new() { Swallow = true };
}
