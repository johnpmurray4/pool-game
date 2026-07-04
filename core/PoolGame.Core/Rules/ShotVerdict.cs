namespace PoolGame.Core.Rules;

public enum PlayerId { One = 0, Two = 1 }

public enum FoulReason
{
    None,
    /// <summary>Cue ball potted (6.1).</summary>
    CueBallPotted,
    /// <summary>First contact was not a ball on (6.2). Suspended on a free shot.</summary>
    WrongBallFirst,
    /// <summary>Cue ball struck nothing.</summary>
    NoContact,
    /// <summary>No ball on potted and no ball reached a cushion after first contact (6.3).</summary>
    NoRailAfterContact,
    /// <summary>Opponent's ball potted without also potting one's own (5.11).</summary>
    OpponentBallPotted,
    /// <summary>Break with no pot and fewer than two object balls crossing the centre string (5.3).</summary>
    IllegalBreak,
}

public enum RackState
{
    InProgress,
    PlayerOneWins,
    PlayerTwoWins,
    /// <summary>Black potted on the break: re-rack, same player breaks again (5.3).</summary>
    ReRack,
}

/// <summary>Everything the game layer needs to know after a shot is judged.</summary>
public sealed record ShotVerdict
{
    public required PlayerId NextPlayer { get; init; }
    public FoulReason Foul { get; init; } = FoulReason.None;
    public bool IsFoul => Foul != FoulReason.None;
    /// <summary>The incoming player's first shot is a free shot: wrong-ball-first
    /// is suspended and the cue ball may be taken in hand in baulk (5.10).</summary>
    public bool FreeShotNext { get; init; }
    /// <summary>Cue ball must be returned to the table (it was potted).</summary>
    public bool CueBallInHand { get; init; }
    public RackState Rack { get; init; } = RackState.InProgress;
    /// <summary>Set when this shot decided the groups (open table → assigned).</summary>
    public BallColor? GroupAssignedToShooter { get; init; }
    /// <summary>True when the shooter stays at the table for another visit shot.</summary>
    public bool ShooterContinues { get; init; }
}
