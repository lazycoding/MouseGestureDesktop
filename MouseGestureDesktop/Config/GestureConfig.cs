using System.Text.Json.Serialization;

namespace MouseGestureDesktop.Config;

/// <summary>Which physical mouse button triggers gestures (and whose click is preserved).</summary>
public enum TriggerButton
{
    XButton1,
    XButton2,
}

/// <summary>How virtual-desktop switches are performed.</summary>
public enum SwitchMethod
{
    /// <summary>Try COM internal API, fall back to keyboard shortcut on failure.</summary>
    Auto,
    ComOnly,
    KeyboardOnly,
}

/// <summary>User-configurable gesture behaviour. Persisted as JSON.</summary>
public sealed class GestureConfig
{
    // --- Trigger / enablement ---
    public bool Enabled { get; set; } = true;
    public bool Autostart { get; set; } = false;
    public TriggerButton Button { get; set; } = TriggerButton.XButton1;

    // --- Thresholds (device pixels / milliseconds) ---
    /// <summary>Horizontal travel (px) from origin required to enter gesture mode.</summary>
    public int MoveThreshold { get; set; } = 12;

    /// <summary>Px of locked-axis travel per one desktop switch (continuous stepping).</summary>
    public int StepDistance { get; set; } = 90;

    /// <summary>Hold durations under this (ms) with no step still resolve to a click; informational for tuning.</summary>
    public int ClickMaxDuration { get; set; } = 220;

    /// <summary>Horizontal must exceed vertical by this ratio to lock onto the horizontal axis.</summary>
    public double DirectionLockRatio { get; set; } = 1.5;

    /// <summary>Within this distance (px) of origin, before any step commits, the gesture is cancelled → click.</summary>
    public int CancelThreshold { get; set; } = 8;

    // --- Behaviour toggles ---
    public bool InvertDirection { get; set; } = false;
    public bool FreezeCursorDuringGesture { get; set; } = true;
    public SwitchMethod SwitchMethod { get; set; } = SwitchMethod.Auto;

    // --- Trajectory overlay ---
    public bool ShowTrajectory { get; set; } = true;

    /// <summary>ARGB packed int for the trajectory colour.</summary>
    public int TrajectoryColorArgb { get; set; } = unchecked((int)0xFF4EA1FF);
    public int TrajectoryThickness { get; set; } = 4;
    public int TrajectoryFadeMs { get; set; } = 250;

    [JsonIgnore]
    public System.Drawing.Color TrajectoryColor
    {
        get => System.Drawing.Color.FromArgb(TrajectoryColorArgb);
        set => TrajectoryColorArgb = value.ToArgb();
    }

    public GestureConfig Clone() => (GestureConfig)MemberwiseClone();
}
