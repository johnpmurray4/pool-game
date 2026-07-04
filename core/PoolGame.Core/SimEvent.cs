namespace PoolGame.Core;

public enum SimEventType
{
    /// <summary>Two balls collided. BallId = first ball (cue ball if involved), OtherBallId = second.</summary>
    BallCollision,
    /// <summary>Ball bounced off a cushion.</summary>
    CushionHit,
    /// <summary>Ball fell into a pocket. OtherBallId = pocket index.</summary>
    BallPotted,
    /// <summary>All balls have come to rest; the shot is over.</summary>
    Settled,
}

/// <summary>One entry in the shot's event stream — the contract between the
/// physics and everything downstream (rules engine, AI scoring, replays, audio).</summary>
public readonly record struct SimEvent(double Time, SimEventType Type, int BallId, int OtherBallId = -1)
{
    /// <summary>Relative impact speed, used for audio volume and AI risk scoring.</summary>
    public double ImpactSpeed { get; init; }
}
