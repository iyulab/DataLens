using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;
using UInsight;

namespace DataLens.Tests;

public class ProfilingAnalyzerTests
{
    private static DataAdapter ValidAdapter()
    {
        var data = new List<Dictionary<string, string>>
        {
            new() { ["A"] = "1", ["B"] = "hello", ["C"] = "3.14" },
            new() { ["A"] = "2", ["B"] = "world", ["C"] = "2.72" },
            new() { ["A"] = "3", ["B"] = "", ["C"] = "1.62" },
        };
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    /// <summary>Injects a CSV string into the analyzer's profiling call (a normal DataAdapter always
    /// produces well-formed CSV, so this seam is how the upstream-failure path is exercised).</summary>
    private sealed class InjectedCsvAdapter(DataFrame df, string csv) : DataAdapter(df)
    {
        public override string ToCsvString() => csv;
    }

    private sealed class ThrowingAdapter(DataFrame df) : DataAdapter(df)
    {
        public override string ToCsvString() => throw new InvalidOperationException("non-Insight bug");
    }

    [Fact]
    public async Task Profile_ReturnsCorrectRowAndColumnCount()
    {
        var report = await new ProfilingAnalyzer().AnalyzeAsync(ValidAdapter(), AnalysisOptions.Default);

        Assert.Equal(3, report.RowCount);
        Assert.Equal(3, report.ColumnCount);
        Assert.Equal(3, report.Columns.Count);
    }

    [Fact]
    public async Task Profile_UpstreamProfilingFailure_SurfacesWarning_NotSilentEmpty()
    {
        // A ragged CSV makes UInsight's ProfileCsv throw InsightException. The old bare catch
        // swallowed this into an empty ProfileReport with no signal; it must now surface a warning.
        var df = DataPipeline.FromData(
            new List<Dictionary<string, string>> { new() { ["a"] = "1", ["b"] = "2" } }).ToDataFrame();
        var adapter = new InjectedCsvAdapter(df, "a,b\n1\n2,3,4");
        var warnings = new List<AnalysisWarning>();

        var report = await new ProfilingAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default, warnings);

        Assert.Empty(report.Columns); // still returns (pipeline continues)
        Assert.Contains(warnings, w =>
            w.Analyzer == "Profiling" && w.Category == WarningCategory.UpstreamError);
    }

    [Fact]
    public async Task Profile_NonInsightException_Propagates_NotSwallowed()
    {
        // A bug in this layer (not an upstream data issue) must not masquerade as an empty-but-successful
        // profile — the narrowed catch lets it propagate.
        var df = DataPipeline.FromData(
            new List<Dictionary<string, string>> { new() { ["a"] = "1" } }).ToDataFrame();
        var adapter = new ThrowingAdapter(df);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProfilingAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default, new List<AnalysisWarning>()));
    }
}
