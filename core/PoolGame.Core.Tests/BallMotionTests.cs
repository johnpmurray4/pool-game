using PoolGame.Core;

namespace PoolGame.Core.Tests;

public class BallMotionTests
{
    private static readonly PhysicsConfig Cfg = new();

    private static Ball MakeBall(Vec2 velocity, Vec3? spin = null, MotionState state = MotionState.Sliding) => new()
    {
        Id = 1, Color = BallColor.Red,
        Radius = Cfg.ObjectBallRadius, Mass = Cfg.ObjectBallMass,
        Position = Vec2.Zero, Velocity = velocity,
        AngularVelocity = spin ?? Vec3.Zero, State = state,
    };

    [Fact]
    public void RollingBall_StopsAtExpectedDistance()
    {
        // Rolling from v: stopping distance = v² / (2 μr g)
        var b = MakeBall(new Vec2(1.0, 0), state: MotionState.Rolling);
        double expected = 1.0 / (2 * Cfg.RollingFriction * Cfg.Gravity);

        var seg = BallMotion.CurrentSegment(b, Cfg);
        BallMotion.Advance(b, seg.Duration, Cfg);

        Assert.Equal(MotionState.Stationary, b.State);
        Assert.Equal(expected, b.Position.X, 3);
        Assert.Equal(0, b.Velocity.Length, 9);
    }

    [Fact]
    public void SlidingBall_TransitionsToRollingAtFormulaTime()
    {
        // Struck dead centre (no spin): u0 = v0, slide ends at 2v/(7 μs g).
        var b = MakeBall(new Vec2(2.0, 0));
        double expected = 2 * 2.0 / (7 * Cfg.SlidingFriction * Cfg.Gravity);

        var seg = BallMotion.CurrentSegment(b, Cfg);
        Assert.Equal(expected, seg.Duration, 9);

        BallMotion.Advance(b, seg.Duration, Cfg);
        Assert.Equal(MotionState.Rolling, b.State);
        // At natural roll onset velocity is 5/7 of the initial strike velocity.
        Assert.Equal(2.0 * 5 / 7, b.Velocity.X, 6);
        // Surface velocity must now be ~zero.
        Assert.Equal(0, b.SurfaceVelocity.Length, 6);
    }

    [Fact]
    public void BackspinBall_SlowsFasterThanStunShot()
    {
        var stun = MakeBall(new Vec2(2.0, 0));
        var draw = MakeBall(new Vec2(2.0, 0), new Vec3(0, -100, 0)); // heavy backspin

        // Backspin increases |u0|, so the slide phase lasts longer and sheds
        // more speed before natural roll.
        var stunSeg = BallMotion.CurrentSegment(stun, Cfg);
        var drawSeg = BallMotion.CurrentSegment(draw, Cfg);
        Assert.True(drawSeg.Duration > stunSeg.Duration);

        BallMotion.Advance(stun, stunSeg.Duration, Cfg);
        BallMotion.Advance(draw, drawSeg.Duration, Cfg);
        Assert.True(draw.Velocity.X < stun.Velocity.X);
    }

    [Fact]
    public void SideSpin_DecaysToZero()
    {
        var b = MakeBall(Vec2.Zero, new Vec3(0, 0, 50), MotionState.Stationary);
        BallMotion.Advance(b, 10.0, Cfg);
        Assert.Equal(0, b.AngularVelocity.Z);
    }

    [Fact]
    public void Reclassify_StationaryWhenSlow()
    {
        var b = MakeBall(new Vec2(1e-6, 0));
        BallMotion.Reclassify(b, Cfg);
        Assert.Equal(MotionState.Stationary, b.State);
    }
}
