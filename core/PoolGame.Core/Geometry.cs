namespace PoolGame.Core;

/// <summary>Shared table geometry used by AI candidate generation and, later,
/// the aiming-guide renderer.</summary>
public static class Geometry
{
    /// <summary>Ghost-ball centre: where the cue ball must be at contact for
    /// the object ball to head toward the target.</summary>
    public static Vec2 GhostBall(Vec2 objectBall, Vec2 target, double objectRadius, double cueRadius)
    {
        Vec2 toTarget = (target - objectBall).Normalized();
        return objectBall - toTarget * (objectRadius + cueRadius);
    }

    /// <summary>Cut angle in radians between the cue ball's approach to the
    /// ghost position and the object ball's departure line. 0 = straight;
    /// beyond ~80° a pot is practically impossible.</summary>
    public static double CutAngle(Vec2 cue, Vec2 ghost, Vec2 objectBall, Vec2 target)
    {
        Vec2 approach = (ghost - cue).Normalized();
        Vec2 departure = (target - objectBall).Normalized();
        double dot = Math.Clamp(approach.Dot(departure), -1, 1);
        return Math.Acos(dot);
    }

    /// <summary>True if a ball of radius `movingRadius` travelling the segment
    /// from → to would hit the stationary ball (positions are centres).</summary>
    public static bool PathBlocked(Vec2 from, Vec2 to, double movingRadius, Vec2 obstacle, double obstacleRadius)
    {
        double clearance = movingRadius + obstacleRadius;
        Vec2 seg = to - from;
        double len = seg.Length;
        if (len < 1e-9) return (obstacle - from).Length < clearance;

        double t = Math.Clamp((obstacle - from).Dot(seg) / (len * len), 0, 1);
        Vec2 closest = from + seg * t;
        return (obstacle - closest).Length < clearance - 1e-9;
    }
}
