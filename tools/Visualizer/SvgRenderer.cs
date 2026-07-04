using System.Globalization;
using System.Text;
using PoolGame.Core;

namespace PoolGame.Tools.Visualizer;

/// <summary>
/// Renders a traced shot as a self-contained animated SVG: table, pockets,
/// faded path lines, pot markers, and SMIL-animated balls following the exact
/// simulated motion (sampled at 60 Hz between exact segment endpoints).
/// </summary>
public sealed class SvgRenderer
{
    private const double Scale = 500; // px per metre
    private const double Margin = 0.08; // m of surround drawn around the cloth

    private readonly PhysicsConfig _cfg;

    public SvgRenderer(PhysicsConfig cfg) => _cfg = cfg;

    public string Render(TableState before, ShotResult result, ShotTrace trace, string title)
    {
        var ci = CultureInfo.InvariantCulture;
        double w = (_cfg.TableLength + 2 * Margin) * Scale;
        double h = (_cfg.TableWidth + 2 * Margin) * Scale + 40;
        double dur = Math.Max(trace.Duration, 0.1);

        var svg = new StringBuilder();
        svg.AppendLine(string.Create(ci, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w:F0} {h:F0}\" font-family=\"sans-serif\">"));
        svg.AppendLine(string.Create(ci, $"<rect width=\"{w:F0}\" height=\"{h:F0}\" fill=\"#1a1a2e\"/>"));
        svg.AppendLine(string.Create(ci, $"<text x=\"{w / 2:F0}\" y=\"26\" fill=\"#ddd\" font-size=\"17\" text-anchor=\"middle\">{title} — {dur:F1}s</text>"));

        // Rails and cloth.
        svg.AppendLine(string.Create(ci, $"<rect x=\"{X(-_cfg.TableLength / 2 - 0.05):F1}\" y=\"{Y(_cfg.TableWidth / 2 + 0.05):F1}\" width=\"{(_cfg.TableLength + 0.1) * Scale:F1}\" height=\"{(_cfg.TableWidth + 0.1) * Scale:F1}\" rx=\"14\" fill=\"#5c3a21\"/>"));
        svg.AppendLine(string.Create(ci, $"<rect x=\"{X(-_cfg.TableLength / 2):F1}\" y=\"{Y(_cfg.TableWidth / 2):F1}\" width=\"{_cfg.TableLength * Scale:F1}\" height=\"{_cfg.TableWidth * Scale:F1}\" fill=\"#1f6e43\"/>"));

        // Baulk line (cue-ball quarter of the table).
        double baulkX = -_cfg.TableLength / 4;
        svg.AppendLine(string.Create(ci, $"<line x1=\"{X(baulkX):F1}\" y1=\"{Y(_cfg.TableWidth / 2):F1}\" x2=\"{X(baulkX):F1}\" y2=\"{Y(-_cfg.TableWidth / 2):F1}\" stroke=\"#ffffff40\" stroke-width=\"1.5\"/>"));

        foreach (Pocket p in _cfg.Pockets)
            svg.AppendLine(string.Create(ci, $"<circle cx=\"{X(p.Center.X):F1}\" cy=\"{Y(p.Center.Y):F1}\" r=\"{p.CaptureRadius * Scale:F1}\" fill=\"#0b0b14\"/>"));

        // Path lines under the balls.
        foreach (var (id, segs) in trace.Paths)
        {
            Ball ball = before.ById(id)!;
            var pts = SamplePath(segs);
            var path = new StringBuilder();
            for (int i = 0; i < pts.Count; i++)
                path.Append(string.Create(ci, $"{(i == 0 ? 'M' : 'L')}{X(pts[i].X):F1},{Y(pts[i].Y):F1}"));
            svg.AppendLine($"<path d=\"{path}\" fill=\"none\" stroke=\"{Colour(ball.Color)}\" stroke-opacity=\"0.35\" stroke-width=\"2\"/>");
        }

        // Pot markers.
        foreach (SimEvent e in result.Events.Where(e => e.Type == SimEventType.BallPotted))
        {
            Pocket p = _cfg.Pockets[e.OtherBallId];
            svg.AppendLine(string.Create(ci, $"<circle cx=\"{X(p.Center.X):F1}\" cy=\"{Y(p.Center.Y):F1}\" r=\"{p.CaptureRadius * Scale * 0.75:F1}\" fill=\"none\" stroke=\"#ffd54a\" stroke-width=\"2.5\" opacity=\"0\"><animate attributeName=\"opacity\" values=\"0;1;0\" begin=\"{e.Time:F2}s\" dur=\"0.6s\" fill=\"freeze\"/></circle>"));
        }

        // Balls, animated along their sampled positions.
        foreach (Ball ball in before.Balls.Where(b => b.InPlay))
        {
            double r = ball.Radius * Scale;
            string fill = Colour(ball.Color);
            if (!trace.Paths.TryGetValue(ball.Id, out var segs))
            {
                svg.AppendLine(string.Create(ci, $"<circle cx=\"{X(ball.Position.X):F1}\" cy=\"{Y(ball.Position.Y):F1}\" r=\"{r:F1}\" fill=\"{fill}\" stroke=\"#00000060\"/>"));
                continue;
            }

            var (times, xs, ys) = SampleAnimation(segs, dur);
            bool potted = result.FinalState.ById(ball.Id)!.Potted;
            double potTime = potted
                ? result.Events.First(e => e.Type == SimEventType.BallPotted && e.BallId == ball.Id).Time
                : double.NaN;

            svg.Append(string.Create(ci, $"<circle r=\"{r:F1}\" cx=\"{xs[0]}\" cy=\"{ys[0]}\" fill=\"{fill}\" stroke=\"#00000060\">"));
            svg.Append(string.Create(ci, $"<animate attributeName=\"cx\" dur=\"{dur:F3}s\" repeatCount=\"indefinite\" calcMode=\"linear\" keyTimes=\"{string.Join(';', times)}\" values=\"{string.Join(';', xs)}\"/>"));
            svg.Append(string.Create(ci, $"<animate attributeName=\"cy\" dur=\"{dur:F3}s\" repeatCount=\"indefinite\" calcMode=\"linear\" keyTimes=\"{string.Join(';', times)}\" values=\"{string.Join(';', ys)}\"/>"));
            if (potted)
                svg.Append(string.Create(ci, $"<animate attributeName=\"opacity\" dur=\"{dur:F3}s\" repeatCount=\"indefinite\" calcMode=\"discrete\" keyTimes=\"0;{Math.Min(potTime / dur, 0.999):F4};1\" values=\"1;0;0\"/>"));
            svg.AppendLine("</circle>");
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private double X(double x) => (x + _cfg.TableLength / 2 + Margin) * Scale;
    private double Y(double y) => (_cfg.TableWidth / 2 - y + Margin) * Scale + 40;

    private static string Colour(BallColor c) => c switch
    {
        BallColor.Cue => "#f5f0e6",
        BallColor.Red => "#d3342c",
        BallColor.Yellow => "#e8b820",
        _ => "#111111",
    };

    private static List<Vec2> SamplePath(List<PathSegment> segs)
    {
        var pts = new List<Vec2>();
        foreach (PathSegment s in segs)
        {
            int n = Math.Max(2, (int)((s.T1 - s.T0) * 30));
            for (int i = 0; i <= n; i++)
                pts.Add(s.PositionAt(s.T0 + (s.T1 - s.T0) * i / n));
        }
        return pts;
    }

    private (List<string> times, List<string> xs, List<string> ys) SampleAnimation(List<PathSegment> segs, double dur)
    {
        var ci = CultureInfo.InvariantCulture;
        var times = new List<string>();
        var xs = new List<string>();
        var ys = new List<string>();

        void Add(double t, Vec2 p)
        {
            times.Add((t / dur).ToString("F4", ci));
            xs.Add(X(p.X).ToString("F1", ci));
            ys.Add(Y(p.Y).ToString("F1", ci));
        }

        Add(0, segs[0].P0);
        const double step = 1.0 / 60;
        foreach (PathSegment s in segs)
            for (double t = s.T0 + step; t < s.T1; t += step)
                Add(t, s.PositionAt(t));
        Vec2 rest = segs[^1].PositionAt(segs[^1].T1);
        Add(segs[^1].T1, rest);
        if (segs[^1].T1 < dur) Add(dur, rest);

        // keyTimes must be strictly non-decreasing and end at 1.
        times[^1] = "1.0000";
        return (times, xs, ys);
    }
}
