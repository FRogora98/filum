namespace Filum.Engine.Collections.Trends;

internal static class HoltSmoothing
{
    private const double GridSteps = 10.0;
    private const int MinAlphaStep = 1;
    private const int MaxAlphaStep = 9;
    private const int MaxBetaStep = 5;
    private const double DefaultAlpha = 0.5;
    private const double DefaultBeta = 0.1;

    // On a coarse grid: few points, no fitting surprises.
    public static (double Alpha, double Beta) Fit(double[] values)
    {
        var best = (Alpha: DefaultAlpha, Beta: DefaultBeta, Error: double.MaxValue);
        for (var a = MinAlphaStep; a <= MaxAlphaStep; a++)
        {
            for (var b = 0; b <= MaxBetaStep; b++)
            {
                var (_, _, residuals) = Run(values, a / GridSteps, b / GridSteps);
                var error = residuals.Skip(1).Sum(r => r * r);
                if (error < best.Error)
                {
                    best = (a / GridSteps, b / GridSteps, error);
                }
            }
        }

        return (best.Alpha, best.Beta);
    }

    public static (double Level, double Trend, double[] Residuals) Run(double[] values, double alpha, double beta)
    {
        var residuals = new double[values.Length];
        var level = values[0];
        var trend = values.Length > 1 ? values[1] - values[0] : 0;
        for (var t = 1; t < values.Length; t++)
        {
            var expected = level + trend;
            residuals[t] = values[t] - expected;
            var previous = level;
            level = alpha * values[t] + (1 - alpha) * expected;
            trend = beta * (level - previous) + (1 - beta) * trend;
        }

        return (level, trend, residuals);
    }
}
