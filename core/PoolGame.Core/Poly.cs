namespace PoolGame.Core;

/// <summary>
/// Real-root isolation for low-degree polynomials (≤ quartic here). Roots of
/// the derivative split the interval into monotonic brackets; each bracket
/// with a sign change is bisected. Robust for the collision-time polynomials
/// the simulation produces, with no step-size/tunneling concerns.
/// Coefficients are ascending: c[0] + c[1]·t + c[2]·t² + …
/// </summary>
internal static class Poly
{
    public static double Eval(double[] c, double t)
    {
        double r = 0;
        for (int i = c.Length - 1; i >= 0; i--)
            r = r * t + c[i];
        return r;
    }

    public static double[] Derivative(double[] c)
    {
        if (c.Length <= 1) return new[] { 0.0 };
        var d = new double[c.Length - 1];
        for (int i = 1; i < c.Length; i++)
            d[i - 1] = c[i] * i;
        return d;
    }

    private static int Degree(double[] c)
    {
        int d = c.Length - 1;
        while (d > 0 && Math.Abs(c[d]) < 1e-14) d--;
        return d;
    }

    /// <summary>All real roots in [lo, hi], ascending.</summary>
    public static List<double> RootsIn(double[] c, double lo, double hi)
    {
        var roots = new List<double>();
        int deg = Degree(c);

        if (deg == 0) return roots;
        if (deg == 1)
        {
            double r = -c[0] / c[1];
            if (r >= lo && r <= hi) roots.Add(r);
            return roots;
        }

        List<double> knots = RootsIn(Derivative(c[..(deg + 1)]), lo, hi);
        knots.Insert(0, lo);
        knots.Add(hi);

        for (int i = 0; i < knots.Count - 1; i++)
        {
            double a = knots[i], b = knots[i + 1];
            if (b - a < 1e-15) continue;
            double fa = Eval(c, a), fb = Eval(c, b);
            if (fa == 0) { AddRoot(roots, a); continue; }
            if (fb == 0 && i == knots.Count - 2) { AddRoot(roots, b); continue; }
            if (fa * fb < 0)
                AddRoot(roots, Bisect(c, a, b, fa));
        }
        return roots;
    }

    private static void AddRoot(List<double> roots, double r)
    {
        if (roots.Count == 0 || Math.Abs(roots[^1] - r) > 1e-12)
            roots.Add(r);
    }

    private static double Bisect(double[] c, double lo, double hi, double fLo)
    {
        for (int i = 0; i < 80 && hi - lo > 1e-14; i++)
        {
            double mid = 0.5 * (lo + hi);
            double fm = Eval(c, mid);
            if (fm == 0) return mid;
            if (fm * fLo > 0) { lo = mid; fLo = fm; }
            else hi = mid;
        }
        return 0.5 * (lo + hi);
    }
}
