namespace PoolGame.Core.Rules;

/// <summary>
/// WPA Blackball rules state machine (WPA Rules of Play §5 "Black Ball",
/// 2025-09-15 revision — rule numbers in comments refer to that document).
/// Consumes physics facts, produces verdicts; it never touches the physics.
///
/// Not modelled in v1 (need referee judgement or unimplemented physics):
/// intentional fouls (5.14c/d), balls driven off the table (6.5), touched
/// balls (6.6), push shots (6.8), time fouls, stalemate re-racks.
/// </summary>
public sealed class BlackballGame
{
    public PlayerId CurrentPlayer { get; private set; }
    public bool TableOpen { get; private set; } = true;
    public bool IsBreakShot { get; private set; } = true;
    /// <summary>The current shot is a free shot (first shot after a foul, 5.10).</summary>
    public bool FreeShot { get; private set; }
    public RackState Rack { get; private set; } = RackState.InProgress;
    public BallColor? PlayerOneGroup { get; private set; }

    public BallColor? GroupOf(PlayerId p) => PlayerOneGroup is null ? null
        : p == PlayerId.One ? PlayerOneGroup
        : PlayerOneGroup == BallColor.Red ? BallColor.Yellow : BallColor.Red;

    public BlackballGame(PlayerId breaker = PlayerId.One) => CurrentPlayer = breaker;

    /// <summary>Judge the shot the current player just played. `before` is the
    /// table as it stood when they addressed the cue ball.</summary>
    public ShotVerdict ApplyShot(TableState before, ShotResult result)
    {
        if (Rack != RackState.InProgress)
            throw new InvalidOperationException("Rack is over; start a new one.");

        ShotFacts facts = ShotFacts.From(before, result);
        PlayerId shooter = CurrentPlayer;
        ShotVerdict verdict = IsBreakShot ? JudgeBreak(facts) : JudgeNormalShot(before, facts);

        CurrentPlayer = verdict.NextPlayer;
        FreeShot = verdict.FreeShotNext;
        Rack = verdict.Rack;
        if (verdict.GroupAssignedToShooter is { } g)
        {
            TableOpen = false;
            PlayerOneGroup = shooter == PlayerId.One ? g
                : g == BallColor.Red ? BallColor.Yellow : BallColor.Red;
        }
        if (verdict.Rack == RackState.ReRack)
            ResetForReRack();
        return verdict;
    }

    private void ResetForReRack()
    {
        Rack = RackState.InProgress;
        TableOpen = true;
        IsBreakShot = true;
        FreeShot = false;
        PlayerOneGroup = null;
    }

    private ShotVerdict JudgeBreak(ShotFacts facts)
    {
        // 5.3: black potted on the break → re-rack, same breaker; cue-scratch
        // and off-table violations on that break are ignored.
        if (facts.BlackPotted)
            return new ShotVerdict { NextPlayer = CurrentPlayer, Rack = RackState.ReRack };

        IsBreakShot = false;

        bool objectBallPotted = facts.PottedInOrder.Any(c => c is BallColor.Red or BallColor.Yellow);
        // 5.3: legal break = a ball potted OR ≥2 object balls cross the centre string.
        bool fairBreak = objectBallPotted || facts.BallsCrossedCentreString >= 2;

        FoulReason foul =
            facts.CuePotted ? FoulReason.CueBallPotted :
            !fairBreak ? FoulReason.IllegalBreak :
            FoulReason.None;

        if (foul != FoulReason.None)
            return FoulVerdict(foul, cuePotted: facts.CuePotted);

        // Pots on the break never assign groups (5.4); breaker continues if any went down.
        return objectBallPotted
            ? new ShotVerdict { NextPlayer = CurrentPlayer, ShooterContinues = true }
            : new ShotVerdict { NextPlayer = Opponent };
    }

    private ShotVerdict JudgeNormalShot(TableState before, ShotFacts facts)
    {
        BallColor? myGroup = TableOpen ? null : GroupOf(CurrentPlayer);
        bool groupClearedBeforeShot = !TableOpen &&
            !before.Balls.Any(b => b.InPlay && b.Color == myGroup);

        FoulReason foul = JudgeFouls(facts, myGroup, groupClearedBeforeShot);

        // Black-ball outcomes (5.6, 5.14) trump everything else.
        if (facts.BlackPotted)
        {
            // Win only when the group was already cleared and the shot was clean.
            // 5.14a: black on an illegal shot, 5.14b: black leaving own balls on
            // the table — both lose the rack. Potting the black together with
            // one's final colour is judged a loss here (5.14b reads "leaves any
            // of his group on the table", which is ambiguous for that edge;
            // traditional interpretation applied — verify against rulebook).
            bool win = groupClearedBeforeShot && foul == FoulReason.None;
            PlayerId winner = win ? CurrentPlayer : Opponent;
            return new ShotVerdict
            {
                NextPlayer = Opponent,
                Foul = foul,
                Rack = winner == PlayerId.One ? RackState.PlayerOneWins : RackState.PlayerTwoWins,
            };
        }

        if (foul != FoulReason.None)
            return FoulVerdict(foul, facts.CuePotted);

        // Group assignment: only on a legal normal shot — not break, not free
        // shot (5.4) — when exactly one colour was potted.
        BallColor? assigned = null;
        if (TableOpen && !FreeShot)
        {
            var colours = facts.PottedInOrder.Where(c => c is BallColor.Red or BallColor.Yellow).Distinct().ToList();
            if (colours.Count == 1) assigned = colours[0];
        }

        bool pottedCounting = TableOpen || FreeShot
            ? facts.PottedInOrder.Any(c => c is BallColor.Red or BallColor.Yellow)
            : facts.PottedCount(myGroup!.Value) > 0;

        return pottedCounting
            ? new ShotVerdict { NextPlayer = CurrentPlayer, ShooterContinues = true, GroupAssignedToShooter = assigned }
            : new ShotVerdict { NextPlayer = Opponent };
    }

    private FoulReason JudgeFouls(ShotFacts facts, BallColor? myGroup, bool groupCleared)
    {
        if (facts.FirstContact is null)
            return FoulReason.NoContact;

        // 6.2 Wrong Ball First — suspended on a free shot (5.10).
        if (!FreeShot)
        {
            bool contactOk = TableOpen
                ? facts.FirstContact is BallColor.Red or BallColor.Yellow
                : groupCleared
                    ? facts.FirstContact == BallColor.Black
                    : facts.FirstContact == myGroup;
            if (!contactOk)
                return FoulReason.WrongBallFirst;
        }

        if (facts.CuePotted)
            return FoulReason.CueBallPotted;

        // 5.11: opponent's ball potted without one's own is a foul. On a free
        // shot or an open table any colour counts as one's own.
        if (!TableOpen && !FreeShot && myGroup is { } g)
        {
            BallColor opp = g == BallColor.Red ? BallColor.Yellow : BallColor.Red;
            if (facts.PottedCount(opp) > 0 && facts.PottedCount(g) == 0)
                return FoulReason.OpponentBallPotted;
        }

        // 6.3 No Rail after Contact: a legal shot needs a counting pot or any
        // ball reaching a cushion after first contact.
        bool anyPot = facts.PottedInOrder.Count > 0;
        if (!anyPot && !facts.RailAfterContact)
            return FoulReason.NoRailAfterContact;

        return FoulReason.None;
    }

    private ShotVerdict FoulVerdict(FoulReason reason, bool cuePotted) => new()
    {
        NextPlayer = Opponent,
        Foul = reason,
        FreeShotNext = true,
        // 5.10: after any foul the incoming player may use the cue ball where
        // it lies or in hand in baulk; if it was potted it must be placed.
        CueBallInHand = true,
        Rack = RackState.InProgress,
    };

    private PlayerId Opponent => CurrentPlayer == PlayerId.One ? PlayerId.Two : PlayerId.One;
}
