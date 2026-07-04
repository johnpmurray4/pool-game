namespace PoolGame.Core;

/// <summary>One constant-acceleration stretch of a ball's path. A full shot is
/// piecewise-quadratic, so these segments reproduce it exactly.</summary>
public readonly record struct PathSegment(double T0, double T1, Vec2 P0, Vec2 V0, Vec2 Acc)
{
    public Vec2 PositionAt(double t)
    {
        double dt = Math.Clamp(t, T0, T1) - T0;
        return P0 + V0 * dt + 0.5 * dt * dt * Acc;
    }
}

/// <summary>
/// Exact motion record of a simulated shot: per-ball piecewise-quadratic paths
/// plus the event stream. Drives the debug visualizer, physics tuning
/// comparisons, and — in the game — shot animation and replays (render the
/// trace; never re-simulate in real time).
/// </summary>
public sealed class ShotTrace
{
    private readonly Dictionary<int, List<PathSegment>> _paths = new();

    public IReadOnlyDictionary<int, List<PathSegment>> Paths => _paths;
    public double Duration { get; private set; }

    internal void Record(int ballId, double t0, double t1, Vec2 p0, Vec2 v0, Vec2 acc)
    {
        if (t1 <= t0) return;
        if (!_paths.TryGetValue(ballId, out var list))
            _paths[ballId] = list = new List<PathSegment>();
        list.Add(new PathSegment(t0, t1, p0, v0, acc));
        Duration = Math.Max(Duration, t1);
    }

    /// <summary>Ball centre at absolute shot time t (its rest position after
    /// its last segment, its start position before its first).</summary>
    public Vec2? PositionAt(int ballId, double t)
    {
        if (!_paths.TryGetValue(ballId, out var segs) || segs.Count == 0) return null;
        if (t <= segs[0].T0) return segs[0].P0;
        foreach (PathSegment s in segs)
            if (t <= s.T1) return s.PositionAt(t);
        return segs[^1].PositionAt(segs[^1].T1);
    }
}
