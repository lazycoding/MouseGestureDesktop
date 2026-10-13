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
        StepDistance = 50,
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
        Assert.Equal(GestureState.Idle, sm.State);
    }

    [Fact]
    public void HorizontalSwipe_StepsRightContinuously()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);

        GestureAction lockMove = sm.OnMove(P(115, 100), 5); // crosses threshold, no step yet
        GestureAction step1 = sm.OnMove(P(160, 100), 10);   // offset 60 -> 1 step
        GestureAction step2 = sm.OnMove(P(210, 100), 15);   // offset 110 -> 1 more step

        Assert.Equal(GestureState.Gesturing, sm.State);
        Assert.Equal(OverlayPhase.Begin, lockMove.Overlay);
        Assert.Equal(0, lockMove.SwitchCount);

        Assert.Equal(1, step1.SwitchCount);
        Assert.Equal(SwitchDirection.Right, step1.SwitchDirection);
        Assert.Equal(1, step2.SwitchCount);
        Assert.Equal(SwitchDirection.Right, step2.SwitchDirection);

        GestureAction up = sm.OnButtonUp(P(210, 100), 20);
        Assert.False(up.EmitClick); // a stepped gesture suppresses the click
    }

    [Fact]
    public void LeftSwipe_StepsLeft()
    {
        var sm = New();
        sm.OnButtonDown(P(200, 100), 0);
        sm.OnMove(P(185, 100), 5);
        GestureAction step = sm.OnMove(P(140, 100), 10); // offset -60

        Assert.Equal(1, step.SwitchCount);
        Assert.Equal(SwitchDirection.Left, step.SwitchDirection);
    }

    [Fact]
    public void InvertDirection_FlipsSide()
    {
        var cfg = Config();
        cfg.InvertDirection = true;
        var sm = New(cfg);

        sm.OnButtonDown(P(100, 100), 0);
        sm.OnMove(P(115, 100), 5);
        GestureAction step = sm.OnMove(P(160, 100), 10); // physical right -> inverted to Left

        Assert.Equal(1, step.SwitchCount);
        Assert.Equal(SwitchDirection.Left, step.SwitchDirection);
    }

    [Fact]
    public void MultiStepJump_EmitsMultipleSwitches()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);
        sm.OnMove(P(115, 100), 5);
        GestureAction big = sm.OnMove(P(260, 100), 10); // offset 160 -> 3 steps at once

        Assert.Equal(3, big.SwitchCount);
        Assert.Equal(SwitchDirection.Right, big.SwitchDirection);
    }

    [Fact]
    public void ReturnToOrigin_BeforeStep_CancelsToClick()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);
        sm.OnMove(P(115, 100), 5);        // enter gesturing, no step
        GestureAction back = sm.OnMove(P(103, 100), 10); // within cancelThreshold

        Assert.Equal(GestureState.Pending, sm.State);
        Assert.Equal(OverlayPhase.End, back.Overlay);

        GestureAction up = sm.OnButtonUp(P(103, 100), 15);
        Assert.True(up.EmitClick);
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
    public void LockedButNoStep_OnRelease_ResolvesToClick()
    {
        var sm = New();
        sm.OnButtonDown(P(100, 100), 0);
        sm.OnMove(P(115, 100), 5); // gesturing, offset 15 < step 50
        GestureAction up = sm.OnButtonUp(P(115, 100), 10);

        Assert.True(up.EmitClick);
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
        Assert.False(move.Swallow); // cursor free when freeze is off
        Assert.Equal(1, move.SwitchCount);
    }
}
