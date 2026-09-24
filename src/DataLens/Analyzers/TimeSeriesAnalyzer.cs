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
    /// <exception cref="ArgumentException">Fewer than <see cref="MinimumAnomalyPoints"/> values, a
    /// value that is not finite, or an option out of range (the message starts with that option's
    /// name).</exception>
    public static SeriesAnomalyReport SpectralResidual(
        IReadOnlyList<double> series, Models.SpectralResidualOptions? options = null)
    {
        var data = Validate(series, MinimumAnomalyPoints, nameof(series));

        using var client = new InsightClient();
        UInsight.SpectralResidualResult result;
        try
        {
            result = client.SpectralResidual(data, ToEngine(options));
        }
        catch (InsightException e) when (e.Category == InsightErrorCategory.InvalidParameter
                                         && e.Parameter is { } engineName
                                         && OptionNames.TryGetValue(engineName, out var name))
        {
            // The engine names the option in its own spelling and states its rule; this layer only
            // puts the option in ours. The rule itself stays the engine's.
            var rule = e.Message.StartsWith(engineName + " ", StringComparison.Ordinal)
                ? e.Message[(engineName.Length + 1)..]
                : e.Message;
            throw new ArgumentException($"{name} {rule}.", nameof(options), e);
        }

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

    // The engine's option names, in the spelling of this library's options.
    private static readonly IReadOnlyDictionary<string, string> OptionNames = new Dictionary<string, string>
    {
        ["averaging_window"] = nameof(Models.SpectralResidualOptions.AveragingWindow),
        ["judgement_window"] = nameof(Models.SpectralResidualOptions.JudgementWindow),
        ["threshold"] = nameof(Models.SpectralResidualOptions.Threshold),
        ["min_zscore"] = nameof(Models.SpectralResidualOptions.MinZscore),
        ["sensitivity"] = nameof(Models.SpectralResidualOptions.Sensitivity),
        ["batch_size"] = nameof(Models.SpectralResidualOptions.BatchSize)
    };

    // An option left null keeps the engine's own default, read from a default instance rather than
    // restated here, so the paper's values have one home. The engine checks every option's range;
    // this layer checks only what its own conversion needs -- a count has no negative in the
    // engine's unsigned type.
    private static UInsight.SpectralResidualOptions? ToEngine(Models.SpectralResidualOptions? options)
    {
        if (options is null)
            return null;

        var defaults = new UInsight.SpectralResidualOptions();
        return new UInsight.SpectralResidualOptions
        {
            AveragingWindow = options.AveragingWindow is { } q
                ? Count(q, nameof(options.AveragingWindow))
                : defaults.AveragingWindow,
            JudgementWindow = options.JudgementWindow is { } z
                ? Count(z, nameof(options.JudgementWindow))
                : defaults.JudgementWindow,
            Threshold = options.Threshold ?? defaults.Threshold,
            MinZscore = options.MinZscore ?? defaults.MinZscore,
            Sensitivity = options.Sensitivity ?? defaults.Sensitivity,
            BatchSize = options.BatchSize is { } n ? Count(n, nameof(options.BatchSize)) : defaults.BatchSize
        };
    }

    private static uint Count(int value, string option) =>
        value >= 0
            ? (uint)value
            : throw new ArgumentException($"{option} must not be negative (got {value}).", "options");

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
