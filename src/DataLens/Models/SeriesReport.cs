namespace DataLens.Models;

/// <summary>
/// The dominant period of a univariate series, or none. See
/// <see cref="Analyzers.TimeSeriesAnalyzer.EstimatePeriod"/>.
/// </summary>
public sealed record SeriesPeriod
{
    /// <summary>The dominant period in observations, or <c>null</c> when no periodicity passed both
    /// the periodogram and the autocorrelation checks — a finding, not a failure.</summary>
    public int? Period { get; init; }

    /// <summary>Every validated candidate, strongest first.</summary>
    public IReadOnlyList<SeriesPeriodCandidate> Candidates { get; init; } = [];

    /// <summary>Number of observations the estimate was made from.</summary>
    public int SampleSize { get; init; }

    /// <summary>The 95% white-noise bound on the autocorrelation, <c>1.96 / sqrt(n)</c>.</summary>
    public double AcfThreshold { get; init; }
}

/// <summary>One validated period candidate.</summary>
public sealed record SeriesPeriodCandidate
{
    /// <summary>Integer period, in observations.</summary>
    public int Period { get; init; }

    /// <summary>Autocorrelation at that lag — the strength of the periodicity.</summary>
    public double Acf { get; init; }

    /// <summary>The candidate's share of the total periodogram power.</summary>
    public double PowerShare { get; init; }
}

/// <summary>
/// Options for <see cref="Analyzers.TimeSeriesAnalyzer.SpectralResidual"/>. Every value left
/// <c>null</c> takes the default of Ren et al. (2019): averaging window 3, judgement window 40,
/// threshold 3, z-score gate 1.5, 70% band, one batch.
/// </summary>
public sealed record SpectralResidualOptions
{
    /// <summary>Moving-average width on the log amplitude spectrum (q, at least 1).</summary>
    public int? AveragingWindow { get; init; }

    /// <summary>Preceding saliencies a point is scored against (z, at least 1).</summary>
    public int? JudgementWindow { get; init; }

    /// <summary>Score above which a point is an anomaly (τ, greater than 0).</summary>
    public double? Threshold { get; init; }

    /// <summary>Minimum z-score of the point against the window before it (0 disables the gate).</summary>
    public double? MinZscore { get; init; }

    /// <summary>Coverage, in percent, of the band around the expected value (between 0 and 100).</summary>
    public double? Sensitivity { get; init; }

    /// <summary>Score in consecutive batches of this size (at least 12), or <c>null</c> for one batch.</summary>
    public int? BatchSize { get; init; }
}

/// <summary>One scored observation.</summary>
public sealed record SeriesAnomalyPoint
{
    /// <summary>Position in the input series.</summary>
    public int Index { get; init; }

    /// <summary>The observed value.</summary>
    public double Value { get; init; }

    /// <summary>Spectral residual saliency (non-negative).</summary>
    public double Saliency { get; init; }

    /// <summary>Saliency relative to the preceding judgement window (non-negative); the point is an
    /// anomaly when this exceeds the threshold and the z-score gate passes.</summary>
    public double Score { get; init; }

    /// <summary>Low-frequency reconstruction of the series with anomalies removed.</summary>
    public double Expected { get; init; }

    /// <summary>Expected minus the band margin.</summary>
    public double Lower { get; init; }

    /// <summary>Expected plus the band margin.</summary>
    public double Upper { get; init; }

    /// <summary>Whether the point is an anomaly.</summary>
    public bool IsAnomaly { get; init; }

    /// <summary>
    /// Whether the point lies within a few places of an end of its batch, where the transform's
    /// boundary handling moves the saliency most. A position, not a verdict: a real anomaly there is
    /// still <see cref="IsAnomaly"/>, but a flag that appears only at an end is worth a second look.
    /// </summary>
    public bool NearEdge { get; init; }
}

/// <summary>Result of <see cref="Analyzers.TimeSeriesAnalyzer.SpectralResidual"/>.</summary>
public sealed record SeriesAnomalyReport
{
    /// <summary>One point per observation, in order.</summary>
    public IReadOnlyList<SeriesAnomalyPoint> Points { get; init; } = [];

    /// <summary>Indices of the points flagged as anomalies.</summary>
    public IReadOnlyList<int> Anomalies { get; init; } = [];
}

/// <summary>
/// The autocorrelation and partial autocorrelation functions of a univariate series, with the
/// bands a correlogram draws. See <see cref="Analyzers.TimeSeriesAnalyzer.Autocorrelation"/>.
/// </summary>
public sealed record SeriesAutocorrelation
{
    /// <summary>Number of observations the functions were estimated from.</summary>
    public int SampleSize { get; init; }

    /// <summary>Autocorrelation at lags 0 to the largest lag (the biased estimator);
    /// <c>Acf[0]</c> is 1.</summary>
    public IReadOnlyList<double> Acf { get; init; } = [];

    /// <summary>The 95% white-noise bound on the autocorrelation, <c>1.96 / sqrt(n)</c>.</summary>
    public double AcfThreshold { get; init; }

    /// <summary>Bartlett's 95% limit at lags 1 to the largest lag: the band under the hypothesis
    /// that the autocorrelation is zero beyond the lag before it (an MA(k − 1) process) — what to
    /// read to choose a moving-average order.</summary>
    public IReadOnlyList<double> AcfBartlettLimits { get; init; } = [];

    /// <summary>Partial autocorrelation at lags 1 to its largest lag — what to read to choose an
    /// autoregressive order. It stops short of the autocorrelation's range: its largest lag is
    /// below half the sample size.</summary>
    public IReadOnlyList<double> Pacf { get; init; } = [];

    /// <summary>The 95% white-noise bound on the partial autocorrelation, <c>1.96 / sqrt(n)</c>.</summary>
    public double PacfThreshold { get; init; }
}

/// <summary>
/// Two tests that disagree by design: the augmented Dickey-Fuller test takes a unit root as its null,
/// KPSS takes stationarity as its null. A series both reject, or neither rejects, is not settled by
/// either; which differencing to apply is the caller's policy. See
/// <see cref="Analyzers.TimeSeriesAnalyzer.Stationarity"/>.
/// </summary>
public sealed record SeriesStationarity
{
    /// <summary>Number of observations tested.</summary>
    public int SampleSize { get; init; }

    /// <summary>Whether the deterministic terms were a constant and a linear trend (<c>true</c>) or a
    /// constant alone (<c>false</c>) — the same choice for both tests.</summary>
    public bool AroundTrend { get; init; }

    /// <summary>The augmented Dickey-Fuller test (H0: the series has a unit root).</summary>
    public UnitRootTest Adf { get; init; } = new();

    /// <summary>The KPSS test (H0: the series is stationary around a level or a trend).</summary>
    public StationarityTest Kpss { get; init; } = new();
}

/// <summary>A test statistic read against the critical value at one significance level.</summary>
public sealed record TestLevel
{
    /// <summary>The significance level (0.01, 0.025, 0.05 or 0.10).</summary>
    public double Level { get; init; }

    /// <summary>The critical value at that level.</summary>
    public double CriticalValue { get; init; }

    /// <summary>Whether the null hypothesis is rejected at that level.</summary>
    public bool Rejected { get; init; }
}

/// <summary>The augmented Dickey-Fuller test of a unit root (H0). The null is rejected — the series
/// looks stationary — when the statistic is at or below the critical value.</summary>
public sealed record UnitRootTest
{
    /// <summary>The t-ratio of the lagged level.</summary>
    public double Statistic { get; init; }

    /// <summary>Lagged differences in the regression (chosen by AIC).</summary>
    public int Lags { get; init; }

    /// <summary>Observations the regression used.</summary>
    public int Observations { get; init; }

    /// <summary>The 1%, 5% and 10% levels, in that order.</summary>
    public IReadOnlyList<TestLevel> Levels { get; init; } = [];
}

/// <summary>The KPSS test of stationarity (H0). The null is rejected — the series looks
/// non-stationary — when the statistic is above the critical value.</summary>
public sealed record StationarityTest
{
    /// <summary>The KPSS statistic.</summary>
    public double Statistic { get; init; }

    /// <summary>The truncation lag of the long-run variance.</summary>
    public int Lags { get; init; }

    /// <summary>The p-value interpolated in the table of critical values, so it lies in
    /// [0.01, 0.10]; <see cref="PValueIsBound"/> says when it is the table's edge.</summary>
    public double PValue { get; init; }

    /// <summary><c>"at-most"</c> when the statistic is beyond the 1% critical value (the p-value is at
    /// most 0.01), <c>"at-least"</c> when it is below the 10% one (at least 0.10), otherwise
    /// <c>"interpolated"</c>.</summary>
    public string PValueBound { get; init; } = "interpolated";

    /// <summary>Whether <see cref="PValue"/> is a bound of the table rather than an interpolated value.</summary>
    public bool PValueIsBound => PValueBound != "interpolated";

    /// <summary>The 1%, 2.5%, 5% and 10% levels, in that order.</summary>
    public IReadOnlyList<TestLevel> Levels { get; init; } = [];
}
