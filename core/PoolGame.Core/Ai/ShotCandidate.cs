namespace PoolGame.Core.Ai;

public enum CandidateKind { Pot, Safety, Break }

/// <summary>A shot the AI is considering, with the intent that produced it
/// (kept for debugging and difficulty-based filtering).</summary>
public sealed record ShotCandidate
{
    public required Shot Shot { get; init; }
    public required CandidateKind Kind { get; init; }
    public int TargetBallId { get; init; } = -1;
    public int PocketIndex { get; init; } = -1;
    public double CutAngle { get; init; }
}
