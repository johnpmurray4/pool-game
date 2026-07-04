using PoolGame.Core;

namespace PoolGame.Core.Tests;

public class CueStrikeTests
{
    private const double R = 0.0254;

    [Fact]
    public void CentreBallHit_HasNoSpin()
    {
        var shot = CueStrike.Straight(2.0, new Vec2(1, 0), R);
        Assert.Equal(2.0, shot.CueVelocity.Length, 9);
        Assert.Equal(0, shot.CueAngularVelocity.Length, 9);
    }

    [Fact]
    public void StrikeAtTwoFifthsAboveCentre_GivesImmediateNaturalRoll()
    {
        // The classic result: tip contact at 0.4R above centre imparts exactly
        // rolling spin, so the ball never slides.
        var shot = CueStrike.Create(2.0, new Vec2(1, 0), 0, 0.4, R);
        var cfg = new PhysicsConfig();
        var ball = new Ball
        {
            Id = 0, Color = BallColor.Cue, Radius = R, Mass = cfg.CueBallMass,
            Velocity = shot.CueVelocity, AngularVelocity = shot.CueAngularVelocity,
        };
        Assert.Equal(0, ball.SurfaceVelocity.Length, 6);
    }

    [Fact]
    public void BelowCentre_GivesBackspin()
    {
        var shot = CueStrike.Create(2.0, new Vec2(1, 0), 0, -0.4, R);
        // Backspin for +X travel: negative ω_y (opposite the natural-roll axis).
        Assert.True(shot.CueAngularVelocity.Y < 0);
        Assert.Equal(0, shot.CueAngularVelocity.Z, 9);
    }

    [Fact]
    public void SideOffset_GivesOnlySideSpin()
    {
        var shot = CueStrike.Create(2.0, new Vec2(1, 0), 0.3, 0, R);
        Assert.NotEqual(0, shot.CueAngularVelocity.Z);
        Assert.Equal(0, shot.CueAngularVelocity.X, 9);
        Assert.Equal(0, shot.CueAngularVelocity.Y, 9);
    }

    [Fact]
    public void ExcessiveOffset_IsClampedToMiscueLimit()
    {
        var extreme = CueStrike.Create(2.0, new Vec2(1, 0), 0.9, 0.9, R);
        var atLimit = CueStrike.Create(2.0, new Vec2(1, 0),
            CueStrike.MaxTipOffset / Math.Sqrt(2), CueStrike.MaxTipOffset / Math.Sqrt(2), R);
        Assert.Equal(atLimit.CueAngularVelocity, extreme.CueAngularVelocity);
    }

    [Fact]
    public void SpinAxes_RotateWithAimDirection()
    {
        var north = CueStrike.Create(2.0, new Vec2(0, 1), 0, 0.4, R);
        // Topspin for +Y travel spins about −X.
        Assert.True(north.CueAngularVelocity.X < 0);
        Assert.Equal(0, north.CueAngularVelocity.Y, 9);
    }
}
