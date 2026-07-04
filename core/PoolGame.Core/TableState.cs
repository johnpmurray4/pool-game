namespace PoolGame.Core;

/// <summary>Snapshot of every ball on the table. Immutable from the caller's
/// perspective: Simulation clones it before running.</summary>
public sealed class TableState
{
    public required IReadOnlyList<Ball> Balls { get; init; }

    public Ball CueBall => Balls.First(b => b.IsCue);
    public Ball? ById(int id) => Balls.FirstOrDefault(b => b.Id == id);

    public TableState Clone() => new() { Balls = Balls.Select(b => b.Clone()).ToList() };
}

/// <summary>Shot input: the cue ball's state immediately after cue impact.
/// A cue-strike model (tip offset + elevation + speed → velocity and spin)
/// layers on top of this later; expressing shots directly as post-impact
/// velocities keeps the physics core independent of it.</summary>
public readonly record struct Shot(Vec2 CueVelocity, Vec3 CueAngularVelocity);

public sealed record ShotResult(IReadOnlyList<SimEvent> Events, TableState FinalState)
{
    /// <summary>The first ball the cue ball contacted, or null — the rules
    /// engine's key input for foul detection.</summary>
    public int? FirstContactBallId => Events
        .Where(e => e.Type == SimEventType.BallCollision)
        .Select(e => (int?)e.OtherBallId)
        .FirstOrDefault();
}
