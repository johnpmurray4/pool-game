using PoolGame.Core;
using PoolGame.Core.Rules;

namespace PoolGame.Core.Tests;

/// <summary>Rules tests drive the state machine with scripted event streams —
/// no physics involved, per the architecture. ShotBuilder fabricates the
/// (before-state, result) pair a real shot would produce.</summary>
public class BlackballGameTests
{
    private const int CueId = 0, RedId = 1, YellowId = 8, BlackId = 15;
    private static readonly PhysicsConfig Cfg = new();

    private sealed class ShotBuilder
    {
        private readonly TableState _before;
        private readonly List<SimEvent> _events = new();
        private readonly HashSet<int> _potted = new();
        private readonly Dictionary<int, Vec2> _moved = new();
        private double _t = 0.1;

        public ShotBuilder(TableState before) => _before = before;

        public ShotBuilder Contact(int ballId, int otherId = CueId)
        {
            _events.Add(new SimEvent(_t += 0.1, SimEventType.BallCollision, otherId, ballId));
            return this;
        }

        public ShotBuilder Cushion(int ballId)
        {
            _events.Add(new SimEvent(_t += 0.1, SimEventType.CushionHit, ballId, 0));
            return this;
        }

        public ShotBuilder Pot(int ballId)
        {
            _events.Add(new SimEvent(_t += 0.1, SimEventType.BallPotted, ballId, 0));
            _potted.Add(ballId);
            return this;
        }

        public ShotBuilder MoveTo(int ballId, Vec2 pos)
        {
            _moved[ballId] = pos;
            return this;
        }

        public (TableState, ShotResult) Build()
        {
            _events.Add(new SimEvent(_t + 1, SimEventType.Settled, -1));
            TableState after = _before.Clone();
            foreach (int id in _potted) after.ById(id)!.Potted = true;
            foreach (var (id, pos) in _moved) after.ById(id)!.Position = pos;
            return (_before, new ShotResult(_events, after));
        }
    }

    /// <summary>Standard mid-game table: some of each colour plus the black.
    /// Object balls sit at negative X (breaker's half) unless moved.</summary>
    private static TableState MidGameTable(int reds = 3, int yellows = 3)
    {
        var balls = new List<Ball>
        {
            new() { Id = CueId, Color = BallColor.Cue, Radius = Cfg.CueBallRadius, Mass = Cfg.CueBallMass, Position = new Vec2(-0.5, 0) },
            new() { Id = BlackId, Color = BallColor.Black, Radius = Cfg.ObjectBallRadius, Mass = Cfg.ObjectBallMass, Position = new Vec2(0.4, 0) },
        };
        for (int i = 0; i < reds; i++)
            balls.Add(new Ball { Id = RedId + i, Color = BallColor.Red, Radius = Cfg.ObjectBallRadius, Mass = Cfg.ObjectBallMass, Position = new Vec2(-0.2, -0.2 + 0.06 * i) });
        for (int i = 0; i < yellows; i++)
            balls.Add(new Ball { Id = YellowId + i, Color = BallColor.Yellow, Radius = Cfg.ObjectBallRadius, Mass = Cfg.ObjectBallMass, Position = new Vec2(-0.2, 0.2 - 0.06 * i) });
        return new TableState { Balls = balls };
    }

    /// <summary>Game already past the break, groups assigned: P1=Red, P1 to play.</summary>
    private static BlackballGame AssignedGame(TableState table)
    {
        var game = new BlackballGame(PlayerId.One);
        // Break: P1 pots nothing but scatters legally; P2's visit assigns groups.
        var (b1, r1) = new ShotBuilder(table).Contact(RedId).Cushion(RedId)
            .MoveTo(RedId, new Vec2(0.3, 0)).MoveTo(RedId + 1, new Vec2(0.3, 0.1)).Build();
        game.ApplyShot(b1, r1);
        Assert.Equal(PlayerId.Two, game.CurrentPlayer);
        var (b2, r2) = new ShotBuilder(b1).Contact(YellowId).Pot(YellowId).Build();
        game.ApplyShot(b2, r2);
        Assert.Equal(BallColor.Yellow, game.GroupOf(PlayerId.Two));
        Assert.Equal(BallColor.Red, game.GroupOf(PlayerId.One));
        // P2 misses to hand the table to P1.
        var (b3, r3) = new ShotBuilder(b2).Contact(YellowId + 1).Cushion(YellowId + 1).Build();
        game.ApplyShot(b3, r3);
        Assert.Equal(PlayerId.One, game.CurrentPlayer);
        Assert.False(game.FreeShot);
        return game;
    }

    // --- break shots ---

    [Fact]
    public void Break_PotOnBreak_BreakerContinues_TableStaysOpen()
    {
        var game = new BlackballGame(PlayerId.One);
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(RedId).Build();
        var v = game.ApplyShot(b, r);

        Assert.False(v.IsFoul);
        Assert.True(v.ShooterContinues);
        Assert.Equal(PlayerId.One, game.CurrentPlayer);
        Assert.True(game.TableOpen); // pots on the break never assign groups
    }

    [Fact]
    public void Break_NoPotFewCrossings_IsIllegalBreakFoul()
    {
        var game = new BlackballGame(PlayerId.One);
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Cushion(RedId)
            .MoveTo(RedId, new Vec2(0.3, 0)).Build(); // only one ball crossed
        var v = game.ApplyShot(b, r);

        Assert.Equal(FoulReason.IllegalBreak, v.Foul);
        Assert.True(v.FreeShotNext);
        Assert.Equal(PlayerId.Two, game.CurrentPlayer);
    }

    [Fact]
    public void Break_TwoBallsCrossCentre_IsLegal()
    {
        var game = new BlackballGame(PlayerId.One);
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Cushion(RedId)
            .MoveTo(RedId, new Vec2(0.3, 0)).MoveTo(YellowId, new Vec2(0.2, 0.1)).Build();
        var v = game.ApplyShot(b, r);

        Assert.False(v.IsFoul);
        Assert.Equal(PlayerId.Two, game.CurrentPlayer); // legal break, nothing potted
    }

    [Fact]
    public void Break_BlackPotted_ReRacksForSameBreaker()
    {
        var game = new BlackballGame(PlayerId.One);
        // Cue also scratched — ignored when the black goes down on the break.
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(BlackId).Pot(CueId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(RackState.ReRack, v.Rack);
        Assert.False(v.IsFoul);
        Assert.Equal(PlayerId.One, game.CurrentPlayer);
        Assert.True(game.IsBreakShot); // ready to break again
        Assert.Equal(RackState.InProgress, game.Rack);
    }

    // --- open table ---

    [Fact]
    public void OpenTable_SingleColourPot_AssignsGroups()
    {
        var game = new BlackballGame(PlayerId.One);
        var (b1, r1) = new ShotBuilder(MidGameTable()).Contact(RedId).Cushion(RedId)
            .MoveTo(RedId, new Vec2(0.3, 0)).MoveTo(YellowId, new Vec2(0.2, 0.1)).Build();
        game.ApplyShot(b1, r1);

        var (b2, r2) = new ShotBuilder(b1).Contact(RedId).Pot(RedId).Build();
        var v = game.ApplyShot(b2, r2);

        Assert.Equal(BallColor.Red, v.GroupAssignedToShooter);
        Assert.Equal(BallColor.Red, game.GroupOf(PlayerId.Two));
        Assert.Equal(BallColor.Yellow, game.GroupOf(PlayerId.One));
        Assert.False(game.TableOpen);
        Assert.True(v.ShooterContinues);
    }

    [Fact]
    public void OpenTable_BothColoursPotted_StaysOpen()
    {
        var game = new BlackballGame(PlayerId.One);
        var (b1, r1) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(RedId).Build();
        game.ApplyShot(b1, r1); // break, P1 continues, open

        var (b2, r2) = new ShotBuilder(b1).Contact(RedId + 1).Pot(RedId + 1).Pot(YellowId).Build();
        var v = game.ApplyShot(b2, r2);

        Assert.False(v.IsFoul);
        Assert.Null(v.GroupAssignedToShooter);
        Assert.True(game.TableOpen);
        Assert.True(v.ShooterContinues);
    }

    [Fact]
    public void OpenTable_BlackFirstContact_IsWrongBallFoul()
    {
        var game = new BlackballGame(PlayerId.One);
        var (b1, r1) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(RedId).Build();
        game.ApplyShot(b1, r1);

        var (b2, r2) = new ShotBuilder(b1).Contact(BlackId).Cushion(BlackId).Build();
        var v = game.ApplyShot(b2, r2);

        Assert.Equal(FoulReason.WrongBallFirst, v.Foul);
    }

    // --- normal play fouls ---

    [Fact]
    public void WrongBallFirst_OpponentColour_IsFoul()
    {
        var game = AssignedGame(MidGameTable()); // P1 on reds
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(YellowId).Cushion(YellowId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(FoulReason.WrongBallFirst, v.Foul);
        Assert.True(v.FreeShotNext);
        Assert.True(v.CueBallInHand);
    }

    [Fact]
    public void NoContact_IsFoul()
    {
        var game = AssignedGame(MidGameTable());
        var (b, r) = new ShotBuilder(MidGameTable()).Cushion(CueId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(FoulReason.NoContact, v.Foul);
    }

    [Fact]
    public void NoRailAfterContact_IsFoul()
    {
        var game = AssignedGame(MidGameTable());
        // Cushion BEFORE contact doesn't count.
        var (b, r) = new ShotBuilder(MidGameTable()).Cushion(CueId).Contact(RedId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(FoulReason.NoRailAfterContact, v.Foul);
    }

    [Fact]
    public void CueScratch_IsFoul_CueInHand()
    {
        var game = AssignedGame(MidGameTable());
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(RedId).Pot(CueId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(FoulReason.CueBallPotted, v.Foul);
        Assert.True(v.CueBallInHand);
        Assert.True(v.FreeShotNext);
    }

    [Fact]
    public void OpponentBallPotted_Alone_IsFoul()
    {
        var game = AssignedGame(MidGameTable()); // P1 on reds
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(YellowId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(FoulReason.OpponentBallPotted, v.Foul);
    }

    [Fact]
    public void OpponentBallPotted_WithOwnBall_IsLegal()
    {
        var game = AssignedGame(MidGameTable()); // P1 on reds
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(RedId).Pot(YellowId).Build();
        var v = game.ApplyShot(b, r);

        Assert.False(v.IsFoul);
        Assert.True(v.ShooterContinues);
    }

    [Fact]
    public void LegalShotNoPot_PassesTurn_NoFreeShot()
    {
        var game = AssignedGame(MidGameTable());
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Cushion(RedId).Build();
        var v = game.ApplyShot(b, r);

        Assert.False(v.IsFoul);
        Assert.False(v.FreeShotNext);
        Assert.Equal(PlayerId.Two, game.CurrentPlayer);
    }

    // --- free shot ---

    [Fact]
    public void FreeShot_WrongBallFirstSuspended_AndOpponentPotCounts()
    {
        var game = AssignedGame(MidGameTable()); // P1 on reds, to play
        var (b1, r1) = new ShotBuilder(MidGameTable()).Contact(YellowId).Cushion(YellowId).Build();
        game.ApplyShot(b1, r1); // P1 fouls; P2 has a free shot
        Assert.True(game.FreeShot);

        // P2 (yellows) hits a red first and pots it — all legal on the free shot.
        var (b2, r2) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(RedId).Build();
        var v = game.ApplyShot(b2, r2);

        Assert.False(v.IsFoul);
        Assert.True(v.ShooterContinues);
        Assert.False(game.FreeShot); // consumed
    }

    [Fact]
    public void FreeShot_DoesNotAssignGroups()
    {
        var game = new BlackballGame(PlayerId.One);
        // Illegal break by P1 → P2 free shot on an open table.
        var (b1, r1) = new ShotBuilder(MidGameTable()).Contact(RedId).Cushion(RedId).Build();
        game.ApplyShot(b1, r1);
        Assert.True(game.FreeShot);

        var (b2, r2) = new ShotBuilder(b1).Contact(RedId).Pot(RedId).Build();
        var v = game.ApplyShot(b2, r2);

        Assert.False(v.IsFoul);
        Assert.Null(v.GroupAssignedToShooter);
        Assert.True(game.TableOpen);
    }

    // --- black ball endings ---

    [Fact]
    public void BlackPotted_AfterClearingGroup_WinsRack()
    {
        var table = MidGameTable(reds: 0, yellows: 3); // P1 (reds) already cleared
        var game = AssignedGame(MidGameTable());
        var (b, r) = new ShotBuilder(table).Contact(BlackId).Pot(BlackId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(RackState.PlayerOneWins, v.Rack);
    }

    [Fact]
    public void BlackPotted_GroupRemaining_LosesRack()
    {
        var game = AssignedGame(MidGameTable());
        var (b, r) = new ShotBuilder(MidGameTable()).Contact(RedId).Pot(BlackId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(RackState.PlayerTwoWins, v.Rack);
    }

    [Fact]
    public void BlackAndCuePotted_TogetherOnTheBlack_LosesRack()
    {
        var table = MidGameTable(reds: 0, yellows: 3);
        var game = AssignedGame(MidGameTable());
        var (b, r) = new ShotBuilder(table).Contact(BlackId).Pot(BlackId).Pot(CueId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(FoulReason.CueBallPotted, v.Foul);
        Assert.Equal(RackState.PlayerTwoWins, v.Rack);
    }

    [Fact]
    public void GroupCleared_MustHitBlackFirst()
    {
        var table = MidGameTable(reds: 0, yellows: 3);
        var game = AssignedGame(MidGameTable());
        var (b, r) = new ShotBuilder(table).Contact(YellowId).Cushion(YellowId).Build();
        var v = game.ApplyShot(b, r);

        Assert.Equal(FoulReason.WrongBallFirst, v.Foul);
    }

    [Fact]
    public void FinishedRack_RejectsFurtherShots()
    {
        var table = MidGameTable(reds: 0, yellows: 3);
        var game = AssignedGame(MidGameTable());
        var (b, r) = new ShotBuilder(table).Contact(BlackId).Pot(BlackId).Build();
        game.ApplyShot(b, r);

        Assert.Throws<InvalidOperationException>(() => game.ApplyShot(b, r));
    }
}
