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

    /// <summary>The fewest observations <see cref="Autocorrelation"/> accepts.</summary>
    public const int MinimumAutocorrelationPoints = 4;

    /// <summary>The fewest observations <see cref="Stationarity"/> accepts.</summary>
    public const int MinimumStationarityPoints = 10;

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
                    IsAnomaly = p.IsAnomaly,
                    NearEdge = p.NearEdge
                })
                .ToList(),
            Anomalies = result.Anomalies.Select(i => checked((int)i)).ToList()
        };
    }

    /// <summary>
    /// The autocorrelation and partial autocorrelation functions of <paramref name="series"/>, with the
    /// bands a correlogram draws (the white-noise threshold and Bartlett's limits) — the evidence for
    /// choosing a moving-average order (autocorrelation) and an autoregressive order (partial
    /// autocorrelation). Which model to fit, and with what order, stays the caller's choice.
    /// </summary>
    /// <param name="series">The ordered observations.</param>
    /// <param name="maxLag">The largest lag, or <c>null</c> for the engine's default (the usual
    /// <c>min(10·log10 n, n − 1)</c>). The partial autocorrelation stops below half the sample size,
    /// so it is never asked for more than that.</param>
    /// <exception cref="ArgumentException">Fewer than <see cref="MinimumAutocorrelationPoints"/> values,
    /// a value that is not finite, or a <paramref name="maxLag"/> outside what the sample allows.</exception>
    /// <exception cref="InsightException">The series does not vary (its autocorrelation is
    /// undefined).</exception>
    public static SeriesAutocorrelation Autocorrelation(IReadOnlyList<double> series, int? maxLag = null)
    {
        var data = Validate(series, MinimumAutocorrelationPoints, nameof(series));

        using var client = new InsightClient();
        UInsight.AcfResult acf;
        UInsight.PacfResult pacf;
        try
        {
            acf = client.Acf(data, maxLag);
            pacf = client.Pacf(data, maxLag is { } k ? Math.Min(k, data.Length / 2 - 1) : null);
        }
        catch (InsightException e) when (e.Category == InsightErrorCategory.InvalidParameter && e.Parameter == "max_lag")
        {
            throw new ArgumentException($"{nameof(maxLag)}: {e.Message}", nameof(maxLag), e);
        }

        return new SeriesAutocorrelation
        {
            SampleSize = data.Length,
            Acf = acf.Acf,
            AcfThreshold = acf.ConfidenceThreshold,
            AcfBartlettLimits = acf.BartlettLimits,
            Pacf = pacf.Pacf,
            PacfThreshold = pacf.ConfidenceThreshold
        };
    }

    /// <summary>
    /// Whether <paramref name="series"/> is stationary, by two tests with opposite nulls: the augmented
    /// Dickey-Fuller test (H0: a unit root) and KPSS (H0: stationary). Neither rejecting, or both,
    /// is a result in itself; how many differences to take is a policy and stays the caller's.
    /// </summary>
    /// <param name="series">The ordered observations.</param>
    /// <param name="aroundTrend"><c>false</c> (the default) tests stationarity around a constant level;
    /// <c>true</c> around a linear trend. The same choice is made for both tests.</param>
    /// <exception cref="ArgumentException">Fewer than <see cref="MinimumStationarityPoints"/> values or a
    /// value that is not finite.</exception>
    /// <exception cref="InsightException">The series does not vary, or is so regular that a test's
    /// statistic is undefined.</exception>
    public static SeriesStationarity Stationarity(IReadOnlyList<double> series, bool aroundTrend = false)
    {
        var data = Validate(series, MinimumStationarityPoints, nameof(series));

        using var client = new InsightClient();
        var adf = client.AdfTest(data, aroundTrend ? AdfModel.ConstantTrend : AdfModel.Constant);
        var kpss = client.KpssTest(data, aroundTrend ? KpssModel.Trend : KpssModel.Level);

        return new SeriesStationarity
        {
            SampleSize = data.Length,
            AroundTrend = aroundTrend,
            Adf = new UnitRootTest
            {
                Statistic = adf.Statistic,
                Lags = checked((int)adf.NLags),
                Observations = checked((int)adf.NObs),
                Levels = adf.Levels.Select(l => new TestLevel
                {
                    Level = l.Level,
                    CriticalValue = l.CriticalValue,
                    Rejected = l.Rejected
                }).ToList()
            },
            Kpss = new StationarityTest
            {
                Statistic = kpss.Statistic,
                Lags = checked((int)kpss.NLags),
                PValue = kpss.PValue,
                PValueBound = kpss.PValueBound switch
                {
                    KpssPValueBound.AtMost => "at-most",
                    KpssPValueBound.AtLeast => "at-least",
                    _ => "interpolated"
                },
                Levels = kpss.Levels.Select(l => new TestLevel
                {
                    Level = l.Level,
                    CriticalValue = l.CriticalValue,
                    Rejected = l.Rejected
                }).ToList()
            }
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
