using PoolGame.Core.Rules;

namespace PoolGame.Core.Ai;

public enum AiDifficulty { Easy, Medium, Hard }

/// <summary>
/// Simulation-based opponent: generate candidates, score each by running the
/// real physics, pick the best, then apply difficulty-scaled execution noise
/// (a weaker player *sees* the right shot but doesn't hit it purely).
/// Deterministic for a given seed so games can be replayed.
/// </summary>
public sealed class AiPlayer
{
    private readonly CandidateGenerator _generator;
    private readonly ShotEvaluator _evaluator;
    private readonly AiDifficulty _difficulty;

    public AiPlayer(Simulation sim, AiDifficulty difficulty)
    {
        _generator = new CandidateGenerator(sim.Config);
        _evaluator = new ShotEvaluator(sim);
        _difficulty = difficulty;
    }

    // (aim jitter stddev radians, speed jitter fraction, candidates examined)
    private (double aimNoise, double speedNoise, int maxCandidates) Profile => _difficulty switch
    {
        AiDifficulty.Easy => (0.030, 0.15, 12),
        AiDifficulty.Medium => (0.012, 0.08, 30),
        _ => (0.004, 0.03, int.MaxValue),
    };

    public Shot ChooseShot(TableState table, BlackballGame game, int seed)
    {
        var rng = new Random(seed);
        (double aimNoise, double speedNoise, int maxCandidates) = Profile;

        IReadOnlyList<ShotCandidate> candidates = _generator.Generate(table, game);
        if (candidates.Count == 0)
            return DesperationShot(table, rng);

        ShotCandidate best = candidates
            .OrderBy(_ => rng.Next()) // shuffle so the cap samples fairly
            .Take(maxCandidates)
            .Select(c => (c, score: _evaluator.Score(table, game, c)))
            .MaxBy(x => x.score).c;

        return AddExecutionNoise(best.Shot, aimNoise, speedNoise, rng);
    }

    /// <summary>Snookered with no generated candidate: hit toward the nearest
    /// legal ball and hope — mirrors what a human does from an impossible spot.</summary>
    private Shot DesperationShot(TableState table, Random rng)
    {
        Ball cue = table.CueBall;
        Ball nearest = table.Balls.Where(b => b.InPlay && !b.IsCue)
            .OrderBy(b => (b.Position - cue.Position).Length).First();
        return CueStrike.Straight(2.0, nearest.Position - cue.Position, cue.Radius);
    }

    private static Shot AddExecutionNoise(Shot shot, double aimNoise, double speedNoise, Random rng)
    {
        double angle = Gaussian(rng) * aimNoise;
        double speedScale = 1 + Gaussian(rng) * speedNoise;

        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        Vec2 v = shot.CueVelocity;
        Vec2 rotated = new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        return shot with { CueVelocity = rotated * Math.Max(0.1, speedScale) };
    }

    private static double Gaussian(Random rng)
    {
        // Box-Muller; rng is seeded, so results stay reproducible.
        double u1 = 1 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
