using PoolGame.Core;

namespace PoolGame.Core.Tests;

public class SimulationTests
{
    private static readonly PhysicsConfig Cfg = new();
    private readonly Simulation _sim = new(Cfg);

    private static Ball Cue(Vec2 pos) => new()
    {
        Id = 0, Color = BallColor.Cue,
        Radius = Cfg.CueBallRadius, Mass = Cfg.CueBallMass, Position = pos,
    };

    private static Ball Object(int id, Vec2 pos, BallColor color = BallColor.Red) => new()
    {
        Id = id, Color = color,
        Radius = Cfg.ObjectBallRadius, Mass = Cfg.ObjectBallMass, Position = pos,
    };

    private static TableState Table(params Ball[] balls) => new() { Balls = balls };

    [Fact]
    public void HeadOnCollision_TransfersMomentumForward()
    {
        var state = Table(Cue(new Vec2(-0.3, 0)), Object(1, new Vec2(0, 0)));
        var result = _sim.Run(state, new Shot(new Vec2(1.5, 0), Vec3.Zero));

        Assert.Contains(result.Events, e => e.Type == SimEventType.BallCollision);
        Assert.Equal(1, result.FirstContactBallId);

        // Object ball must have travelled forward from the contact.
        var obj = result.FinalState.ById(1)!;
        Assert.True(obj.Position.X > 0.05, $"object ball only reached {obj.Position.X}");
    }

    [Fact]
    public void StraightShot_PotsBallIntoCornerPocket()
    {
        // Object ball placed on the diagonal toward the top-right corner pocket,
        // cue ball directly behind the line.
        Vec2 pocket = new(Cfg.TableLength / 2, Cfg.TableWidth / 2);
        Vec2 objPos = pocket - new Vec2(0.15, 0.15).Normalized() * 0.2;
        Vec2 cuePos = objPos - new Vec2(0.15, 0.15).Normalized() * 0.3;

        var state = Table(Cue(cuePos), Object(1, objPos));
        Vec2 aim = (objPos - cuePos).Normalized();
        var result = _sim.Run(state, new Shot(aim * 2.0, Vec3.Zero));

        Assert.Contains(result.Events, e => e.Type == SimEventType.BallPotted && e.BallId == 1);
        Assert.True(result.FinalState.ById(1)!.Potted);
        Assert.False(result.FinalState.CueBall.Potted);
    }

    [Fact]
    public void CushionBounce_ReversesVelocityComponent()
    {
        var state = Table(Cue(new Vec2(0, 0)));
        var result = _sim.Run(state, new Shot(new Vec2(1.0, 0), Vec3.Zero));

        Assert.Contains(result.Events, e => e.Type == SimEventType.CushionHit && e.BallId == 0);
        // Ball ends up left of where a cushion-less roll would have put it.
        Assert.True(result.FinalState.CueBall.Position.X < Cfg.TableLength / 2 - Cfg.CueBallRadius);
    }

    [Fact]
    public void SlowRoll_StopsWithoutReachingCushion()
    {
        var state = Table(Cue(new Vec2(0, 0)));
        var result = _sim.Run(state, new Shot(new Vec2(0.2, 0), Vec3.Zero));

        Assert.DoesNotContain(result.Events, e => e.Type == SimEventType.CushionHit);
        Assert.Equal(SimEventType.Settled, result.Events[^1].Type);
        Assert.True(result.FinalState.CueBall.Position.X > 0);
    }

    [Fact]
    public void Shot_IsDeterministic()
    {
        var state = Rack.BlackballBreak(Cfg);
        var shot = new Shot(new Vec2(6.0, 0.3), new Vec3(0, 0, 15));

        var r1 = _sim.Run(state, shot);
        var r2 = _sim.Run(state, shot);

        Assert.Equal(r1.Events.Count, r2.Events.Count);
        for (int i = 0; i < r1.Events.Count; i++)
            Assert.Equal(r1.Events[i], r2.Events[i]);
        for (int i = 0; i < r1.FinalState.Balls.Count; i++)
        {
            Assert.Equal(r1.FinalState.Balls[i].Position, r2.FinalState.Balls[i].Position);
            Assert.Equal(r1.FinalState.Balls[i].Potted, r2.FinalState.Balls[i].Potted);
        }
    }

    [Fact]
    public void BreakShot_SettlesAndScattersRack()
    {
        var state = Rack.BlackballBreak(Cfg);
        var result = _sim.Run(state, new Shot(new Vec2(7.0, 0.05), Vec3.Zero));

        Assert.Equal(SimEventType.Settled, result.Events[^1].Type);
        Assert.Contains(result.Events, e => e.Type == SimEventType.BallCollision);

        // The pack must actually scatter: mean displacement of object balls > 5 cm.
        double meanMove = state.Balls.Where(b => !b.IsCue)
            .Zip(result.FinalState.Balls.Where(b => !b.IsCue),
                 (before, after) => after.Potted ? 0.3 : (after.Position - before.Position).Length)
            .Average();
        Assert.True(meanMove > 0.05, $"rack barely moved: mean {meanMove:F4} m");

        // Nothing may end up outside the cushions or overlapping another ball.
        foreach (var b in result.FinalState.Balls.Where(b => b.InPlay))
        {
            Assert.True(Math.Abs(b.Position.X) <= Cfg.TableLength / 2 + 1e-6);
            Assert.True(Math.Abs(b.Position.Y) <= Cfg.TableWidth / 2 + 1e-6);
        }
        var inPlay = result.FinalState.Balls.Where(b => b.InPlay).ToList();
        for (int i = 0; i < inPlay.Count; i++)
        for (int j = i + 1; j < inPlay.Count; j++)
        {
            double gap = (inPlay[i].Position - inPlay[j].Position).Length
                         - (inPlay[i].Radius + inPlay[j].Radius);
            Assert.True(gap > -1e-4, $"balls {inPlay[i].Id},{inPlay[j].Id} overlap by {-gap:E2} m");
        }
    }

    [Fact]
    public void Rack_HasCorrectBallCounts()
    {
        var state = Rack.BlackballBreak(Cfg);
        Assert.Equal(16, state.Balls.Count);
        Assert.Equal(7, state.Balls.Count(b => b.Color == BallColor.Red));
        Assert.Equal(7, state.Balls.Count(b => b.Color == BallColor.Yellow));
        Assert.Equal(1, state.Balls.Count(b => b.Color == BallColor.Black));
        Assert.Equal(1, state.Balls.Count(b => b.IsCue));
    }

    [Fact]
    public void Run_DoesNotMutateInputState()
    {
        var state = Rack.BlackballBreak(Cfg);
        Vec2 cueBefore = state.CueBall.Position;
        _sim.Run(state, new Shot(new Vec2(5, 0), Vec3.Zero));
        Assert.Equal(cueBefore, state.CueBall.Position);
        Assert.All(state.Balls, b => Assert.False(b.Moving));
    }
}
