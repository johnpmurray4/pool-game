using PoolGame.Core.Rules;

namespace PoolGame.Core.Ai;

/// <summary>
/// Enumerates plausible shots for the current position: for every legal
/// target ball × pocket, a ghost-ball pot line (with power variants), plus
/// low-speed safety contacts. Feasibility filters (cut angle, blocked paths)
/// keep the candidate count small enough to simulate every one.
/// </summary>
public sealed class CandidateGenerator
{
    private const double MaxCutAngle = 80 * Math.PI / 180;
    private static readonly double[] PotSpeeds = { 1.2, 2.2, 3.5 };
    private static readonly double[] SafetySpeeds = { 0.8, 1.4 };

    private readonly PhysicsConfig _cfg;

    public CandidateGenerator(PhysicsConfig cfg) => _cfg = cfg;

    public IReadOnlyList<ShotCandidate> Generate(TableState table, BlackballGame game)
    {
        if (game.IsBreakShot)
            return new[] { BreakShot(table) };

        var candidates = new List<ShotCandidate>();
        Ball cue = table.CueBall;
        IReadOnlyList<BallColor> onColours = game.BallOnColours(table);
        List<Ball> targets = table.Balls
            .Where(b => b.InPlay && !b.IsCue && onColours.Contains(b.Color))
            .ToList();

        foreach (Ball target in targets)
        {
            foreach (var (pocket, pIdx) in _cfg.Pockets.Select((p, i) => (p, i)))
            {
                Vec2 ghost = Geometry.GhostBall(target.Position, pocket.Center, target.Radius, cue.Radius);
                double cut = Geometry.CutAngle(cue.Position, ghost, target.Position, pocket.Center);
                if (cut > MaxCutAngle) continue;
                if (Blocked(table, cue, target, ghost, pocket.Center)) continue;

                Vec2 aim = ghost - cue.Position;
                if (aim.Length < 1e-6) continue;

                foreach (double speed in PotSpeeds)
                {
                    // Thin cuts transfer little energy; scale power up with cut angle.
                    double v = speed * (1 + 0.8 * cut / MaxCutAngle);
                    candidates.Add(new ShotCandidate
                    {
                        Shot = CueStrike.Straight(v, aim, cue.Radius),
                        Kind = CandidateKind.Pot,
                        TargetBallId = target.Id,
                        PocketIndex = pIdx,
                        CutAngle = cut,
                    });
                }
            }

            // Safety: soft full-ball contact, leaving distance rather than potting.
            Vec2 safetyAim = target.Position - cue.Position;
            if (safetyAim.Length > 1e-6 && !CuePathBlocked(table, cue, target.Position, target.Id))
            {
                foreach (double speed in SafetySpeeds)
                {
                    candidates.Add(new ShotCandidate
                    {
                        Shot = CueStrike.Straight(speed, safetyAim, cue.Radius),
                        Kind = CandidateKind.Safety,
                        TargetBallId = target.Id,
                    });
                }
            }
        }

        return candidates;
    }

    private ShotCandidate BreakShot(TableState table)
    {
        // Straight, hard, at the apex ball with a touch of offset so the pack
        // opens asymmetrically.
        Ball cue = table.CueBall;
        Ball apex = table.Balls.Where(b => b.InPlay && !b.IsCue)
            .OrderBy(b => (b.Position - cue.Position).Length).First();
        Vec2 aim = apex.Position - cue.Position + new Vec2(0, 0.008);
        return new ShotCandidate
        {
            Shot = CueStrike.Straight(6.5, aim, cue.Radius),
            Kind = CandidateKind.Break,
            TargetBallId = apex.Id,
        };
    }

    private bool Blocked(TableState table, Ball cue, Ball target, Vec2 ghost, Vec2 pocket) =>
        CuePathBlocked(table, cue, ghost, target.Id) ||
        table.Balls.Any(o => o.InPlay && o.Id != target.Id && !o.IsCue &&
            Geometry.PathBlocked(target.Position, pocket, target.Radius, o.Position, o.Radius));

    private bool CuePathBlocked(TableState table, Ball cue, Vec2 destination, int targetId) =>
        table.Balls.Any(o => o.InPlay && o.Id != cue.Id && o.Id != targetId &&
            Geometry.PathBlocked(cue.Position, destination, cue.Radius, o.Position, o.Radius));
}
