namespace MouseGestureDesktop.Gesture;

/// <summary>States of the gesture recognizer.</summary>
public enum GestureState
{
    /// <summary>Nothing in progress; trigger button is up.</summary>
    Idle,

    /// <summary>Trigger button is down but intent (click vs gesture) is undecided.</summary>
    Pending,

    /// <summary>Direction locked; accumulating travel and emitting desktop switches.</summary>
    Gesturing,
}
