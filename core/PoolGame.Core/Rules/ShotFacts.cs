namespace PoolGame.Core.Rules;

/// <summary>
/// Rule-relevant facts extracted from a physics event stream. Keeping the
/// extraction separate from judgement makes both independently testable and
/// lets the rules engine work from scripted event lists in tests.
/// </summary>
public sealed record ShotFacts
{
    public required BallColor? FirstContact { get; init; }
    public required IReadOnlyList<BallColor> PottedInOrder { get; init; }
    /// <summary>Any ball (including the cue) touched a cushion at or after the
    /// moment of first contact.</summary>
    public required bool RailAfterContact { get; init; }
    /// <summary>Object balls that finished on the opposite side of the centre
    /// string from where they started (break legality proxy — the sim does not
    /// yet emit centre-string crossing events, so a ball that crosses and
    /// returns is not counted).</summary>
    public required int BallsCrossedCentreString { get; init; }

    public bool CuePotted => PottedInOrder.Contains(BallColor.Cue);
    public bool BlackPotted => PottedInOrder.Contains(BallColor.Black);
    public int PottedCount(BallColor c) => PottedInOrder.Count(b => b == c);

    public static ShotFacts From(TableState before, ShotResult result)
    {
        BallColor? firstContact = null;
        double firstContactTime = double.PositiveInfinity;
        bool rail = false;
        var potted = new List<BallColor>();

        foreach (SimEvent e in result.Events)
        {
            switch (e.Type)
            {
                case SimEventType.BallCollision when firstContact is null && IsCueContact(before, e):
                    Ball hit = before.ById(e.BallId)!.IsCue
                        ? before.ById(e.OtherBallId)!
                        : before.ById(e.BallId)!;
                    firstContact = hit.Color;
                    firstContactTime = e.Time;
                    break;
                case SimEventType.CushionHit when e.Time >= firstContactTime:
                    rail = true;
                    break;
                case SimEventType.BallPotted:
                    potted.Add(before.ById(e.BallId)!.Color);
                    break;
            }
        }

        int crossed = before.Balls.Where(b => !b.IsCue && b.InPlay).Count(b =>
        {
            Ball? after = result.FinalState.ById(b.Id);
            return after is { Potted: false } && Math.Sign(after.Position.X) != Math.Sign(b.Position.X);
        });

        return new ShotFacts
        {
            FirstContact = firstContact,
            PottedInOrder = potted,
            RailAfterContact = rail,
            BallsCrossedCentreString = crossed,
        };
    }

    private static bool IsCueContact(TableState before, SimEvent e) =>
        before.ById(e.BallId)!.IsCue || before.ById(e.OtherBallId)!.IsCue;
}
