using System.Drawing;
using System.Windows.Forms;
using MouseGestureDesktop.Config;
using MouseGestureDesktop.Desktop;
using MouseGestureDesktop.Gesture;
using MouseGestureDesktop.Hooks;
using MouseGestureDesktop.Input;
using MouseGestureDesktop.Overlay;

namespace MouseGestureDesktop.App;

/// <summary>
/// Wires the mouse hook to the gesture state machine, then drives the desktop switcher,
/// click synthesizer and trajectory overlay. Lives on the UI thread.
/// </summary>
public sealed class GestureController : IDisposable
{
    private readonly LowLevelMouseHook _hook;
    private readonly GestureStateMachine _stateMachine;
    private readonly InputSynthesizer _input;
    private readonly GestureOverlayWindow _overlay;

    private GestureConfig _config;
    private IVirtualDesktopSwitcher _switcher;
    private Point _origin;

    // Cursor-freeze bookkeeping: while frozen we swallow WM_MOUSEMOVE, which pins the OS cursor,
    // so raw event points stop advancing. _virtualX reconstructs the cumulative locked-axis
    // position from per-event deltas measured against _lockX (the pinned point).
    private int _virtualX;
    private int _lockX;
    private bool _locked;

    public GestureController(GestureConfig config)
    {
        _config = config;
        _input = new InputSynthesizer();
        _stateMachine = new GestureStateMachine(config);
        _switcher = VirtualDesktopSwitcherFactory.Create(config.SwitchMethod, _input);
        _overlay = new GestureOverlayWindow();
        _ = _overlay.Handle; // force handle creation so BeginInvoke works before first Show

        _hook = new LowLevelMouseHook();
        _hook.MouseEvent += OnMouseEvent;
    }

    public string BackendName => _switcher.BackendName;

    public void Start() => _hook.Install();

    public void Stop() => _hook.Uninstall();

    public void ApplyConfig(GestureConfig config)
    {
        bool methodChanged = config.SwitchMethod != _config.SwitchMethod;
        _config = config;
        _stateMachine.UpdateConfig(config);
        if (methodChanged)
            _switcher = VirtualDesktopSwitcherFactory.Create(config.SwitchMethod, _input);
    }

    private void OnMouseEvent(object? sender, MouseHookEventArgs e)
    {
        // Never react to our own synthesized input, or when disabled.
        if (e.Injected || !_config.Enabled)
            return;

        GestureAction action;
        switch (e.Type)
        {
            case MouseMessageType.XButtonDown when IsTriggerButton(e.Button):
                _origin = e.Point;
                _virtualX = e.Point.X;
                _lockX = e.Point.X;
                _locked = false;
                action = _stateMachine.OnButtonDown(e.Point, e.Timestamp);
                break;

            case MouseMessageType.XButtonUp when IsTriggerButton(e.Button):
                _locked = false;
                action = _stateMachine.OnButtonUp(e.Point, e.Timestamp);
                break;

            case MouseMessageType.Move:
            {
                GestureState before = _stateMachine.State;
                Point gesturePoint = ToGesturePoint(e.Point, before);
                action = _stateMachine.OnMove(gesturePoint, e.Timestamp);
                AnchorCursorLock(before, _stateMachine.State, e.Point);
                break;
            }

            default:
                return;
        }

        e.Handled = action.Swallow;

        // Defer side effects so the low-level hook callback returns promptly (avoids hook timeout).
        if (HasSideEffects(action))
            _overlay.BeginInvoke(() => ApplySideEffects(action));
    }

    private static bool HasSideEffects(GestureAction a) =>
        a.EmitClick || a.SwitchCount > 0 || a.Overlay != OverlayPhase.None;

    /// <summary>
    /// Converts a raw hook point into the coordinate the (cursor-free) state machine expects.
    /// When the cursor is frozen we swallow moves, which pins the OS cursor at the point where
    /// gesturing began; from then on each event reports that pinned point plus only the latest
    /// hardware delta, so <c>point.X - origin.X</c> stops growing and no switch ever fires.
    /// Accumulate the per-event delta (measured against the pinned <see cref="_lockX"/>) into a
    /// virtual X so the gesture keeps advancing while the cursor stays put.
    /// </summary>
    private Point ToGesturePoint(Point raw, GestureState state)
    {
        if (_locked && _config.FreezeCursorDuringGesture && state == GestureState.Gesturing)
        {
            _virtualX += raw.X - _lockX;
            return new Point(_virtualX, raw.Y);
        }
        return raw;
    }

    /// <summary>Anchors the virtual coordinate to the pin point the moment gesturing starts.</summary>
    private void AnchorCursorLock(GestureState before, GestureState after, Point raw)
    {
        if (_config.FreezeCursorDuringGesture && after == GestureState.Gesturing && before != GestureState.Gesturing)
        {
            _lockX = raw.X;      // the OS cursor is now pinned here
            _virtualX = raw.X;
            _locked = true;
        }
        else if (after != GestureState.Gesturing)
        {
            _locked = false;
        }
    }

    private void ApplySideEffects(GestureAction action)
    {
        try
        {
            switch (action.Overlay)
            {
                case OverlayPhase.Begin:
                    _overlay.Begin(_origin, _config);
                    _overlay.Update(action.OverlayOffset);
                    break;
                case OverlayPhase.Update:
                case OverlayPhase.Step:
                    _overlay.Update(action.OverlayOffset);
                    break;
                case OverlayPhase.End:
                    _overlay.End();
                    break;
            }

            for (int i = 0; i < action.SwitchCount; i++)
                _switcher.TrySwitch(action.SwitchDirection);

            if (action.EmitClick)
                _input.ClickSideButton(_config.Button);
        }
        catch
        {
            // A failed side effect must not tear down the hook.
        }
    }

    private bool IsTriggerButton(XButton button) => _config.Button switch
    {
        TriggerButton.XButton1 => button == XButton.XButton1,
        TriggerButton.XButton2 => button == XButton.XButton2,
        _ => false,
    };

    public void Dispose()
    {
        _hook.MouseEvent -= OnMouseEvent;
        _hook.Dispose();
        _overlay.Dispose();
    }
}
