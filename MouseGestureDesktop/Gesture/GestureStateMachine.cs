using System.Drawing;
using MouseGestureDesktop.Config;

namespace MouseGestureDesktop.Gesture;

/// <summary>
/// Pure gesture recognizer: consumes button/move events and produces <see cref="GestureAction"/>s.
/// Contains no Win32 calls so it can be unit-tested with synthetic event sequences.
///
/// Lifecycle: Idle --buttonDown--> Pending --(horizontal travel &gt; threshold)--> Gesturing.
/// A release that never stepped resolves to a click (preserving single/double click);
/// a release after stepping suppresses the click. Returning within cancelThreshold before the
/// first step reverts to Pending (gesture cancel).
/// </summary>
public sealed class GestureStateMachine
{
    private GestureConfig _config;

    private GestureState _state = GestureState.Idle;
    private Point _origin;
    private long _downTimestamp;
    private int _stepsFired;      // signed count already emitted along locked axis
    private bool _firstStepFired;

    public GestureStateMachine(GestureConfig config) => _config = config;

    public GestureState State => _state;

    /// <summary>Replace the active config snapshot (applied on the next gesture).</summary>
    public void UpdateConfig(GestureConfig config) => _config = config;

    public void Reset()
    {
        _state = GestureState.Idle;
        _stepsFired = 0;
        _firstStepFired = false;
    }

    public GestureAction OnButtonDown(Point point, long timestamp)
    {
        if (!_config.Enabled)
            return GestureAction.PassThrough;

        // Begin custody of the button: swallow the physical down and defer the decision.
        _state = GestureState.Pending;
        _origin = point;
        _downTimestamp = timestamp;
        _stepsFired = 0;
        _firstStepFired = false;
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
                // Locked but never completed a step → fall back to a click so the button still works.
                bool emitClick = !_firstStepFired;
                return new GestureAction
                {
                    Swallow = true,
                    EmitClick = emitClick,
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
            // Lock onto the horizontal axis and start gesturing from this same event.
            _state = GestureState.Gesturing;
            return BuildGestureMove(dx, OverlayPhase.Begin);
        }

        // Still undecided: let the cursor move freely in case this turns out to be a click.
        return GestureAction.PassThrough;
    }

    private GestureAction HandleGesturingMove(Point point)
    {
        int offset = point.X - _origin.X;

        // Gesture cancel: back near origin before any step committed → revert to Pending.
        if (!_firstStepFired && Math.Abs(offset) <= _config.CancelThreshold)
        {
            _state = GestureState.Pending;
            return new GestureAction
            {
                Swallow = _config.FreezeCursorDuringGesture,
                Overlay = OverlayPhase.End,
            };
        }

        return BuildGestureMove(offset, OverlayPhase.Update);
    }

    private GestureAction BuildGestureMove(int offset, OverlayPhase defaultPhase)
    {
        int step = Math.Max(1, _config.StepDistance);
        int desiredSteps = offset / step;              // truncates toward zero, signed
        int delta = desiredSteps - _stepsFired;

        int switchCount = 0;
        SwitchDirection direction = SwitchDirection.Right;
        OverlayPhase phase = defaultPhase;

        if (delta != 0)
        {
            switchCount = Math.Abs(delta);
            bool movingRight = delta > 0;
            if (_config.InvertDirection)
                movingRight = !movingRight;
            direction = movingRight ? SwitchDirection.Right : SwitchDirection.Left;

            _stepsFired = desiredSteps;
            _firstStepFired = true;
            phase = OverlayPhase.Step;
        }

        return new GestureAction
        {
            Swallow = _config.FreezeCursorDuringGesture,
            SwitchCount = switchCount,
            SwitchDirection = direction,
            Overlay = phase,
            OverlayOffset = offset,
        };
    }
}
