using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

/// <summary>
/// The row thresholds guard an <c>n²</c> cost that grows with the columns as well: a 20,000-row table of 200 numeric columns ran DBSCAN 65 s,
/// the silhouette 64 s, HDBSCAN 4 min, the multivariate change points 6 min and the gap statistic 2 min (single thread), all under the row
/// thresholds that were set for a handful of columns - <c>mloop info --analyze</c> took minutes to hours (the local outlier factor, 184 s,
/// is guarded by the same threshold). Past <see cref="AnalysisOptions.PairwiseWorkThreshold"/> (rows² × columns, with more than
/// 7 columns, or 12 for the local outlier factor - fewer are answered by a k-d tree) the all-pairs analyses are skipped and the report says so; the gap
/// statistic reads at most <see cref="AnalysisOptions.GapStatisticMaxRows"/> evenly spaced rows.
/// </summary>
public class PairwiseWorkGuardTests
{
    private static DataAdapter Adapter(int rows, int columns)
    {
        var rng = new Random(13);
        var data = Enumerable.Range(0, rows)
            .Select(_ => Enumerable.Range(0, columns).ToDictionary(
                c => $"C{c}",
                _ => (rng.NextDouble() * 10).ToString("R", System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    [Fact]
    public void TheDefaults_AreTwentyBillionCellPairsAndFiveThousandGapRows()
    {
        var options = new AnalysisOptions();

        Assert.Equal(20_000_000_000ul, options.PairwiseWorkThreshold);
        Assert.Equal(5_000u, options.GapStatisticMaxRows);
    }

    // The local outlier factor is one of the all-pairs analyses: the same threshold, the same 7-column exemption.
    [Fact]
    public async Task Lof_IsGuardedByThePairwiseThreshold_OnAWideTable()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(200, 20), new AnalysisOptions { PairwiseWorkThreshold = 1 }, warnings);

        Assert.Null(report.Lof);
        Assert.NotNull(report.IsolationForest);
        var skipped = Assert.Single(warnings, w => w.Category == WarningCategory.SkippedForSize);
        Assert.Equal("Lof", skipped.Analyzer);
        Assert.Contains("PairwiseWorkThreshold", skipped.Message);
    }

    // Measured on 100,000 rows (UInsight 0.43.1): the local outlier factor takes 7 s at 7 and 8 columns, 16 s at 10, 26 s at 12 and 36 s at 14 -
    // the cost of the old boundary (28 s at 7 columns) is reached at 12.
    [Theory]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(12, false)]
    [InlineData(13, true)]
    public async Task TheLofTreeExemption_EndsAfterTwelveColumns(int columns, bool skipped)
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(200, columns), new AnalysisOptions { PairwiseWorkThreshold = 1 }, warnings);

        Assert.Equal(skipped, report.Lof is null);
        Assert.Equal(skipped, warnings.Any(w => w.Category == WarningCategory.SkippedForSize && w.Analyzer == "Lof"));
    }

    // DBSCAN grows with the neighbours a point has, not with the columns (89 s at 8 columns on 100,000 standardized rows, 146 s at 7), so its
    // exemption stays at 7 columns.
    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    public async Task TheOtherAnalysesKeepTheirTreeExemptionAtSevenColumns(int columns, bool skipped)
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ClusterAnalyzer().AnalyzeAsync(Adapter(200, columns), new AnalysisOptions { PairwiseWorkThreshold = 1 }, warnings);

        Assert.Equal(skipped, report.Dbscan is null);
        Assert.Equal(skipped, warnings.Any(w => w.Category == WarningCategory.SkippedForSize && w.Analyzer == "DBSCAN"));
    }

    [Fact]
    public async Task Lof_RunsOnANarrowTable_WhateverTheThreshold()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new OutlierAnalyzer().AnalyzeAsync(Adapter(200, 4), new AnalysisOptions { PairwiseWorkThreshold = 1 }, warnings);

        Assert.NotNull(report.Lof);
        Assert.DoesNotContain(warnings, w => w.Category == WarningCategory.SkippedForSize);
    }

    [Fact]
    public async Task DbscanHdbscanAndTheSilhouette_AreSkippedOnAWideTable_AndSayWhich()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ClusterAnalyzer().AnalyzeAsync(Adapter(200, 20), new AnalysisOptions { PairwiseWorkThreshold = 500_000 }, warnings);

        Assert.Null(report.Dbscan);
        Assert.Null(report.Hdbscan);
        Assert.NotNull(report.KMeans);
        Assert.Null(report.KMeans!.SilhouetteAvg);
        var skipped = warnings.Where(w => w.Category == WarningCategory.SkippedForSize).Select(w => w.Analyzer).Order().ToArray();
        Assert.Equal(["DBSCAN", "HDBSCAN", "Silhouette"], skipped);
    }

    [Fact]
    public async Task TheMultivariateChangepoints_AreSkippedOnAWideTable_AndTheColumnWiseOnesRun()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ChangepointAnalyzer().AnalyzeAsync(Adapter(200, 20), new AnalysisOptions { PairwiseWorkThreshold = 500_000 }, warnings);

        Assert.Null(report.Multivariate);
        Assert.NotEmpty(report.Columns);
        var skipped = Assert.Single(warnings, w => w.Category == WarningCategory.SkippedForSize);
        Assert.Equal("Changepoints", skipped.Analyzer);
        Assert.Contains("다변량", skipped.Message);
        Assert.Contains("PairwiseWorkThreshold", skipped.Message);
    }

    [Fact]
    public async Task TheMultivariateChangepoints_RunWithinTheThreshold()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ChangepointAnalyzer().AnalyzeAsync(Adapter(200, 20), new AnalysisOptions { PairwiseWorkThreshold = 0 }, warnings);

        Assert.NotNull(report.Multivariate);
        Assert.DoesNotContain(warnings, w => w.Category == WarningCategory.SkippedForSize);
    }

    [Fact]
    public async Task TheGapStatistic_ReadsASampleOfAnOverlongTable_AndSaysSo()
    {
        var warnings = new List<AnalysisWarning>();

        var report = await new ClusterAnalyzer().AnalyzeAsync(
            Adapter(300, 3),
            new AnalysisOptions { GapStatisticMaxRows = 100, MaxClusters = 4 },
            warnings);

        Assert.NotNull(report.KMeans);
        var sampled = Assert.Single(warnings, w => w.Analyzer == "GapStatistic");
        Assert.Equal(WarningCategory.SkippedForSize, sampled.Category);
        Assert.Contains("GapStatisticMaxRows", sampled.Message);
        Assert.Contains("100", sampled.Message);
    }

    [Theory]
    [InlineData(300u)]   // at the row count
    [InlineData(0u)]     // no cap
    public async Task TheGapStatistic_ReadsEveryRow_WithinTheCapOrWithoutOne(uint cap)
    {
        var warnings = new List<AnalysisWarning>();

        await new ClusterAnalyzer().AnalyzeAsync(Adapter(300, 3), new AnalysisOptions { GapStatisticMaxRows = cap, MaxClusters = 4 }, warnings);

        Assert.DoesNotContain(warnings, w => w.Analyzer == "GapStatistic");
    }
}
