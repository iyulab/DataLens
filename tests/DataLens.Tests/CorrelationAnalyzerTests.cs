using DataLens.Adapters;
using DataLens.Analyzers;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class CorrelationAnalyzerTests
{
    [Fact]
    public async Task Correlation_DetectsHighCorrelationPairs()
    {
        // X와 Y는 강한 양의 상관
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 50; i++)
        {
            data.Add(new Dictionary<string, string>
            {
                ["X"] = i.ToString(),
                ["Y"] = (i * 2 + 1).ToString(),
                ["Z"] = ((i % 7) * 3).ToString()
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions { CorrelationThreshold = 0.7 };

        var analyzer = new CorrelationAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, options);

        Assert.NotNull(report.Matrix);
        Assert.True(report.HighCorrelationPairs.Count > 0);

        var xyPair = report.HighCorrelationPairs.FirstOrDefault(
            p => (p.Column1 == "X" && p.Column2 == "Y") ||
                 (p.Column1 == "Y" && p.Column2 == "X"));
        Assert.NotNull(xyPair);
        Assert.True(xyPair.AbsValue > 0.99);
    }

    [Fact]
    public async Task Correlation_HandlesSingleColumn()
    {
        var data = new List<Dictionary<string, string>>
        {
            new() { ["X"] = "1" },
            new() { ["X"] = "2" },
        };
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var analyzer = new CorrelationAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, AnalysisOptions.Default);

        // 단일 컬럼이면 상관 행렬을 만들 수 없음
        Assert.Null(report.Matrix);
    }
}
