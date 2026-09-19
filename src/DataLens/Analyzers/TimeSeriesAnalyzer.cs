using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

/// <summary>
/// Univariate time-series primitives over an ordered series: the dominant period, and a per-point
/// anomaly score by spectral residual saliency. Both run on the statistics engine's own transform,
/// so they need no platform math library.
/// </summary>
/// <remarks>
/// These take a plain series rather than a <c>DataAdapter</c>, like
/// <see cref="UnivariateOutlierDetector"/>: the order of the observations is the input, and a
/// table adapter has no notion of which column is time.
/// </remarks>
public static class TimeSeriesAnalyzer
{
    /// <summary>The fewest observations <see cref="EstimatePeriod"/> accepts.</summary>
    public const int MinimumPeriodPoints = 8;

    /// <summary>The fewest observations <see cref="SpectralResidual"/> accepts.</summary>
    public const int MinimumAnomalyPoints = 12;

    /// <summary>
    /// The dominant period of <paramref name="series"/> (AutoPeriod: a permutation-thresholded
    /// periodogram peak confirmed on the autocorrelation function). Deterministic for a series.
    /// </summary>
    /// <exception cref="ArgumentException">Fewer than <see cref="MinimumPeriodPoints"/> values, or a
    /// value that is not finite.</exception>
    public static SeriesPeriod EstimatePeriod(IReadOnlyList<double> series)
    {
        var data = Validate(series, MinimumPeriodPoints, nameof(series));

        using var client = new InsightClient();
        var estimate = client.EstimatePeriod(data);

        return new SeriesPeriod
        {
            Period = estimate.Period is { } p ? checked((int)p) : null,
            Candidates = estimate.Candidates
                .Select(c => new SeriesPeriodCandidate
                {
                    Period = checked((int)c.Period),
                    Acf = c.Acf,
                    PowerShare = c.PowerShare
                })
                .ToList(),
            SampleSize = checked((int)estimate.N),
            AcfThreshold = estimate.AcfThreshold
        };
    }

    /// <summary>
    /// Scores every observation of <paramref name="series"/> by spectral residual saliency
    /// (Ren et al. 2019) — spikes, steps and dropouts, without a trained model and without assuming
    /// a period.
    /// </summary>
    /// <exception cref="ArgumentException">Fewer than <see cref="MinimumAnomalyPoints"/> values, or a
    /// value that is not finite.</exception>
    public static SeriesAnomalyReport SpectralResidual(
        IReadOnlyList<double> series, Models.SpectralResidualOptions? options = null)
    {
        var data = Validate(series, MinimumAnomalyPoints, nameof(series));

        using var client = new InsightClient();
        var result = client.SpectralResidual(data, ToEngine(options));

        return new SeriesAnomalyReport
        {
            Points = result.Points
                .Select(p => new SeriesAnomalyPoint
                {
                    Index = checked((int)p.Index),
                    Value = p.Value,
                    Saliency = p.Saliency,
                    Score = p.Score,
                    Expected = p.Expected,
                    Lower = p.Lower,
                    Upper = p.Upper,
                    IsAnomaly = p.IsAnomaly
                })
                .ToList(),
            Anomalies = result.Anomalies.Select(i => checked((int)i)).ToList()
        };
    }

    // An option left null keeps the engine's own default, read from a default instance rather than
    // restated here, so the paper's values have one home.
    private static UInsight.SpectralResidualOptions? ToEngine(Models.SpectralResidualOptions? options)
    {
        if (options is null)
            return null;

        var defaults = new UInsight.SpectralResidualOptions();
        return new UInsight.SpectralResidualOptions
        {
            AveragingWindow = options.AveragingWindow is { } q ? checked((uint)q) : defaults.AveragingWindow,
            JudgementWindow = options.JudgementWindow is { } z ? checked((uint)z) : defaults.JudgementWindow,
            Threshold = options.Threshold ?? defaults.Threshold,
            MinZscore = options.MinZscore ?? defaults.MinZscore,
            Sensitivity = options.Sensitivity ?? defaults.Sensitivity,
            BatchSize = options.BatchSize is { } n ? checked((uint)n) : defaults.BatchSize
        };
    }

    private static double[] Validate(IReadOnlyList<double> series, int minimum, string name)
    {
        ArgumentNullException.ThrowIfNull(series, name);
        if (series.Count < minimum)
            throw new ArgumentException(
                $"At least {minimum} observations are required (got {series.Count}).", name);

        var data = new double[series.Count];
        for (var i = 0; i < data.Length; i++)
        {
            if (!double.IsFinite(series[i]))
                throw new ArgumentException($"Observation {i} is not a finite number.", name);
            data[i] = series[i];
        }
        return data;
    }
}
