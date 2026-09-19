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
}

/// <summary>Result of <see cref="Analyzers.TimeSeriesAnalyzer.SpectralResidual"/>.</summary>
public sealed record SeriesAnomalyReport
{
    /// <summary>One point per observation, in order.</summary>
    public IReadOnlyList<SeriesAnomalyPoint> Points { get; init; } = [];

    /// <summary>Indices of the points flagged as anomalies.</summary>
    public IReadOnlyList<int> Anomalies { get; init; } = [];
}
