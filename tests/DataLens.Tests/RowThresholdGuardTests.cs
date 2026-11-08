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
    private static DataAdapter Adapter(int rows, int columns = 3)
    {
        var rng = new Random(11);
        var data = Enumerable.Range(0, rows)
            .Select(_ => Enumerable.Range(0, columns).ToDictionary(
                c => ((char)('A' + c)).ToString(),
                _ => (rng.NextDouble() * 10).ToString("R", System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    [Fact]
    public void TheDefaults_AreTheMeasuredCostBounds()
    {
        var options = new AnalysisOptions();

        // 100,000 rows of four numeric columns: the largest narrow table the old row threshold let through.
        Assert.Equal(100_000UL * 100_000UL * 4UL, options.LofWorkThreshold);
        Assert.Equal(20_000u, options.ChangepointRowThreshold);
    }

    [Fact]
    public async Task Lof_IsSkippedAboveTheThreshold_AndTheReportSaysSo()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { LofWorkThreshold = 200_000 }, warnings);

        Assert.Null(report.Lof);
        Assert.NotNull(report.IsolationForest);
        Assert.NotNull(report.Mahalanobis);
        var skipped = Assert.Single(warnings, w => w.Category == WarningCategory.SkippedForSize);
        Assert.Equal("Lof", skipped.Analyzer);
        Assert.Contains("LofWorkThreshold", skipped.Message);
    }

    [Theory]
    [InlineData(270_000UL)]  // 300 rows x 3 columns: at the work
    [InlineData(0UL)]        // no threshold
    public async Task Lof_RunsAtOrBelowTheThreshold_AndWhenThereIsNone(ulong threshold)
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { LofWorkThreshold = threshold }, warnings);

        Assert.NotNull(report.Lof);
        Assert.Equal(300, report.Lof!.Scores.Length);
        Assert.DoesNotContain(warnings, w => w.Category == WarningCategory.SkippedForSize);
    }

    // The same rows are cheap when narrow and skipped when wide: the work is rows squared times columns, so a row threshold alone lets a
    // 20,000-row, 200-column table through (184 s measured) while a 20,000-row, 4-column one takes 5 s.
    [Fact]
    public async Task Lof_CountsTheColumns_NotOnlyTheRows()
    {
        var threshold = new AnalysisOptions { LofWorkThreshold = 300_000 };   // 300 rows: 3 columns = 270,000 (runs), 6 columns = 540,000 (skipped)
        var narrow = new List<AnalysisWarning>();
        var wide = new List<AnalysisWarning>();

        var narrowReport = await new OutlierAnalyzer().AnalyzeAsync(Adapter(300, columns: 3), threshold, narrow);
        var wideReport = await new OutlierAnalyzer().AnalyzeAsync(Adapter(300, columns: 6), threshold, wide);

        Assert.NotNull(narrowReport.Lof);
        Assert.DoesNotContain(narrow, w => w.Category == WarningCategory.SkippedForSize);
        Assert.Null(wideReport.Lof);
        Assert.Single(wide, w => w.Category == WarningCategory.SkippedForSize && w.Analyzer == "Lof");
    }

    // With LOF out, a row is an ensemble outlier when the other two both say so.
    [Fact]
    public async Task WithoutLof_TheEnsembleIsTheOtherTwoAgreeing()
    {
        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { LofWorkThreshold = 200_000 });

        var both = Enumerable.Range(0, 300).Count(i => report.IsolationForest!.Anomalies[i] && report.Mahalanobis!.Anomalies[i]);
        Assert.Equal(both, report.OutlierCount);
    }

    [Fact]
    public async Task AboveTheThreshold_L2ColumnsStillRun_AndTheMultivariateRunIsSkipped()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ChangepointAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions { ChangepointRowThreshold = 200 }, warnings);

        // Per-column L2 PELT is near-linear: no threshold.
        Assert.NotEmpty(report.Columns);
        Assert.Null(report.Multivariate);
        var skipped = Assert.Single(warnings, w => w.Category == WarningCategory.SkippedForSize);
        Assert.Equal("Changepoints", skipped.Analyzer);
        Assert.Contains("ChangepointRowThreshold", skipped.Message);
    }

    [Fact]
    public async Task AboveTheThreshold_TheNormalCostSkipsBothRuns_AndSaysSo()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ChangepointAnalyzer().AnalyzeAsync(
            Adapter(300), new AnalysisOptions { ChangepointRowThreshold = 200, ChangepointCost = ChangepointCost.Normal }, warnings);

        Assert.Empty(report.Columns);
        Assert.Null(report.Multivariate);
        Assert.Equal(2, warnings.Count(w => w.Category == WarningCategory.SkippedForSize && w.Analyzer == "Changepoints"));
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
            LofWorkThreshold = 200_000,
            ChangepointRowThreshold = 200,
        };

        var result = await DataLensEngine.Analyze(df, options);

        Assert.Null(result.Outliers!.Lof);
        Assert.Equal(["Changepoints", "Lof"], result.Warnings.Where(w => w.Category == WarningCategory.SkippedForSize).Select(w => w.Analyzer).Order());
    }

    [Fact]
    public async Task Hdbscan_AndHierarchical_AreSkippedAboveTheirThresholds_AndSaySo()
    {
        var warnings = new List<AnalysisWarning>();
        var options = new AnalysisOptions { HdbscanRowThreshold = 200, HierarchicalRowThreshold = 200 };

        var report = await new ClusterAnalyzer().AnalyzeAsync(Adapter(300), options, warnings);

        Assert.Null(report.Hdbscan);
        Assert.Null(report.Hierarchical);
        var skipped = warnings.Where(w => w.Category == WarningCategory.SkippedForSize).Select(w => w.Analyzer).OrderBy(a => a).ToArray();
        Assert.Equal(["HDBSCAN", "Hierarchical"], skipped);
        Assert.Contains(warnings, w => w.Message.Contains(nameof(AnalysisOptions.HdbscanRowThreshold)));
    }

    [Fact]
    public async Task Hdbscan_AndHierarchical_RunAtOrBelowTheirThresholds()
    {
        var warnings = new List<AnalysisWarning>();
        var report = await new ClusterAnalyzer().AnalyzeAsync(Adapter(300), new AnalysisOptions(), warnings);

        Assert.NotNull(report.Hdbscan);
        Assert.NotNull(report.Hierarchical);
        Assert.DoesNotContain(warnings, w => w.Category == WarningCategory.SkippedForSize);
        Assert.Equal(20_000u, new AnalysisOptions().HdbscanRowThreshold);
        Assert.Equal(10_000u, new AnalysisOptions().HierarchicalRowThreshold);
    }
}
