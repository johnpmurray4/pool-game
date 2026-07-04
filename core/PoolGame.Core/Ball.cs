namespace PoolGame.Core;

public enum BallColor { Cue, Red, Yellow, Black }

public enum MotionState { Stationary, Sliding, Rolling }

/// <summary>Mutable ball state within a simulation run. The simulation clones
/// the input state, so callers keep an unmodified copy of the pre-shot table.</summary>
public sealed class Ball
{
    public required int Id { get; init; }
    public required BallColor Color { get; init; }
    public required double Radius { get; init; }
    public required double Mass { get; init; }

    public Vec2 Position { get; set; }
    public Vec2 Velocity { get; set; }
    public Vec3 AngularVelocity { get; set; }
    public MotionState State { get; set; } = MotionState.Stationary;
    public bool Potted { get; set; }

    public bool IsCue => Color == BallColor.Cue;
    public bool InPlay => !Potted;
    public bool Moving => !Potted && State != MotionState.Stationary;

    /// <summary>Velocity of the ball's contact point relative to the cloth.
    /// Zero means natural roll; non-zero means the ball is sliding.
    /// u = v − R(ω × ẑ) with the contact point at −Rẑ.</summary>
    public Vec2 SurfaceVelocity =>
        new(Velocity.X - Radius * AngularVelocity.Y,
            Velocity.Y + Radius * AngularVelocity.X);

    public Ball Clone() => new()
    {
        Id = Id, Color = Color, Radius = Radius, Mass = Mass,
        Position = Position, Velocity = Velocity, AngularVelocity = AngularVelocity,
        State = State, Potted = Potted,
    };
}
