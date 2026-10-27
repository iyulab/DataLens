using DataLens.Analyzers;
using UInsight;

namespace DataLens.Tests;

public class SeriesStatisticsTests
{
    private static double[] Ar1(int n, double phi, int seed)
    {
        var rng = new Random(seed);
        var x = new double[n];
        for (var i = 1; i < n; i++)
            x[i] = phi * x[i - 1] + (rng.NextDouble() - 0.5);
        return x;
    }

    private static double[] WhiteNoise(int n, int seed)
    {
        var rng = new Random(seed);
        return Enumerable.Range(0, n).Select(_ => rng.NextDouble() - 0.5).ToArray();
    }

    private static double[] RandomWalk(int n, int seed)
    {
        var rng = new Random(seed);
        var x = new double[n];
        for (var i = 1; i < n; i++)
            x[i] = x[i - 1] + (rng.NextDouble() - 0.5);
        return x;
    }

    private static double[] TrendPlusNoise(int n, int seed)
    {
        var rng = new Random(seed);
        return Enumerable.Range(0, n).Select(i => 0.2 * i + rng.NextDouble() - 0.5).ToArray();
    }

    [Fact]
    public void Autocorrelation_of_an_AR1_series_decays_and_cuts_off_the_partial()
    {
        var result = TimeSeriesAnalyzer.Autocorrelation(Ar1(400, 0.8, 5), maxLag: 10);

        Assert.Equal(400, result.SampleSize);
        Assert.Equal(1.0, result.Acf[0]);
        Assert.Equal(11, result.Acf.Count);            // lags 0..10
        Assert.Equal(10, result.AcfBartlettLimits.Count);   // lags 1..10
        Assert.Equal(10, result.Pacf.Count);           // lags 1..10
        Assert.InRange(result.Acf[1], 0.7, 0.9);
        Assert.True(result.Acf[1] > result.Acf[3] && result.Acf[3] > result.Acf[6]);
        // An AR(1) shows in the partial autocorrelation as one spike: lag 1 outside the band, lag 2 within.
        Assert.True(Math.Abs(result.Pacf[0]) > result.PacfThreshold);
        Assert.True(Math.Abs(result.Pacf[1]) < 2.5 * result.PacfThreshold);
        Assert.Equal(1.96 / Math.Sqrt(400), result.AcfThreshold, 4);
        Assert.Equal(1.96 / Math.Sqrt(400), result.PacfThreshold, 4);
    }

    [Fact]
    public void Autocorrelation_of_a_seasonal_series_peaks_at_the_period()
    {
        var series = Enumerable.Range(0, 240).Select(i => Math.Sin(2 * Math.PI * i / 12)).ToArray();

        var acf = TimeSeriesAnalyzer.Autocorrelation(series, maxLag: 30).Acf;

        Assert.True(acf[12] > acf[6] && acf[12] > acf[11] && acf[12] > acf[13]);
        Assert.True(acf[6] < 0);
    }

    [Fact]
    public void Autocorrelation_default_lag_is_the_engines_and_the_partial_stops_below_half_the_sample()
    {
        var result = TimeSeriesAnalyzer.Autocorrelation(Ar1(100, 0.5, 2));

        Assert.True(result.Acf.Count > 2);
        Assert.Equal(result.Acf.Count - 1, result.AcfBartlettLimits.Count);
        Assert.True(result.Pacf.Count <= 49);
    }

    [Fact]
    public void Autocorrelation_with_a_lag_past_what_the_partial_allows_still_answers_the_autocorrelation()
    {
        var result = TimeSeriesAnalyzer.Autocorrelation(Ar1(100, 0.5, 2), maxLag: 80);

        Assert.Equal(81, result.Acf.Count);
        Assert.Equal(49, result.Pacf.Count);
    }

    [Fact]
    public void Autocorrelation_rejects_a_lag_beyond_the_sample_naming_the_argument()
    {
        var ex = Assert.Throws<ArgumentException>(() => TimeSeriesAnalyzer.Autocorrelation(Ar1(100, 0.5, 2), maxLag: 200));

        Assert.Equal("maxLag", ex.ParamName);
        Assert.IsType<InsightException>(ex.InnerException);
    }

    [Fact]
    public void Autocorrelation_of_too_short_or_non_finite_input_is_an_argument_error()
    {
        Assert.Throws<ArgumentException>(() => TimeSeriesAnalyzer.Autocorrelation([1.0, 2.0, 3.0]));
        Assert.Throws<ArgumentException>(() => TimeSeriesAnalyzer.Autocorrelation([1.0, 2.0, double.NaN, 4.0, 5.0]));
    }

    [Fact]
    public void Autocorrelation_of_a_constant_series_is_the_engines_degenerate_error()
    {
        var ex = Assert.Throws<InsightException>(() => TimeSeriesAnalyzer.Autocorrelation(Enumerable.Repeat(3.0, 30).ToArray()));

        Assert.Equal(InsightErrorCategory.DegenerateData, ex.Category);
    }

    [Fact]
    public void Stationarity_of_white_noise_rejects_the_unit_root_and_keeps_stationarity()
    {
        var result = TimeSeriesAnalyzer.Stationarity(WhiteNoise(300, 4));

        Assert.Equal(300, result.SampleSize);
        Assert.False(result.AroundTrend);
        Assert.Equal([0.01, 0.05, 0.10], result.Adf.Levels.Select(l => l.Level));
        Assert.Equal([0.01, 0.025, 0.05, 0.10], result.Kpss.Levels.Select(l => l.Level));
        Assert.True(result.Adf.Levels[1].Rejected);        // ADF at 5 %: the unit root is rejected
        Assert.False(result.Kpss.Levels[2].Rejected);      // KPSS at 5 %: stationarity stands
        Assert.True(result.Adf.Statistic <= result.Adf.Levels[1].CriticalValue);
        Assert.True(result.Adf.Observations > 0 && result.Kpss.PValue is >= 0.01 and <= 0.10);
    }

    [Fact]
    public void Stationarity_of_a_random_walk_keeps_the_unit_root_and_rejects_stationarity()
    {
        var result = TimeSeriesAnalyzer.Stationarity(RandomWalk(400, 9));

        Assert.False(result.Adf.Levels[1].Rejected);
        Assert.True(result.Kpss.Levels[2].Rejected);
        Assert.True(result.Kpss.PValueIsBound);
        Assert.Equal("at-most", result.Kpss.PValueBound);
    }

    [Fact]
    public void Stationarity_around_a_trend_is_a_different_null_than_around_a_level()
    {
        var series = TrendPlusNoise(300, 3);

        var level = TimeSeriesAnalyzer.Stationarity(series, aroundTrend: false);
        var trend = TimeSeriesAnalyzer.Stationarity(series, aroundTrend: true);

        Assert.True(level.Kpss.Levels[2].Rejected);        // not stationary around a constant
        Assert.False(trend.Kpss.Levels[2].Rejected);       // stationary around the trend
        Assert.True(trend.AroundTrend);
        Assert.True(trend.Adf.Levels[1].Rejected);
    }

    [Fact]
    public void Stationarity_of_too_short_input_is_an_argument_error_and_of_a_constant_series_the_engines()
    {
        Assert.Throws<ArgumentException>(() => TimeSeriesAnalyzer.Stationarity(WhiteNoise(9, 1)));
        var ex = Assert.Throws<InsightException>(() => TimeSeriesAnalyzer.Stationarity(Enumerable.Repeat(1.0, 50).ToArray()));
        Assert.Equal(InsightErrorCategory.DegenerateData, ex.Category);
    }

    /// <summary>The README's example: a sawtooth of period 7 with one spike.</summary>
    [Fact]
    public void The_documented_example_runs_and_shows_the_period_in_the_correlogram()
    {
        double[] series = [.. Enumerable.Range(0, 84).Select(i => (double)(i % 7))];
        series[50] += 15;

        var correlogram = TimeSeriesAnalyzer.Autocorrelation(series, maxLag: 14);
        var tests = TimeSeriesAnalyzer.Stationarity(series);

        Assert.True(correlogram.Acf[7] > correlogram.AcfThreshold);   // 0.49 against a 0.21 band
        Assert.True(correlogram.Acf[7] > correlogram.Acf[6] && correlogram.Acf[7] > correlogram.Acf[8]);
        Assert.Equal(3, tests.Adf.Levels.Count);
        Assert.Equal(4, tests.Kpss.Levels.Count);
    }
}
