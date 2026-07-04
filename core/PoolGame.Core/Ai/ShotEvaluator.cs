using PoolGame.Core.Rules;

namespace PoolGame.Core.Ai;

/// <summary>
/// Scores a candidate by actually playing it: run the real physics headlessly,
/// judge the outcome with a clone of the real rules state, then add positional
/// terms. This is the payoff of the deterministic engine-agnostic core — the
/// AI evaluates with the exact code the game runs.
/// </summary>
public sealed class ShotEvaluator
{
    private readonly Simulation _sim;
    private readonly PhysicsConfig _cfg;

    public ShotEvaluator(Simulation sim)
    {
        _sim = sim;
        _cfg = sim.Config;
    }

    public double Score(TableState table, BlackballGame game, ShotCandidate candidate)
    {
        ShotResult result = _sim.Run(table, candidate.Shot);
        BlackballGame whatIf = game.Clone();
        PlayerId me = whatIf.CurrentPlayer;
        ShotVerdict verdict = whatIf.ApplyShot(table, result);

        bool iWin = verdict.Rack == (me == PlayerId.One ? RackState.PlayerOneWins : RackState.PlayerTwoWins);
        bool iLose = verdict.Rack == (me == PlayerId.One ? RackState.PlayerTwoWins : RackState.PlayerOneWins);

        if (iWin) return 1e6;
        if (iLose) return -1e6;

        double score = 0;
        if (verdict.IsFoul) score -= 500;

        // Balls of mine that went down (open table: anything I potted counts).
        BallColor? myGroup = game.TableOpen ? null : game.GroupOf(me);
        int myPots = table.Balls.Count(b => b.InPlay
            && result.FinalState.ById(b.Id)!.Potted
            && b.Color != BallColor.Cue && b.Color != BallColor.Black
            && (myGroup is null || b.Color == myGroup));
        score += 120 * myPots;

        if (verdict.ShooterContinues)
            score += 40 + 60 * NextShotQuality(result.FinalState, whatIf);
        else
            // Position left for the opponent: their easy table is my problem.
            score -= 80 * NextShotQuality(result.FinalState, whatIf);

        return score;
    }

    /// <summary>Cheap position metric for whoever plays next: best available
    /// pot line quality in [0,1] — 0 when nothing is on.</summary>
    private double NextShotQuality(TableState table, BlackballGame game)
    {
        if (game.Rack != RackState.InProgress) return 0;
        Ball cue = table.CueBall;
        if (cue.Potted) return 0.5; // in hand: decent but placement-dependent

        double best = 0;
        IReadOnlyList<BallColor> on = game.BallOnColours(table);
        foreach (Ball target in table.Balls.Where(b => b.InPlay && !b.IsCue && on.Contains(b.Color)))
        foreach (Pocket pocket in _cfg.Pockets)
        {
            Vec2 ghost = Geometry.GhostBall(target.Position, pocket.Center, target.Radius, cue.Radius);
            double cut = Geometry.CutAngle(cue.Position, ghost, target.Position, pocket.Center);
            if (cut > Math.PI / 2) continue;

            double angleQuality = 1 - cut / (Math.PI / 2);
            double distance = (cue.Position - target.Position).Length + (target.Position - pocket.Center).Length;
            double distanceQuality = Math.Clamp(1 - distance / (_cfg.TableLength * 1.5), 0, 1);
            best = Math.Max(best, angleQuality * (0.5 + 0.5 * distanceQuality));
        }
        return best;
    }
}
