using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

/// <summary>
/// Local outlier factor and PELT change points are quadratic in the rows (four numeric columns: LOF 5 s at 20,000 rows and 32 s at 50,000,
/// PELT 1.4 s and 10 s), and both ran by default - an analysis of a large file took minutes to hours for results nobody asked for. Above a
/// row threshold they are skipped and the report says so; a threshold of 0 keeps the old, unconditional run. The thresholds here are small so
/// that the tests stay cheap.
/// </summary>
public class RowThresholdGuardTests
{
    private static DataAdapter Adapter(int rows)
    {
        var rng = new Random(11);
        var data = Enumerable.Range(0, rows)
            .Select(_ => new Dictionary<string, string>
            {
                ["A"] = (rng.NextDouble() * 10).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ["B"] = (rng.NextDouble() * 10).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ["C"] = (rng.NextDouble() * 10).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            }).ToList();
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    [Fact]
    public void TheDefaults_AreTwentyThousandRows()
    {
        var options = new AnalysisOptions();

        Assert.Equal(20_000u, options.LofRowThreshold);
        Assert.Equal(20_000u, options.ChangepointRowThreshold);
    }

    [Fact]
    public async Task Lof_IsSkippedAboveTheThreshold_AndTheReportSaysSo()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { LofRowThreshold = 200 }, warnings);

        Assert.Null(report.Lof);
        Assert.NotNull(report.IsolationForest);
        Assert.NotNull(report.Mahalanobis);
        var skipped = Assert.Single(warnings, w => w.Category == WarningCategory.SkippedForSize);
        Assert.Equal("Lof", skipped.Analyzer);
        Assert.Contains("LofRowThreshold", skipped.Message);
    }

    [Theory]
    [InlineData(300u)]     // at or over the row count
    [InlineData(0u)]       // no threshold
    public async Task Lof_RunsAtOrBelowTheThreshold_AndWhenThereIsNone(uint threshold)
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { LofRowThreshold = threshold }, warnings);

        Assert.NotNull(report.Lof);
        Assert.Equal(300, report.Lof!.Scores.Length);
        Assert.DoesNotContain(warnings, w => w.Category == WarningCategory.SkippedForSize);
    }

    // With LOF out, a row is an ensemble outlier when the other two both say so.
    [Fact]
    public async Task WithoutLof_TheEnsembleIsTheOtherTwoAgreeing()
    {
        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { LofRowThreshold = 200 });

        var both = Enumerable.Range(0, 300).Count(i => report.IsolationForest!.Anomalies[i] && report.Mahalanobis!.Anomalies[i]);
        Assert.Equal(both, report.OutlierCount);
    }

    [Fact]
    public async Task Changepoints_AreSkippedAboveTheThreshold_AndTheReportSaysSo()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ChangepointAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { ChangepointRowThreshold = 200 }, warnings);

        Assert.Empty(report.Columns);
        var skipped = Assert.Single(warnings, w => w.Category == WarningCategory.SkippedForSize);
        Assert.Equal("Changepoints", skipped.Analyzer);
        Assert.Contains("ChangepointRowThreshold", skipped.Message);
    }

    [Theory]
    [InlineData(300u)]
    [InlineData(0u)]
    public async Task Changepoints_RunAtOrBelowTheThreshold_AndWhenThereIsNone(uint threshold)
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ChangepointAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { ChangepointRowThreshold = threshold }, warnings);

        Assert.NotEmpty(report.Columns);
        Assert.DoesNotContain(warnings, w => w.Category == WarningCategory.SkippedForSize);
    }

    [Fact]
    public async Task TheEngine_ReportsBothSkips_OnALargeFrame()
    {
        var df = Adapter(300).DataFrame;
        var options = new AnalysisOptions
        {
            IncludeProfiling = false,
            IncludeDescriptive = false,
            IncludeCorrelation = false,
            IncludeRegression = false,
            IncludeClustering = false,
            IncludeDistribution = false,
            IncludeFeatures = false,
            IncludePca = false,
            IncludeOutliers = true,
            IncludeChangepoints = true,
            LofRowThreshold = 200,
            ChangepointRowThreshold = 200,
        };

        var result = await DataLensEngine.Analyze(df, options);

        Assert.Null(result.Outliers!.Lof);
        Assert.Equal(["Changepoints", "Lof"], result.Warnings.Where(w => w.Category == WarningCategory.SkippedForSize).Select(w => w.Analyzer).Order());
    }
}
