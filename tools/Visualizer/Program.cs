using PoolGame.Core;
using PoolGame.Tools.Visualizer;

// Renders canned scenarios (or all of them) to animated SVGs.
// Usage: dotnet run [break|pot|draw|sidespin|all] [output-dir]

string scenario = args.Length > 0 ? args[0] : "all";
string outDir = args.Length > 1 ? args[1] : ".";
Directory.CreateDirectory(outDir);

var cfg = new PhysicsConfig();
var sim = new Simulation(cfg);
var renderer = new SvgRenderer(cfg);

var scenarios = new Dictionary<string, Func<(TableState table, Shot shot, string title)>>
{
    ["break"] = () => (Rack.BlackballBreak(cfg),
        CueStrike.Straight(6.5, new Vec2(1, 0.01), cfg.CueBallRadius),
        "Break shot, 6.5 m/s"),

    ["pot"] = () =>
    {
        Vec2 pocket = new(cfg.TableLength / 2, cfg.TableWidth / 2);
        Vec2 dir = new Vec2(1, 0.7).Normalized();
        var table = TwoBallTable(pocket - dir * 0.55, pocket - dir * 0.18);
        Vec2 ghost = Geometry.GhostBall(table.Balls[1].Position, pocket, cfg.ObjectBallRadius, cfg.CueBallRadius);
        return (table, CueStrike.Straight(1.8, ghost - table.CueBall.Position, cfg.CueBallRadius),
            "Corner pot with roll-through");
    },

    ["draw"] = () =>
    {
        var table = TwoBallTable(new Vec2(-0.35, 0), new Vec2(0.1, 0));
        return (table, CueStrike.Create(2.5, new Vec2(1, 0), 0, -0.45, cfg.CueBallRadius),
            "Screw back (draw): cue ball reverses after contact");
    },

    ["sidespin"] = () =>
    {
        var table = TwoBallTable(new Vec2(-0.4, -0.2), new Vec2(10, 10)); // object ball far away: cushion demo
        table.Balls[1].Position = new Vec2(0.6, 0.35); // out of the shot line
        return (table, CueStrike.Create(3.0, new Vec2(1, 0.42), 0.45, 0, cfg.CueBallRadius),
            "Side spin altering cushion rebound");
    },
};

foreach (var (name, make) in scenarios)
{
    if (scenario != "all" && scenario != name) continue;
    var (table, shot, title) = make();
    var trace = new ShotTrace();
    ShotResult result = sim.Run(table, shot, trace);
    string svg = renderer.Render(table, result, trace, title);
    string path = Path.Combine(outDir, $"{name}.svg");
    File.WriteAllText(path, svg);
    Console.WriteLine($"{path}: {result.Events.Count} events, {trace.Duration:F1}s");
}

TableState TwoBallTable(Vec2 cuePos, Vec2 objPos) => new()
{
    Balls = new[]
    {
        new Ball { Id = 0, Color = BallColor.Cue, Radius = cfg.CueBallRadius, Mass = cfg.CueBallMass, Position = cuePos },
        new Ball { Id = 1, Color = BallColor.Red, Radius = cfg.ObjectBallRadius, Mass = cfg.ObjectBallMass, Position = objPos },
    },
};
