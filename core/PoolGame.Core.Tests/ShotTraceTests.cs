using PoolGame.Core;

namespace PoolGame.Core.Tests;

public class ShotTraceTests
{
    private static readonly PhysicsConfig Cfg = new();

    [Fact]
    public void Trace_EndsWhereTheSimulationEnds()
    {
        var sim = new Simulation(Cfg);
        var table = Rack.BlackballBreak(Cfg);
        var trace = new ShotTrace();
        var result = sim.Run(table, new Shot(new Vec2(6.5, 0.05), Vec3.Zero), trace);

        foreach (Ball final in result.FinalState.Balls.Where(b => b.InPlay))
        {
            Vec2? traced = trace.PositionAt(final.Id, trace.Duration);
            if (traced is null) continue; // never moved
            Assert.Equal(final.Position.X, traced.Value.X, 6);
            Assert.Equal(final.Position.Y, traced.Value.Y, 6);
        }
    }

    [Fact]
    public void Trace_SegmentsAreContiguousInTimeAndSpace()
    {
        var sim = new Simulation(Cfg);
        var table = Rack.BlackballBreak(Cfg);
        var trace = new ShotTrace();
        sim.Run(table, new Shot(new Vec2(5.0, 0.1), Vec3.Zero), trace);

        foreach (var (_, segs) in trace.Paths)
        {
            for (int i = 1; i < segs.Count; i++)
            {
                // Time may gap (ball at rest between two spells of motion) but
                // never run backwards, and position must be continuous.
                Assert.True(segs[i].T0 >= segs[i - 1].T1 - 1e-9);
                Vec2 endPrev = segs[i - 1].PositionAt(segs[i - 1].T1);
                Assert.Equal(endPrev.X, segs[i].P0.X, 6);
                Assert.Equal(endPrev.Y, segs[i].P0.Y, 6);
            }
        }
    }

    [Fact]
    public void Trace_DoesNotChangeSimulationOutcome()
    {
        var sim = new Simulation(Cfg);
        var table = Rack.BlackballBreak(Cfg);
        var shot = new Shot(new Vec2(6.0, 0.2), new Vec3(0, 0, 10));

        var plain = sim.Run(table, shot);
        var traced = sim.Run(table, shot, new ShotTrace());

        Assert.Equal(plain.Events.Count, traced.Events.Count);
        for (int i = 0; i < plain.Events.Count; i++)
            Assert.Equal(plain.Events[i], traced.Events[i]);
    }
}
