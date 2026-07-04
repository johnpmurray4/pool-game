using PoolGame.Core;
using PoolGame.Core.Rules;

namespace PoolGame.Core.Tests;

/// <summary>Physics and rules together: real simulated shots judged by the
/// state machine, as the game layer will use them.</summary>
public class IntegrationTests
{
    private static readonly PhysicsConfig Cfg = new();

    [Fact]
    public void RealBreakShot_JudgedWithoutError_AndGameContinues()
    {
        var sim = new Simulation(Cfg);
        var game = new BlackballGame(PlayerId.One);
        var table = Rack.BlackballBreak(Cfg);

        var result = sim.Run(table, new Shot(new Vec2(7.0, 0.05), Vec3.Zero));
        var verdict = game.ApplyShot(table, result);

        // A firm centre break must never be judged no-contact, and the rack
        // continues (win/loss on a break is impossible; black pot re-racks).
        Assert.NotEqual(FoulReason.NoContact, verdict.Foul);
        Assert.Equal(RackState.InProgress, game.Rack);
        Assert.False(game.IsBreakShot ? verdict.IsFoul : false); // re-rack only via black
    }

    [Fact]
    public void GentleShot_ThatMovesNothingFar_IsJudgedFoul()
    {
        var sim = new Simulation(Cfg);
        var game = new BlackballGame(PlayerId.One);
        var table = Rack.BlackballBreak(Cfg);

        // Tap the pack so softly nothing pots, nothing crosses the centre
        // string, and no ball reaches a cushion: an illegal break.
        var result = sim.Run(table, new Shot(new Vec2(0.8, 0), Vec3.Zero));
        var verdict = game.ApplyShot(table, result);

        Assert.True(verdict.IsFoul);
        Assert.True(verdict.FreeShotNext);
        Assert.Equal(PlayerId.Two, game.CurrentPlayer);
    }

    [Fact]
    public void SimulatedStraightPot_AwardsContinuation()
    {
        var sim = new Simulation(Cfg);
        var game = new BlackballGame(PlayerId.One);

        // Past the break: hand-build an open table with a red hanging over the
        // top-right pocket and the cue lined up straight at it.
        Vec2 pocket = new(Cfg.TableLength / 2, Cfg.TableWidth / 2);
        Vec2 dir = new Vec2(1, 1).Normalized();
        var table = new TableState
        {
            Balls = new[]
            {
                new Ball { Id = 0, Color = BallColor.Cue, Radius = Cfg.CueBallRadius, Mass = Cfg.CueBallMass, Position = pocket - dir * 0.5 },
                new Ball { Id = 1, Color = BallColor.Red, Radius = Cfg.ObjectBallRadius, Mass = Cfg.ObjectBallMass, Position = pocket - dir * 0.2 },
                new Ball { Id = 8, Color = BallColor.Yellow, Radius = Cfg.ObjectBallRadius, Mass = Cfg.ObjectBallMass, Position = new Vec2(-0.4, -0.2) },
                new Ball { Id = 15, Color = BallColor.Black, Radius = Cfg.ObjectBallRadius, Mass = Cfg.ObjectBallMass, Position = new Vec2(-0.4, 0.2) },
            },
        };

        // Skip past the break state with a scripted legal break first.
        var breakGame = SkipBreak(game, table);

        var result = sim.Run(table, new Shot(dir * 1.8, Vec3.Zero));
        var verdict = breakGame.ApplyShot(table, result);

        Assert.False(verdict.IsFoul);
        Assert.True(verdict.ShooterContinues);
        Assert.Equal(BallColor.Red, verdict.GroupAssignedToShooter);
    }

    private static BlackballGame SkipBreak(BlackballGame game, TableState table)
    {
        var events = new List<SimEvent>
        {
            new(0.1, SimEventType.BallCollision, 0, 1),
            new(0.2, SimEventType.CushionHit, 1, 0),
            new(1.0, SimEventType.Settled, -1),
        };
        var after = table.Clone();
        after.ById(1)!.Position = new Vec2(0.3, 0.3); // pretend two balls crossed
        after.ById(8)!.Position = new Vec2(0.3, -0.3);
        var moved = table.Clone();
        moved.ById(1)!.Position = new Vec2(-0.3, 0.3);
        moved.ById(8)!.Position = new Vec2(-0.3, -0.3);
        game.ApplyShot(moved, new ShotResult(events, after));
        return game;
    }
}
