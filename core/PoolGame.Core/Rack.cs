namespace PoolGame.Core;

/// <summary>Standard table setups. Table axis convention: X runs from the
/// baulk end (−) to the rack end (+); the black spot sits at +X/4.</summary>
public static class Rack
{
    public const int CueBallId = 0;
    public const int BlackBallId = 15;

    /// <summary>WPA Blackball break setup: triangle of 7 reds + 7 yellows with
    /// the black in the middle of the third row, apex ball on the spot facing
    /// the breaker; cue ball in baulk.</summary>
    public static TableState BlackballBreak(PhysicsConfig cfg)
    {
        var balls = new List<Ball>
        {
            new()
            {
                Id = CueBallId, Color = BallColor.Cue,
                Radius = cfg.CueBallRadius, Mass = cfg.CueBallMass,
                Position = new Vec2(-cfg.TableLength * 0.3, 0),
            },
        };

        // Rows of the triangle, apex toward the cue ball. WPA pattern
        // alternates colours with the black centred in row 3.
        BallColor[][] pattern =
        {
            new[] { BallColor.Red },
            new[] { BallColor.Yellow, BallColor.Red },
            new[] { BallColor.Red, BallColor.Black, BallColor.Yellow },
            new[] { BallColor.Yellow, BallColor.Red, BallColor.Yellow, BallColor.Red },
            new[] { BallColor.Red, BallColor.Yellow, BallColor.Red, BallColor.Yellow, BallColor.Yellow },
        };

        double r = cfg.ObjectBallRadius;
        double spacing = 2 * r + 1e-5; // hair of daylight so the rack isn't pre-overlapping
        Vec2 apex = new(cfg.TableLength / 4, 0);
        double rowDx = spacing * Math.Sqrt(3) / 2;

        int id = 1;
        for (int row = 0; row < pattern.Length; row++)
        {
            for (int i = 0; i < pattern[row].Length; i++)
            {
                var color = pattern[row][i];
                double y = (i - row / 2.0) * spacing;
                balls.Add(new Ball
                {
                    Id = color == BallColor.Black ? BlackBallId : id++,
                    Color = color,
                    Radius = r, Mass = cfg.ObjectBallMass,
                    Position = new Vec2(apex.X + row * rowDx, y),
                });
            }
        }

        return new TableState { Balls = balls };
    }
}
