using PoolGame.Core;
using PoolGame.Core.Ai;
using PoolGame.Core.Rules;

namespace PoolGame.Core.Tests;

public class AiTests
{
    private static readonly PhysicsConfig Cfg = new();
    private readonly Simulation _sim = new(Cfg);

    private static Ball MakeBall(int id, BallColor color, Vec2 pos) => new()
    {
        Id = id, Color = color,
        Radius = color == BallColor.Cue ? Cfg.CueBallRadius : Cfg.ObjectBallRadius,
        Mass = color == BallColor.Cue ? Cfg.CueBallMass : Cfg.ObjectBallMass,
        Position = pos,
    };

    /// <summary>Open table, past the break, P1 to shoot.</summary>
    private static BlackballGame PastBreak(TableState table)
    {
        var game = new BlackballGame(PlayerId.One);
        var events = new List<SimEvent>
        {
            new(0.1, SimEventType.BallCollision, 0, table.Balls.First(b => !b.IsCue).Id),
            new(0.2, SimEventType.BallPotted, table.Balls.First(b => b.Color == BallColor.Red).Id, 0),
            new(1.0, SimEventType.Settled, -1),
        };
        // Judge against a throwaway clone so `table` itself stays pristine.
        var scratch = table.Clone();
        var after = table.Clone();
        after.Balls.First(b => b.Color == BallColor.Red).Potted = true;
        game.ApplyShot(scratch, new ShotResult(events, after));
        Assert.Equal(PlayerId.One, game.CurrentPlayer); // potted on break → continues
        return game;
    }

    private static TableState HangingPotTable()
    {
        Vec2 pocket = new(Cfg.TableLength / 2, Cfg.TableWidth / 2);
        Vec2 dir = new Vec2(1, 1).Normalized();
        return new TableState
        {
            Balls = new[]
            {
                MakeBall(0, BallColor.Cue, pocket - dir * 0.45),
                MakeBall(1, BallColor.Red, pocket - dir * 0.15),
                MakeBall(8, BallColor.Yellow, new Vec2(-0.5, -0.25)),
                MakeBall(15, BallColor.Black, new Vec2(-0.5, 0.25)),
            },
        };
    }

    [Fact]
    public void GeneratesPotCandidates_ForHangingBall()
    {
        var table = HangingPotTable();
        var game = PastBreak(table);
        var candidates = new CandidateGenerator(Cfg).Generate(table, game);

        Assert.Contains(candidates, c => c.Kind == CandidateKind.Pot && c.TargetBallId == 1);
    }

    [Fact]
    public void HardAi_PotsALegalBall_AndContinues()
    {
        // Open table with an easy red hanging — the AI may equally choose the
        // yellow if the position afterwards is better; what matters is a clean
        // pot and a continued visit.
        var table = HangingPotTable();
        var game = PastBreak(table);
        var ai = new AiPlayer(_sim, AiDifficulty.Hard);

        Shot shot = ai.ChooseShot(table, game, seed: 42);
        var result = _sim.Run(table, shot);
        var verdict = game.ApplyShot(table, result);

        Assert.False(verdict.IsFoul);
        Assert.True(verdict.ShooterContinues, "hard AI failed to pot from an easy position");
        Assert.True(result.FinalState.Balls.Any(b =>
            b.Potted && b.Color is BallColor.Red or BallColor.Yellow));
    }

    [Fact]
    public void Ai_NeverTargetsBlack_WhileGroupRemains()
    {
        // Black hangs over a pocket, tempting; red is awkward. AI must not
        // shoot at the black (instant loss).
        Vec2 pocket = new(Cfg.TableLength / 2, Cfg.TableWidth / 2);
        Vec2 dir = new Vec2(1, 1).Normalized();
        var table = new TableState
        {
            Balls = new[]
            {
                MakeBall(0, BallColor.Cue, pocket - dir * 0.45),
                MakeBall(1, BallColor.Red, new Vec2(-0.6, -0.3)),
                MakeBall(8, BallColor.Yellow, new Vec2(-0.5, -0.25)),
                MakeBall(15, BallColor.Black, pocket - dir * 0.15),
            },
        };
        var game = PastBreak(table);
        var candidates = new CandidateGenerator(Cfg).Generate(table, game);

        Assert.DoesNotContain(candidates, c => c.TargetBallId == 15);
    }

    [Fact]
    public void Ai_TargetsBlack_AfterClearingGroup()
    {
        Vec2 pocket = new(Cfg.TableLength / 2, Cfg.TableWidth / 2);
        Vec2 dir = new Vec2(1, 1).Normalized();
        var table = new TableState
        {
            Balls = new[]
            {
                MakeBall(0, BallColor.Cue, pocket - dir * 0.45),
                MakeBall(8, BallColor.Yellow, new Vec2(-0.5, -0.25)),
                MakeBall(15, BallColor.Black, pocket - dir * 0.15),
            },
        };
        // P1 on reds with none left → black is the ball on.
        var game = AssignedRedsGame();
        var ai = new AiPlayer(_sim, AiDifficulty.Hard);

        Shot shot = ai.ChooseShot(table, game, seed: 7);
        var result = _sim.Run(table, shot);
        var verdict = game.ApplyShot(table, result);

        Assert.Equal(RackState.PlayerOneWins, verdict.Rack);
    }

    [Fact]
    public void ChooseShot_IsDeterministicPerSeed()
    {
        var table = HangingPotTable();
        var game = PastBreak(table);
        var ai = new AiPlayer(_sim, AiDifficulty.Medium);

        Shot a = ai.ChooseShot(table, game, seed: 123);
        Shot b = ai.ChooseShot(table, game, seed: 123);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Snookered_StillProducesAShot()
    {
        // Cue boxed in by opponent balls; no pot or safety line generates.
        var table = new TableState
        {
            Balls = new[]
            {
                MakeBall(0, BallColor.Cue, new Vec2(0, 0)),
                MakeBall(8, BallColor.Yellow, new Vec2(0.06, 0)),
                MakeBall(9, BallColor.Yellow, new Vec2(-0.06, 0)),
                MakeBall(10, BallColor.Yellow, new Vec2(0, 0.06)),
                MakeBall(11, BallColor.Yellow, new Vec2(0, -0.06)),
                MakeBall(1, BallColor.Red, new Vec2(0.6, 0.3)),
                MakeBall(15, BallColor.Black, new Vec2(0.7, -0.3)),
            },
        };
        var game = AssignedRedsGame();
        var ai = new AiPlayer(_sim, AiDifficulty.Easy);

        Shot shot = ai.ChooseShot(table, game, seed: 1);
        Assert.True(shot.CueVelocity.Length > 0);
    }

    [Fact]
    public void Geometry_PathBlocked_DetectsObstacleOnLine()
    {
        Assert.True(Geometry.PathBlocked(
            new Vec2(0, 0), new Vec2(1, 0), 0.025, new Vec2(0.5, 0.01), 0.025));
        Assert.False(Geometry.PathBlocked(
            new Vec2(0, 0), new Vec2(1, 0), 0.025, new Vec2(0.5, 0.2), 0.025));
        // Behind the start point doesn't block.
        Assert.False(Geometry.PathBlocked(
            new Vec2(0, 0), new Vec2(1, 0), 0.025, new Vec2(-0.2, 0), 0.025));
    }

    [Fact]
    public void BreakCandidate_ScattersThePack()
    {
        var table = Rack.BlackballBreak(Cfg);
        var game = new BlackballGame(PlayerId.One);
        var ai = new AiPlayer(_sim, AiDifficulty.Medium);

        Shot shot = ai.ChooseShot(table, game, seed: 5);
        var result = _sim.Run(table, shot);

        Assert.Contains(result.Events, e => e.Type == SimEventType.BallCollision);
        var verdict = game.ApplyShot(table, result);
        Assert.NotEqual(FoulReason.NoContact, verdict.Foul);
    }

    /// <summary>Game with groups assigned: P1 = reds, P1 to play.</summary>
    private static BlackballGame AssignedRedsGame()
    {
        var game = new BlackballGame(PlayerId.One);
        var setup = new TableState
        {
            Balls = new[]
            {
                MakeBall(0, BallColor.Cue, new Vec2(-0.5, 0)),
                MakeBall(1, BallColor.Red, new Vec2(-0.2, 0.1)),
                MakeBall(2, BallColor.Red, new Vec2(0.2, 0.2)),
                MakeBall(8, BallColor.Yellow, new Vec2(-0.2, -0.1)),
                MakeBall(15, BallColor.Black, new Vec2(0.4, 0)),
            },
        };
        // Legal break (two balls cross), turn passes to P2.
        var breakEvents = new List<SimEvent>
        {
            new(0.1, SimEventType.BallCollision, 0, 1),
            new(0.2, SimEventType.CushionHit, 1, 0),
            new(1.0, SimEventType.Settled, -1),
        };
        var afterBreak = setup.Clone();
        afterBreak.ById(1)!.Position = new Vec2(0.25, 0.1);
        afterBreak.ById(8)!.Position = new Vec2(0.25, -0.1);
        game.ApplyShot(setup, new ShotResult(breakEvents, afterBreak));

        // P2 pots a yellow (assigns yellows to P2, reds to P1) then misses.
        var potEvents = new List<SimEvent>
        {
            new(0.1, SimEventType.BallCollision, 0, 8),
            new(0.2, SimEventType.BallPotted, 8, 0),
            new(1.0, SimEventType.Settled, -1),
        };
        var afterPot = afterBreak.Clone();
        afterPot.ById(8)!.Potted = true;
        game.ApplyShot(afterBreak, new ShotResult(potEvents, afterPot));

        // P2's only yellow is down, so their ball on is now the black: a legal
        // safety off the black hands the table to P1 with no free shot.
        var missEvents = new List<SimEvent>
        {
            new(0.1, SimEventType.BallCollision, 0, 15),
            new(0.2, SimEventType.CushionHit, 15, 0),
            new(1.0, SimEventType.Settled, -1),
        };
        game.ApplyShot(afterPot, new ShotResult(missEvents, afterPot.Clone()));
        Assert.Equal(PlayerId.One, game.CurrentPlayer);
        Assert.False(game.FreeShot);
        Assert.Equal(BallColor.Red, game.GroupOf(PlayerId.One));
        return game;
    }
}
