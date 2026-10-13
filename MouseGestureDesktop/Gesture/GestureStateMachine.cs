using System.Drawing;
using MouseGestureDesktop.Config;

namespace MouseGestureDesktop.Gesture;

/// <summary>
/// Pure gesture recognizer: consumes button/move events and produces <see cref="GestureAction"/>s.
/// Contains no Win32 calls so it can be unit-tested with synthetic event sequences.
///
/// Lifecycle: Idle --buttonDown--> Pending --(horizontal travel &gt; threshold)--> Gesturing.
/// Switching is decided only on release: a release while gesturing emits exactly one switch whose
/// direction is the sign of the net horizontal travel (no continuous stepping). A release that
/// never locked resolves to a click (preserving single/double click); returning within
/// cancelThreshold of the origin reverts to Pending (gesture cancel → click on release).
/// </summary>
public sealed class GestureStateMachine
{
    private GestureConfig _config;

    private GestureState _state = GestureState.Idle;
    private Point _origin;
    private long _downTimestamp;
    private int _lastOffset;      // latest signed horizontal offset seen while gesturing

    public GestureStateMachine(GestureConfig config) => _config = config;

    public GestureState State => _state;

    /// <summary>Replace the active config snapshot (applied on the next gesture).</summary>
    public void UpdateConfig(GestureConfig config) => _config = config;

    public void Reset()
    {
        _state = GestureState.Idle;
        _lastOffset = 0;
    }

    public GestureAction OnButtonDown(Point point, long timestamp)
    {
        if (!_config.Enabled)
            return GestureAction.PassThrough;

        // Begin custody of the button: swallow the physical down and defer the decision.
        _state = GestureState.Pending;
        _origin = point;
        _downTimestamp = timestamp;
        _lastOffset = 0;
        return GestureAction.Swallowed;
    }

    public GestureAction OnButtonUp(Point point, long timestamp)
    {
        switch (_state)
        {
            case GestureState.Pending:
                // Never became a gesture → it was a click. Replay the real button.
                _state = GestureState.Idle;
                return new GestureAction { Swallow = true, EmitClick = true, Overlay = OverlayPhase.None };

            case GestureState.Gesturing:
                _state = GestureState.Idle;
                // Direction is decided here, from the net horizontal travel: exactly one switch.
                bool movingRight = _lastOffset > 0;
                if (_config.InvertDirection)
                    movingRight = !movingRight;
                return new GestureAction
                {
                    Swallow = true,
                    SwitchCount = 1,
                    SwitchDirection = movingRight ? SwitchDirection.Right : SwitchDirection.Left,
                    Overlay = OverlayPhase.End,
                };

            default:
                return GestureAction.PassThrough;
        }
    }

    public GestureAction OnMove(Point point, long timestamp)
    {
        switch (_state)
        {
            case GestureState.Pending:
                return HandlePendingMove(point);
            case GestureState.Gesturing:
                return HandleGesturingMove(point);
            default:
                return GestureAction.PassThrough;
        }
    }

    private GestureAction HandlePendingMove(Point point)
    {
        int dx = point.X - _origin.X;
        int dy = point.Y - _origin.Y;

        bool horizontalDominates = Math.Abs(dx) >= _config.DirectionLockRatio * Math.Abs(dy);
        if (Math.Abs(dx) >= _config.MoveThreshold && horizontalDominates)
        {
            // Lock onto the horizontal axis; from here we only track travel for the overlay —
            // the switch itself is withheld until release.
            _state = GestureState.Gesturing;
            _lastOffset = dx;
            return new GestureAction
            {
                Swallow = _config.FreezeCursorDuringGesture,
                Overlay = OverlayPhase.Begin,
                OverlayOffset = dx,
            };
        }

        // Still undecided: let the cursor move freely in case this turns out to be a click.
        return GestureAction.PassThrough;
    }

    private GestureAction HandleGesturingMove(Point point)
    {
        int offset = point.X - _origin.X;

        // Gesture cancel: back near origin → revert to Pending so the release resolves to a click.
        if (Math.Abs(offset) <= _config.CancelThreshold)
        {
            _state = GestureState.Pending;
            _lastOffset = 0;
            return new GestureAction
            {
                Swallow = _config.FreezeCursorDuringGesture,
                Overlay = OverlayPhase.End,
            };
        }

        // Just track travel for visual feedback; no switch happens mid-gesture anymore.
        _lastOffset = offset;
        return new GestureAction
        {
            Swallow = _config.FreezeCursorDuringGesture,
            Overlay = OverlayPhase.Update,
            OverlayOffset = offset,
        };
    }
}
