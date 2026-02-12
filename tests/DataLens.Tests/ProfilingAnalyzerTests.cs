using DataLens.Adapters;
using DataLens.Analyzers;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class ProfilingAnalyzerTests
{
    [Fact]
    public async Task Profile_ReturnsCorrectRowAndColumnCount()
    {
        var data = new List<Dictionary<string, string>>
        {
            new() { ["A"] = "1", ["B"] = "hello", ["C"] = "3.14" },
            new() { ["A"] = "2", ["B"] = "world", ["C"] = "2.72" },
            new() { ["A"] = "3", ["B"] = "", ["C"] = "1.62" },
        };
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var analyzer = new ProfilingAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.Equal(3, report.RowCount);
        Assert.Equal(3, report.ColumnCount);
        Assert.Equal(3, report.Columns.Count);
    }
}
