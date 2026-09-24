using DataLens.Analyzers;
using DataLens.Models;

namespace DataLens.Tests;

public class TimeSeriesAnalyzerTests
{
    private static double[] Sawtooth(int n, int period) =>
        Enumerable.Range(0, n).Select(i => (double)(i % period)).ToArray();

    private static double[] NoisySine(int n, int period, int seed)
    {
        var rng = new Random(seed);
        return Enumerable.Range(0, n)
            .Select(i => 10 * Math.Sin(2 * Math.PI * i / period) + rng.NextDouble())
            .ToArray();
    }

    [Fact]
    public void EstimatePeriod_finds_the_period_of_a_short_sawtooth()
    {
        // 40 rows of a perfect period-7 sawtooth: a case where a spectral-only estimate misses,
        // because the peak falls between two periodogram bins.
        var result = TimeSeriesAnalyzer.EstimatePeriod(Sawtooth(40, 7));

        Assert.Equal(7, result.Period);
        Assert.Equal(40, result.SampleSize);
        Assert.Contains(result.Candidates, c => c.Period == 7);
    }

    [Fact]
    public void EstimatePeriod_finds_the_period_of_a_noisy_sine()
    {
        Assert.Equal(24, TimeSeriesAnalyzer.EstimatePeriod(NoisySine(240, 24, 3)).Period);
    }

    [Fact]
    public void EstimatePeriod_reports_no_period_for_a_straight_line()
    {
        var line = Enumerable.Range(0, 40).Select(i => (double)i).ToArray();

        var result = TimeSeriesAnalyzer.EstimatePeriod(line);

        Assert.Null(result.Period);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void SpectralResidual_flags_a_spike_on_a_seasonal_series_and_nothing_else()
    {
        var series = Sawtooth(84, 7);
        series[50] += 15;

        var report = TimeSeriesAnalyzer.SpectralResidual(series);

        Assert.Equal([50], report.Anomalies);
        Assert.Equal(84, report.Points.Count);
        Assert.True(report.Points[50].IsAnomaly);
        Assert.Equal(series[50], report.Points[50].Value);
    }

    [Fact]
    public void SpectralResidual_flags_a_spike_on_a_noisy_sine()
    {
        var series = NoisySine(240, 24, 3);
        series[150] += 12;

        Assert.Equal([150], TimeSeriesAnalyzer.SpectralResidual(series).Anomalies);
    }

    [Fact]
    public void SpectralResidual_flags_the_start_of_a_step()
    {
        var rng = new Random(3);
        var series = Enumerable.Range(0, 120).Select(i => (i < 60 ? 1.0 : 6.0) + rng.NextDouble() * 0.2).ToArray();

        var anomalies = TimeSeriesAnalyzer.SpectralResidual(series).Anomalies;

        // The saliency of an edge spreads to its neighbour, so the point before the step can be
        // flagged with it depending on the noise; what holds is that the step is found and nothing
        // away from it is.
        Assert.Contains(60, anomalies);
        Assert.All(anomalies, i => Assert.InRange(i, 59, 61));
    }

    [Fact]
    public void SpectralResidual_band_surrounds_the_expected_value()
    {
        var report = TimeSeriesAnalyzer.SpectralResidual(NoisySine(120, 24, 5));

        Assert.All(report.Points, p => Assert.True(p.Lower <= p.Expected && p.Expected <= p.Upper));
    }

    [Fact]
    public void An_option_left_unset_keeps_the_engine_default()
    {
        // Setting only the threshold must not zero the other options: the result matches the
        // all-default run on a series whose one anomaly scores far above both thresholds.
        var series = Sawtooth(84, 7);
        series[50] += 15;

        var defaults = TimeSeriesAnalyzer.SpectralResidual(series);
        var partial = TimeSeriesAnalyzer.SpectralResidual(series, new SpectralResidualOptions { Threshold = 3.0 });

        Assert.Equal(defaults.Anomalies, partial.Anomalies);
        Assert.Equal(defaults.Points.Select(p => p.Upper), partial.Points.Select(p => p.Upper));
    }

    [Fact]
    public void A_higher_threshold_flags_no_more_points()
    {
        var series = NoisySine(240, 24, 3);
        series[150] += 12;

        var strict = TimeSeriesAnalyzer.SpectralResidual(series, new SpectralResidualOptions { Threshold = 1000 });

        Assert.Empty(strict.Anomalies);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    public void Too_short_a_series_is_refused(int length)
    {
        Assert.Throws<ArgumentException>(() => TimeSeriesAnalyzer.EstimatePeriod(new double[length]));
        Assert.Throws<ArgumentException>(() => TimeSeriesAnalyzer.SpectralResidual(new double[length + 4]));
    }

    [Fact]
    public void A_non_finite_value_is_refused_with_its_position()
    {
        var series = Sawtooth(40, 7);
        series[12] = double.NaN;

        var ex = Assert.Throws<ArgumentException>(() => TimeSeriesAnalyzer.SpectralResidual(series));
        Assert.Contains("12", ex.Message);
    }

    public static TheoryData<SpectralResidualOptions, string> InvalidOptions => new()
    {
        { new SpectralResidualOptions { Threshold = 0 }, "Threshold" },
        { new SpectralResidualOptions { Sensitivity = 100 }, "Sensitivity" },
        { new SpectralResidualOptions { Sensitivity = 0 }, "Sensitivity" },
        { new SpectralResidualOptions { BatchSize = 5 }, "BatchSize" },
        { new SpectralResidualOptions { AveragingWindow = 0 }, "AveragingWindow" },
        { new SpectralResidualOptions { MinZscore = -1 }, "MinZscore" },
        { new SpectralResidualOptions { JudgementWindow = 0 }, "JudgementWindow" },
        { new SpectralResidualOptions { BatchSize = 0 }, "BatchSize" },
        { new SpectralResidualOptions { AveragingWindow = -1 }, "AveragingWindow" },
        { new SpectralResidualOptions { BatchSize = -12 }, "BatchSize" },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void An_invalid_option_is_refused_by_name(SpectralResidualOptions options, string name)
    {
        var ex = Assert.Throws<ArgumentException>(() => TimeSeriesAnalyzer.SpectralResidual(Sawtooth(40, 7), options));
        Assert.StartsWith(name + " ", ex.Message);
        Assert.Equal("options", ex.ParamName);
    }

    /// <summary>
    /// The rule in the message is the engine's, not a copy kept here: the out-of-range case carries
    /// the engine's exception, and the text after the option's name is the engine's requirement.
    /// </summary>
    [Fact]
    public void An_invalid_option_carries_the_engine_rule()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TimeSeriesAnalyzer.SpectralResidual(Sawtooth(40, 7), new SpectralResidualOptions { Threshold = 0 }));
        var engine = Assert.IsType<UInsight.InsightException>(ex.InnerException);
        Assert.Equal("threshold", engine.Parameter);
        Assert.Equal("Threshold must be a finite number > 0. (Parameter 'options')", ex.Message);
    }
}
