using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
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

    [Fact]
    public async Task Correlation_AutoExcludesConstantColumnsAndComputesPartialMatrix()
    {
        // X/Y 는 정상 분산, C1/C2 는 분산 0 (상수 컬럼).
        // 기존 동작: UInsight 가 throw → Matrix=null + 영문 warning.
        // 새 동작: C1/C2 자동 제외 + 2x2 부분 매트릭스 + DegenerateColumnsExcluded warning.
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 50; i++)
        {
            data.Add(new Dictionary<string, string>
            {
                ["X"] = i.ToString(),
                ["Y"] = (i * 2 + 1).ToString(),
                ["C1"] = "5",
                ["C2"] = "0"
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var warnings = new List<AnalysisWarning>();

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default, warnings);

        Assert.NotNull(report.Matrix);
        Assert.Equal(2, report.Matrix!.GetLength(0));
        Assert.Equal(new[] { "X", "Y" }, report.ColumnNames);

        var degenerateWarning = warnings.SingleOrDefault(w => w.Category == WarningCategory.DegenerateColumnsExcluded);
        Assert.NotNull(degenerateWarning);
        Assert.Equal("Correlation", degenerateWarning!.Analyzer);
        Assert.NotNull(degenerateWarning.AffectedColumns);
        Assert.Contains("C1", degenerateWarning.AffectedColumns!);
        Assert.Contains("C2", degenerateWarning.AffectedColumns!);
    }

    [Fact]
    public async Task Correlation_EmitsInsufficientUsableColumnsWhenAllConstant()
    {
        // 모든 numeric 컬럼이 상수 → 부분 결과조차 불가.
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 50; i++)
        {
            data.Add(new Dictionary<string, string>
            {
                ["A"] = "5",
                ["B"] = "0"
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var warnings = new List<AnalysisWarning>();

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default, warnings);

        Assert.Null(report.Matrix);

        var insufficient = warnings.SingleOrDefault(w => w.Category == WarningCategory.InsufficientUsableColumns);
        Assert.NotNull(insufficient);
        Assert.Equal("Correlation", insufficient!.Analyzer);
        Assert.NotNull(insufficient.AffectedColumns);
        Assert.Contains("A", insufficient.AffectedColumns!);
        Assert.Contains("B", insufficient.AffectedColumns!);
    }
}
