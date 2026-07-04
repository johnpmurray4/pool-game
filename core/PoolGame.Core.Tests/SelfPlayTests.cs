using PoolGame.Core;
using PoolGame.Core.Ai;
using PoolGame.Core.Rules;
using Xunit.Abstractions;

namespace PoolGame.Core.Tests;

/// <summary>AI vs AI full racks — the closest thing to an end-to-end soak test
/// the core can run headlessly. Also a preview of the game layer's shot loop
/// (cue-ball-in-hand placement, re-racks).</summary>
public class SelfPlayTests(ITestOutputHelper output)
{
    private static readonly PhysicsConfig Cfg = new();

    [Fact]
    public void AiVsAi_CompletesRacks_WithinShotBudget()
    {
        var sim = new Simulation(Cfg);
        var players = new Dictionary<PlayerId, AiPlayer>
        {
            [PlayerId.One] = new(sim, AiDifficulty.Medium),
            [PlayerId.Two] = new(sim, AiDifficulty.Medium),
        };

        int finished = 0;
        for (int rackNo = 0; rackNo < 3; rackNo++)
        {
            var game = new BlackballGame(PlayerId.One);
            TableState table = Rack.BlackballBreak(Cfg);
            int shots = 0;
            const int shotBudget = 150; // a real rack is ~15-40 shots

            while (game.Rack == RackState.InProgress && shots < shotBudget)
            {
                Shot shot = players[game.CurrentPlayer].ChooseShot(table, game, seed: rackNo * 1000 + shots);
                ShotResult result = sim.Run(table, shot);
                ShotVerdict verdict = game.ApplyShot(table, result);
                shots++;

                if (verdict.Rack == RackState.ReRack)
                {
                    table = Rack.BlackballBreak(Cfg);
                    continue;
                }

                table = result.FinalState;
                if (table.CueBall.Potted)
                    RestoreCueBallInBaulk(table);

                // Invariants: everything settled, and no ball may ever escape
                // the cushions (regression guard: pocket-mouth handling once
                // let balls sail off the table).
                Assert.All(table.Balls, b => Assert.False(b.Moving));
                Assert.All(table.Balls.Where(b => b.InPlay), b =>
                {
                    Assert.True(Math.Abs(b.Position.X) <= Cfg.TableLength / 2 + 1e-6,
                        $"ball {b.Id} escaped: x={b.Position.X:F3}");
                    Assert.True(Math.Abs(b.Position.Y) <= Cfg.TableWidth / 2 + 1e-6,
                        $"ball {b.Id} escaped: y={b.Position.Y:F3}");
                });
            }

            output.WriteLine($"rack {rackNo}: {shots} shots, result={game.Rack}");
            if (game.Rack is RackState.PlayerOneWins or RackState.PlayerTwoWins)
                finished++;
        }

        // Medium AI must close out most racks; a stuck rack means the loop or
        // the AI has regressed even if no assertion inside tripped.
        Assert.True(finished >= 2, $"only {finished}/3 racks finished");
    }

    /// <summary>Game-layer preview: cue ball in hand in baulk after a scratch.
    /// Baulk is the cue-ball starting quarter of the table (−X end).</summary>
    private static void RestoreCueBallInBaulk(TableState table)
    {
        Ball cue = table.CueBall;
        for (double y = 0; y <= 0.3; y = y <= 0 ? -y + 0.03 : -y)
        {
            var pos = new Vec2(-Cfg.TableLength * 0.3, y);
            bool clear = table.Balls.All(b => b.Potted || b.IsCue
                || (b.Position - pos).Length > b.Radius + cue.Radius + 0.001);
            if (clear)
            {
                cue.Potted = false;
                cue.Position = pos;
                cue.Velocity = Vec2.Zero;
                cue.AngularVelocity = Vec3.Zero;
                cue.State = MotionState.Stationary;
                return;
            }
        }
        throw new InvalidOperationException("no clear baulk spot for the cue ball");
    }
}
