namespace PoolGame.Core;

/// <summary>
/// Closed-form single-ball motion under cloth friction (Han 2005 / the standard
/// billiards literature). Within one motion regime every ball's acceleration is
/// constant, so positions are quadratic in time — which is what lets the
/// simulation solve for event times instead of stepping frame by frame.
///
/// Sliding: the contact-point velocity u keeps a fixed direction and decays
/// linearly at (7/2)·μs·g; centre velocity decelerates at μs·g along û.
/// Rolling: straight-line deceleration at μr·g until rest.
/// Side spin (ωz) decays independently at (5/2)·μsp·g/R.
/// </summary>
public static class BallMotion
{
    public readonly record struct Segment(Vec2 Acceleration, double Duration);

    /// <summary>Constant acceleration and remaining duration of the ball's
    /// current motion regime.</summary>
    public static Segment CurrentSegment(Ball b, PhysicsConfig cfg)
    {
        switch (b.State)
        {
            case MotionState.Sliding:
            {
                Vec2 u = b.SurfaceVelocity;
                double uLen = u.Length;
                if (uLen < cfg.RestSpeed)
                    return new Segment(Vec2.Zero, 0); // regime already over
                Vec2 uHat = u / uLen;
                double duration = 2 * uLen / (7 * cfg.SlidingFriction * cfg.Gravity);
                return new Segment(-cfg.SlidingFriction * cfg.Gravity * uHat, duration);
            }
            case MotionState.Rolling:
            {
                double vLen = b.Velocity.Length;
                if (vLen < cfg.RestSpeed)
                    return new Segment(Vec2.Zero, 0);
                Vec2 vHat = b.Velocity / vLen;
                double duration = vLen / (cfg.RollingFriction * cfg.Gravity);
                return new Segment(-cfg.RollingFriction * cfg.Gravity * vHat, duration);
            }
            default:
                return new Segment(Vec2.Zero, double.PositiveInfinity);
        }
    }

    /// <summary>Advance the ball by dt, which must not exceed the current
    /// segment's duration. If dt lands exactly on the segment end, the ball
    /// transitions to the next regime (sliding→rolling→stationary).</summary>
    public static void Advance(Ball b, double dt, PhysicsConfig cfg)
    {
        if (b.State == MotionState.Stationary || dt <= 0)
        {
            DecaySideSpin(b, dt, cfg);
            return;
        }

        Segment seg = CurrentSegment(b, cfg);
        bool completes = dt >= seg.Duration - 1e-12;

        b.Position += b.Velocity * dt + 0.5 * dt * dt * seg.Acceleration;

        if (b.State == MotionState.Sliding)
        {
            Vec2 uHat = b.SurfaceVelocity.Normalized();
            b.Velocity += seg.Acceleration * dt;
            // Friction torque spins the ball toward natural roll:
            // dω/dt = (5 μs g)/(2R) (ẑ × û)
            double k = 5 * cfg.SlidingFriction * cfg.Gravity / (2 * b.Radius) * dt;
            Vec2 zxu = uHat.Perp();
            b.AngularVelocity = new Vec3(
                b.AngularVelocity.X + k * zxu.X,
                b.AngularVelocity.Y + k * zxu.Y,
                b.AngularVelocity.Z);

            if (completes)
                StartRolling(b, cfg);
        }
        else // Rolling
        {
            b.Velocity += seg.Acceleration * dt;
            if (completes || b.Velocity.Length < cfg.RestSpeed)
                Stop(b);
            else
                SetNaturalRollSpin(b);
        }

        DecaySideSpin(b, dt, cfg);
    }

    /// <summary>Classify the ball's regime from its current velocities, used
    /// after any impulse (cue strike, collision, cushion).</summary>
    public static void Reclassify(Ball b, PhysicsConfig cfg)
    {
        if (b.Velocity.Length < cfg.RestSpeed && b.SurfaceVelocity.Length < cfg.RestSpeed)
        {
            Stop(b);
        }
        else if (b.SurfaceVelocity.Length < cfg.RestSpeed)
        {
            StartRolling(b, cfg);
        }
        else
        {
            b.State = MotionState.Sliding;
        }
    }

    private static void StartRolling(Ball b, PhysicsConfig cfg)
    {
        if (b.Velocity.Length < cfg.RestSpeed)
        {
            Stop(b);
            return;
        }
        b.State = MotionState.Rolling;
        SetNaturalRollSpin(b);
    }

    private static void SetNaturalRollSpin(Ball b)
    {
        // Natural roll: ω = (ẑ × v)/R, side spin preserved.
        b.AngularVelocity = new Vec3(-b.Velocity.Y / b.Radius, b.Velocity.X / b.Radius, b.AngularVelocity.Z);
    }

    private static void Stop(Ball b)
    {
        b.State = MotionState.Stationary;
        b.Velocity = Vec2.Zero;
        b.AngularVelocity = new Vec3(0, 0, b.AngularVelocity.Z);
    }

    private static void DecaySideSpin(Ball b, double dt, PhysicsConfig cfg)
    {
        double wz = b.AngularVelocity.Z;
        if (wz == 0) return;
        double decay = 5 * cfg.SpinFriction * cfg.Gravity / (2 * b.Radius) * dt;
        double newWz = Math.Abs(wz) <= decay ? 0 : wz - Math.Sign(wz) * decay;
        b.AngularVelocity = b.AngularVelocity with { Z = newWz };
    }
}
