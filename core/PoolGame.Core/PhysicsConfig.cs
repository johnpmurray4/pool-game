namespace PoolGame.Core;

/// <summary>
/// All tunable physics and table parameters. Lives as data (not code) so feel
/// tuning never requires a rebuild — the game loads this from JSON and the
/// defaults below are the starting point for a 7ft English pool table.
/// Units: metres, kilograms, seconds, radians.
/// </summary>
public sealed record PhysicsConfig
{
    // Table playing surface (cushion nose to cushion nose), 7ft UK table.
    public double TableLength { get; init; } = 1.727;
    public double TableWidth { get; init; } = 0.8635;

    // English pool: 2" object balls, 1 7/8" cue ball.
    public double ObjectBallRadius { get; init; } = 0.0254;
    public double CueBallRadius { get; init; } = 0.0238;
    public double ObjectBallMass { get; init; } = 0.142;
    public double CueBallMass { get; init; } = 0.118;

    public double Gravity { get; init; } = 9.81;

    // Cloth: napped UK cloth rolls off harder than American worsted.
    public double SlidingFriction { get; init; } = 0.20;
    public double RollingFriction { get; init; } = 0.012;
    public double SpinFriction { get; init; } = 0.044; // z-spin (english) decay

    public double BallRestitution { get; init; } = 0.96;
    public double CushionRestitution { get; init; } = 0.75;

    // Pocket capture radius from pocket centre to ball centre.
    public double CornerPocketRadius { get; init; } = 0.058;
    public double SidePocketRadius { get; init; } = 0.053;

    /// <summary>Balls slower than this are considered stopped.</summary>
    public double RestSpeed { get; init; } = 1e-4;

    /// <summary>Table coordinates: origin at table centre, X along the long axis.
    /// Pockets sit on the cushion line: four corners plus two side (middle) pockets.</summary>
    public IReadOnlyList<Pocket> Pockets => new[]
    {
        new Pocket(new Vec2(-TableLength / 2, -TableWidth / 2), CornerPocketRadius),
        new Pocket(new Vec2(-TableLength / 2,  TableWidth / 2), CornerPocketRadius),
        new Pocket(new Vec2( TableLength / 2, -TableWidth / 2), CornerPocketRadius),
        new Pocket(new Vec2( TableLength / 2,  TableWidth / 2), CornerPocketRadius),
        new Pocket(new Vec2(0, -TableWidth / 2), SidePocketRadius),
        new Pocket(new Vec2(0,  TableWidth / 2), SidePocketRadius),
    };
}

public readonly record struct Pocket(Vec2 Center, double CaptureRadius);
