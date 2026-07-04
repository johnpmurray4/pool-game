namespace PoolGame.Core;

/// <summary>
/// Event-based shot simulation: within a time window where every ball's
/// acceleration is constant, ball positions are quadratic in t, so the next
/// collision/cushion/pocket event time is found by root-isolation on
/// polynomials rather than fixed timesteps. Deterministic: identical inputs
/// produce identical event streams on the same platform.
/// </summary>
public sealed class Simulation
{
    private readonly PhysicsConfig _cfg;
    private readonly IReadOnlyList<Pocket> _pockets;

    private const double MaxShotSeconds = 120;

    public Simulation(PhysicsConfig? config = null)
    {
        _cfg = config ?? new PhysicsConfig();
        _pockets = _cfg.Pockets;
    }

    public PhysicsConfig Config => _cfg;

    public ShotResult Run(TableState initial, Shot shot)
    {
        TableState state = initial.Clone();
        var events = new List<SimEvent>();

        Ball cue = state.CueBall;
        cue.Velocity = shot.CueVelocity;
        cue.AngularVelocity = shot.CueAngularVelocity;
        BallMotion.Reclassify(cue, _cfg);

        double t = 0;
        while (t < MaxShotSeconds)
        {
            List<Ball> moving = state.Balls.Where(b => b.Moving).ToList();
            if (moving.Count == 0) break;

            // Window where all accelerations are constant.
            double window = moving.Min(b => BallMotion.CurrentSegment(b, _cfg).Duration);
            window = Math.Max(window, 1e-9);

            (double dt, SimEvent? ev) = FindNextEvent(state, window, t);

            foreach (Ball b in state.Balls.Where(b => b.InPlay))
                BallMotion.Advance(b, dt, _cfg);
            t += dt;

            if (ev is { } e)
            {
                Resolve(state, e);
                events.Add(e);
            }
            // else: a regime transition; Advance() already applied it.
        }

        events.Add(new SimEvent(t, SimEventType.Settled, -1));
        return new ShotResult(events, state);
    }

    private (double dt, SimEvent? ev) FindNextEvent(TableState state, double window, double now)
    {
        double best = window;
        SimEvent? bestEvent = null;

        List<Ball> balls = state.Balls.Where(b => b.InPlay).ToList();
        var segs = balls.ToDictionary(b => b.Id, b => BallMotion.CurrentSegment(b, _cfg));

        foreach (Ball b in balls.Where(b => b.Moving))
        {
            // Pockets first: a ball reaching the cushion line inside a pocket
            // mouth is captured, not reflected.
            foreach (var (pocket, idx) in _pockets.Select((p, i) => (p, i)))
            {
                double? hit = QuadraticPathCircleHit(b, segs[b.Id].Acceleration, pocket.Center, pocket.CaptureRadius, best);
                if (hit is { } tp && (bestEvent is null || tp < best))
                {
                    best = tp;
                    bestEvent = new SimEvent(now + tp, SimEventType.BallPotted, b.Id, idx)
                        { ImpactSpeed = b.Velocity.Length };
                }
            }

            foreach (var (wallPos, isX, dir) in Walls(b))
            {
                // Pocket capture extends further into the table than the cushion
                // line, so a true pot is captured before its cushion event fires;
                // anything else must reflect — a skip here lets balls escape.
                double? hit = WallHitTime(b, segs[b.Id].Acceleration, wallPos, isX, dir, best);
                if (hit is { } tw && tw < best)
                {
                    best = tw;
                    bestEvent = new SimEvent(now + tw, SimEventType.CushionHit, b.Id, isX ? 0 : 1)
                        { ImpactSpeed = Math.Abs(isX ? VelocityAt(b, segs[b.Id].Acceleration, tw).X : VelocityAt(b, segs[b.Id].Acceleration, tw).Y) };
                }
            }
        }

        for (int i = 0; i < balls.Count; i++)
        for (int j = i + 1; j < balls.Count; j++)
        {
            Ball a = balls[i], b = balls[j];
            if (!a.Moving && !b.Moving) continue;
            double? hit = BallBallHitTime(a, segs[a.Id].Acceleration, b, segs[b.Id].Acceleration, best);
            if (hit is { } tc && tc < best)
            {
                // Report cue ball first so FirstContact reads naturally.
                (Ball first, Ball second) = a.IsCue ? (a, b) : b.IsCue ? (b, a) : (a, b);
                Vec2 relV = VelocityAt(first, segs[first.Id].Acceleration, tc) - VelocityAt(second, segs[second.Id].Acceleration, tc);
                best = tc;
                bestEvent = new SimEvent(now + tc, SimEventType.BallCollision, first.Id, second.Id)
                    { ImpactSpeed = relV.Length };
            }
        }

        return (best, bestEvent);
    }

    private void Resolve(TableState state, SimEvent e)
    {
        switch (e.Type)
        {
            case SimEventType.BallCollision:
            {
                Ball a = state.ById(e.BallId)!;
                Ball b = state.ById(e.OtherBallId)!;
                ResolveBallCollision(a, b);
                break;
            }
            case SimEventType.CushionHit:
            {
                Ball b = state.ById(e.BallId)!;
                ResolveCushion(b, isXWall: e.OtherBallId == 0);
                break;
            }
            case SimEventType.BallPotted:
            {
                Ball b = state.ById(e.BallId)!;
                b.Potted = true;
                b.Velocity = Vec2.Zero;
                b.AngularVelocity = Vec3.Zero;
                b.State = MotionState.Stationary;
                break;
            }
        }
    }

    private void ResolveBallCollision(Ball a, Ball b)
    {
        Vec2 n = (b.Position - a.Position).Normalized();
        double relN = (a.Velocity - b.Velocity).Dot(n);
        if (relN <= 0) return; // separating; numerical edge case

        // Frictionless impulse along the line of centres. Spin-induced throw
        // is a later refinement; it perturbs n by ~1-3° in real play.
        double e = _cfg.BallRestitution;
        double j = (1 + e) * relN / (1 / a.Mass + 1 / b.Mass);
        a.Velocity -= (j / a.Mass) * n;
        b.Velocity += (j / b.Mass) * n;

        BallMotion.Reclassify(a, _cfg);
        BallMotion.Reclassify(b, _cfg);
    }

    private void ResolveCushion(Ball b, bool isXWall)
    {
        double e = _cfg.CushionRestitution;
        if (isXWall)
            b.Velocity = new Vec2(-e * b.Velocity.X, b.Velocity.Y);
        else
            b.Velocity = new Vec2(b.Velocity.X, -e * b.Velocity.Y);

        // Side spin nudges the rebound: a crude linear model pending the full
        // cushion-impact treatment (Han 2005 §cushion).
        double spinEffect = 0.12 * b.AngularVelocity.Z * b.Radius;
        if (isXWall)
            b.Velocity = new Vec2(b.Velocity.X, b.Velocity.Y + Math.Sign(b.Velocity.X) * spinEffect);
        else
            b.Velocity = new Vec2(b.Velocity.X - Math.Sign(b.Velocity.Y) * spinEffect, b.Velocity.Y);
        b.AngularVelocity = b.AngularVelocity with { Z = b.AngularVelocity.Z * 0.7 };

        BallMotion.Reclassify(b, _cfg);
    }

    // --- geometry helpers ---

    private IEnumerable<(double wallPos, bool isX, int dir)> Walls(Ball b)
    {
        double hx = _cfg.TableLength / 2 - b.Radius;
        double hy = _cfg.TableWidth / 2 - b.Radius;
        yield return (-hx, true, -1);
        yield return (hx, true, 1);
        yield return (-hy, false, -1);
        yield return (hy, false, 1);
    }

    private static Vec2 VelocityAt(Ball b, Vec2 acc, double t) => b.Velocity + acc * t;

    private static double? WallHitTime(Ball b, Vec2 acc, double wall, bool isX, int dir, double tMax)
    {
        double p0 = (isX ? b.Position.X : b.Position.Y) - wall;
        double v0 = isX ? b.Velocity.X : b.Velocity.Y;
        double a0 = isX ? acc.X : acc.Y;
        // Solve p0 + v0 t + a0/2 t² = 0 for smallest t in (0, tMax], moving toward the wall.
        foreach (double t in QuadraticRoots(a0 / 2, v0, p0))
        {
            if (t <= 1e-12 || t > tMax) continue;
            double vAt = v0 + a0 * t;
            if (Math.Sign(vAt) == dir || vAt == 0) return t;
        }
        return null;
    }

    private static IEnumerable<double> QuadraticRoots(double a, double b, double c)
    {
        if (Math.Abs(a) < 1e-15)
        {
            if (Math.Abs(b) > 1e-15) yield return -c / b;
            yield break;
        }
        double disc = b * b - 4 * a * c;
        if (disc < 0) yield break;
        double sq = Math.Sqrt(disc);
        double r1 = (-b - sq) / (2 * a);
        double r2 = (-b + sq) / (2 * a);
        yield return Math.Min(r1, r2);
        yield return Math.Max(r1, r2);
    }

    /// <summary>Smallest t in (0, tMax] where |path(t) − centre| = radius,
    /// entering the circle. Distance² along a quadratic path is a quartic in t.</summary>
    private static double? QuadraticPathCircleHit(Ball b, Vec2 acc, Vec2 center, double radius, double tMax)
    {
        Vec2 dp = b.Position - center;
        return SmallestEntryTime(dp, b.Velocity, acc, radius, tMax);
    }

    private static double? BallBallHitTime(Ball a, Vec2 accA, Ball b, Vec2 accB, double tMax)
    {
        Vec2 dp = a.Position - b.Position;
        Vec2 dv = a.Velocity - b.Velocity;
        Vec2 da = accA - accB;
        double rsum = a.Radius + b.Radius;

        // Cheap reject: even at full relative speed the pair can't close the gap.
        double gap = dp.Length - rsum;
        if (gap > dv.Length * tMax + 0.5 * da.Length * tMax * tMax)
            return null;

        return SmallestEntryTime(dp, dv, da, rsum, tMax);
    }

    /// <summary>Smallest t in (0, tMax] where |dp + dv·t + ½·da·t²| crosses
    /// below r from outside. Skips any initial overlap (numerical residue from
    /// a just-resolved contact) — only fresh entries count.</summary>
    private static double? SmallestEntryTime(Vec2 dp, Vec2 dv, Vec2 da, double r, double tMax)
    {
        var c = new[]
        {
            dp.LengthSquared - r * r,
            2 * dp.Dot(dv),
            dv.LengthSquared + dp.Dot(da),
            dv.Dot(da),
            0.25 * da.LengthSquared,
        };

        List<double> roots = Poly.RootsIn(c, 0, tMax);
        foreach (double t in roots)
        {
            if (t <= 1e-12) continue;
            // Entering means f decreasing at the crossing.
            if (Poly.Eval(Poly.Derivative(c), t) < 0 && Poly.Eval(c, Math.Max(0, t - 1e-9)) > 0)
                return t;
        }
        return null;
    }
}
