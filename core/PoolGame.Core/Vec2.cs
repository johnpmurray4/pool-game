namespace PoolGame.Core;

/// <summary>2D vector in the table plane. Double precision throughout the sim;
/// convert to float only at the rendering boundary.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => a * s;
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);

    public double Dot(Vec2 b) => X * b.X + Y * b.Y;
    public double Cross(Vec2 b) => X * b.Y - Y * b.X;
    public double LengthSquared => X * X + Y * Y;
    public double Length => Math.Sqrt(LengthSquared);

    public Vec2 Normalized()
    {
        double len = Length;
        return len > 0 ? this / len : Zero;
    }

    /// <summary>Counter-clockwise perpendicular (equals ẑ × this).</summary>
    public Vec2 Perp() => new(-Y, X);
}
