namespace PoolGame.Core;

/// <summary>
/// Converts player-facing shot controls (aim, power, tip position) into the
/// cue ball's post-impact state. Level cue assumed — elevation (swerve/massé)
/// is a later refinement.
///
/// Impulse J = m·v at contact offset r from centre gives ω = (r × J)/I with
/// I = (2/5)mR². For aim direction d̂ and tip offsets (side a, vertical b) in
/// ball radii: topspin acts about the in-plane axis perpendicular to d̂, side
/// spin about ẑ, both scaled by 5v/(2R).
/// </summary>
public static class CueStrike
{
    /// <summary>Beyond ~half the ball radius the tip slips off — a miscue.
    /// Offsets are clamped to this rather than modelling the miscue itself.</summary>
    public const double MaxTipOffset = 0.5;

    /// <param name="speed">Cue ball speed off the tip, m/s (game caps this; ~7 is a hard break).</param>
    /// <param name="aim">Direction of travel (normalised internally).</param>
    /// <param name="sideOffset">Tip offset in ball radii, positive = right of centre (as the shooter faces the shot).</param>
    /// <param name="verticalOffset">Tip offset in ball radii, positive = above centre (topspin), negative = screw/draw.</param>
    /// <param name="ballRadius">Cue ball radius, m.</param>
    public static Shot Create(double speed, Vec2 aim, double sideOffset, double verticalOffset, double ballRadius)
    {
        Vec2 d = aim.Normalized();
        if (d == Vec2.Zero) throw new ArgumentException("aim direction must be non-zero", nameof(aim));

        (double a, double b) = ClampOffset(sideOffset, verticalOffset);

        double spinScale = 5 * speed / (2 * ballRadius);
        Vec2 topSpinAxis = d.Perp(); // natural-roll spin axis for travel along d̂

        var angular = new Vec3(
            spinScale * b * topSpinAxis.X,
            spinScale * b * topSpinAxis.Y,
            -spinScale * a);

        return new Shot(d * speed, angular);
    }

    /// <summary>A plain centre-ball hit (stun at impact distance zero).</summary>
    public static Shot Straight(double speed, Vec2 aim, double ballRadius) =>
        Create(speed, aim, 0, 0, ballRadius);

    private static (double a, double b) ClampOffset(double a, double b)
    {
        double len = Math.Sqrt(a * a + b * b);
        if (len <= MaxTipOffset) return (a, b);
        double s = MaxTipOffset / len;
        return (a * s, b * s);
    }
}
