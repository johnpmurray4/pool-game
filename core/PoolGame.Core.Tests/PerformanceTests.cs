using System.Diagnostics;
using PoolGame.Core;
using Xunit.Abstractions;

namespace PoolGame.Core.Tests;

public class PerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void BreakShot_SimulatesFastEnoughForAi()
    {
        var cfg = new PhysicsConfig();
        var sim = new Simulation(cfg);
        var state = Rack.BlackballBreak(cfg);
        var shot = new Shot(new Vec2(7.0, 0.05), Vec3.Zero);

        sim.Run(state, shot); // warm-up/JIT

        const int runs = 50;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
            sim.Run(state, shot);
        sw.Stop();

        double msPerShot = sw.Elapsed.TotalMilliseconds / runs;
        output.WriteLine($"break shot: {msPerShot:F2} ms ({runs} runs)");

        // AI budget: ~200 candidate sims inside a 2 s decision window on a phone.
        // A break is the worst-case shot; desktop CI gets a loose 100 ms ceiling
        // (phones are ~5-10x slower — the Phase 0 on-device benchmark measures that).
        Assert.True(msPerShot < 100, $"break sim too slow: {msPerShot:F1} ms");
    }
}
