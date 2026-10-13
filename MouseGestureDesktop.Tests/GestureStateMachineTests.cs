using System.Drawing;
using MouseGestureDesktop.Config;
using MouseGestureDesktop.Gesture;
using Xunit;

namespace MouseGestureDesktop.Tests;

public class GestureStateMachineTests
{
    private static GestureConfig Config() => new()
    {
        Enabled = true,
        Button = TriggerButton.XButton1,
        MoveThreshold = 10,
        CancelThreshold = 5,
        DirectionLockRatio = 1.5,
        InvertDirection = false,
        FreezeCursorDuringGesture = true,
    };

    private static GestureStateMachine New(GestureConfig? c = null) => new(c ?? Config());

    private static Point P(int x, int y) => new(x, y);

    [Fact]
    public void QuickTap_ResolvesToClick()
    {
        var sm = New();
        GestureAction down = sm.OnButtonDown(P(100, 100), 0);
        GestureAction up = sm.OnButtonUp(P(100, 100), 10);

        Assert.True(down.Swallow);
        Assert.True(up.Swallow);
        Assert.True(up.EmitClick);
        Assert.Equal(0, up.SwitchCount);
        Assert.Equal(GestureState.Idle, sm.State);
    }

    [Fact]
    public void HorizontalSwipe_SwitchesOnceOnRelease_Right()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);

        GestureAction lockMove = sm.OnMove(P(115, 100), 5); // crosses threshold → gesturing
        GestureAction move = sm.OnMove(P(260, 100), 10);    // keeps sliding right

        Assert.Equal(GestureState.Gesturing, sm.State);
        Assert.Equal(OverlayPhase.Begin, lockMove.Overlay);
        // No switch fires mid-gesture anymore.
        Assert.Equal(0, lockMove.SwitchCount);
        Assert.Equal(0, move.SwitchCount);

        GestureAction up = sm.OnButtonUp(P(260, 100), 20);
        Assert.Equal(1, up.SwitchCount);                    // exactly one switch
        Assert.Equal(SwitchDirection.Right, up.SwitchDirection);
        Assert.False(up.EmitClick);                         // a committed gesture suppresses the click
        Assert.Equal(GestureState.Idle, sm.State);
    }

    [Fact]
    public void LeftSwipe_SwitchesLeftOnRelease()
    {
        var sm = New();
        sm.OnButtonDown(P(200, 100), 0);
        sm.OnMove(P(185, 100), 5);
        sm.OnMove(P(120, 100), 10); // offset -80

        GestureAction up = sm.OnButtonUp(P(120, 100), 15);
        Assert.Equal(1, up.SwitchCount);
        Assert.Equal(SwitchDirection.Left, up.SwitchDirection);
    }

    [Fact]
    public void InvertDirection_FlipsSide()
    {
        var cfg = Config();
        cfg.InvertDirection = true;
        var sm = New(cfg);

        sm.OnButtonDown(P(100, 100), 0);
        sm.OnMove(P(115, 100), 5);
        sm.OnMove(P(160, 100), 10); // physical right

        GestureAction up = sm.OnButtonUp(P(160, 100), 15); // inverted → Left
        Assert.Equal(1, up.SwitchCount);
        Assert.Equal(SwitchDirection.Left, up.SwitchDirection);
    }

    [Fact]
    public void LargeSwipe_StillSwitchesOnlyOnce()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);
        sm.OnMove(P(115, 100), 5);
        GestureAction big = sm.OnMove(P(600, 100), 10); // far slide, offset 500

        Assert.Equal(0, big.SwitchCount); // nothing mid-gesture

        GestureAction up = sm.OnButtonUp(P(600, 100), 15);
        Assert.Equal(1, up.SwitchCount);  // never more than one
        Assert.Equal(SwitchDirection.Right, up.SwitchDirection);
    }

    [Fact]
    public void ReturnToOrigin_CancelsToClick()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);
        sm.OnMove(P(160, 100), 5);        // enter gesturing
        GestureAction back = sm.OnMove(P(103, 100), 10); // within cancelThreshold

        Assert.Equal(GestureState.Pending, sm.State);
        Assert.Equal(OverlayPhase.End, back.Overlay);

        GestureAction up = sm.OnButtonUp(P(103, 100), 15);
        Assert.True(up.EmitClick);
        Assert.Equal(0, up.SwitchCount);
    }

    [Fact]
    public void VerticalMovement_DoesNotLock()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);
        GestureAction move = sm.OnMove(P(100, 140), 5); // pure vertical

        Assert.Equal(GestureState.Pending, sm.State);
        Assert.False(move.Swallow);

        GestureAction up = sm.OnButtonUp(P(100, 140), 10);
        Assert.True(up.EmitClick);
        Assert.Equal(0, up.SwitchCount);
    }

    [Fact]
    public void DiagonalBelowRatio_DoesNotLock()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);
        // dx=12 (>threshold) but dy=20 so 12 < 1.5*20 -> not horizontal-dominant
        GestureAction move = sm.OnMove(P(112, 120), 5);

        Assert.Equal(GestureState.Pending, sm.State);
        Assert.False(move.Swallow);
    }

    [Fact]
    public void Disabled_PassesThrough()
    {
        var cfg = Config();
        cfg.Enabled = false;
        var sm = New(cfg);

        GestureAction down = sm.OnButtonDown(P(100, 100), 0);
        Assert.False(down.Swallow);
        Assert.Equal(GestureState.Idle, sm.State);
    }

    [Fact]
    public void FreezeDisabled_DoesNotSwallowGestureMoves()
    {
        var cfg = Config();
        cfg.FreezeCursorDuringGesture = false;
        var sm = New(cfg);

        sm.OnButtonDown(P(100, 100), 0);
        GestureAction move = sm.OnMove(P(160, 100), 5);

        Assert.Equal(GestureState.Gesturing, sm.State);
        Assert.False(move.Swallow);     // cursor free when freeze is off
        Assert.Equal(0, move.SwitchCount); // still no mid-gesture switch

        GestureAction up = sm.OnButtonUp(P(160, 100), 10);
        Assert.Equal(1, up.SwitchCount);
        Assert.Equal(SwitchDirection.Right, up.SwitchDirection);
    }
}
