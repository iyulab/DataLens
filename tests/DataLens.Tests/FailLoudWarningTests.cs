using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;
using UInsight;

namespace DataLens.Tests;

/// <summary>
/// v0.9.0 fail-loud + DataQualityScreener 동작 검증.
/// </summary>
public class FailLoudWarningTests
{
    [Fact]
    public async Task Correlation_NumericLessThan2_EmitsInsufficientColumns()
    {
        // 1 numeric + 1 categorical → numeric 컬럼 1 개.
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 10; i++)
            data.Add(new() { ["x"] = i.ToString(), ["label"] = $"row{i}" });

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var warnings = new List<AnalysisWarning>();

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default, warnings);

        Assert.Null(report.Matrix);
        Assert.Contains(warnings, w =>
            w.Analyzer == "Correlation" &&
            w.Category == WarningCategory.InsufficientColumns);
    }

    [Fact]
    public async Task Correlation_DuplicateColumns_EmitsDuplicateColumnsWarning()
    {
        // a == b (완전 동일), c 는 독립.
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 20; i++)
        {
            data.Add(new()
            {
                ["a"] = i.ToString(),
                ["b"] = i.ToString(),
                ["c"] = (i * 7 % 13).ToString()
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var warnings = new List<AnalysisWarning>();

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default, warnings);

        Assert.NotNull(report.Matrix);
        var dup = warnings.FirstOrDefault(w => w.Category == WarningCategory.DuplicateColumns);
        Assert.NotNull(dup);
        Assert.NotNull(dup.AffectedColumns);
        Assert.Contains("a", dup.AffectedColumns);
        Assert.Contains("b", dup.AffectedColumns);
    }

    [Fact]
    public void DataQualityScreener_ConstantColumn_DetectedAsConstantColumns()
    {
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 10; i++)
        {
            data.Add(new()
            {
                ["fixed"] = "42",
                ["varying"] = i.ToString()
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = adapter.DataQuality;

        Assert.Contains("fixed", report.ConstantColumns);
        Assert.DoesNotContain("varying", report.ConstantColumns);
        Assert.True(report.HasSingularCovarianceRisk);
    }

    [Fact]
    public async Task Outlier_ConstantColumn_EmitsSingularCovariancePreDiagnostic()
    {
        // 분산 0 컬럼 + 다른 numeric 컬럼.
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 30; i++)
        {
            data.Add(new()
            {
                ["constant"] = "1",
                ["varying"] = i.ToString(),
                ["other"] = (i * 2).ToString()
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var warnings = new List<AnalysisWarning>();

        await new OutlierAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default, warnings);

        var preDiag = warnings.FirstOrDefault(w =>
            w.Analyzer == "Mahalanobis" && w.Category == WarningCategory.SingularCovariance);
        Assert.NotNull(preDiag);
        Assert.NotNull(preDiag.AffectedColumns);
        Assert.Contains("constant", preDiag.AffectedColumns);
    }

    [Fact]
    public async Task Outlier_NormalInput_NoSingularCovarianceWarning()
    {
        // 정상 입력 → 사전 진단 emit 안 됨.
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);
        for (int i = 0; i < 50; i++)
        {
            data.Add(new()
            {
                ["a"] = rng.NextDouble().ToString("R"),
                ["b"] = rng.NextDouble().ToString("R"),
                ["c"] = rng.NextDouble().ToString("R")
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var warnings = new List<AnalysisWarning>();

        await new OutlierAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default, warnings);

        Assert.DoesNotContain(warnings, w => w.Category == WarningCategory.SingularCovariance);
    }

    [Fact]
    public async Task Engine_MahalanobisDegenerateInput_MapsToSingularCovarianceWithUpstreamCategory()
    {
        // 모든 행 모든 컬럼 동일 — UInsight 0.8.1 mahalanobis 의 zero-variance 사전 검사가 DegenerateData throw.
        // SafeAnalyze 가 AnalysisWarning.FromInsightException 으로 SingularCovariance + UpstreamCategory 매핑.
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 20; i++)
        {
            data.Add(new()
            {
                ["a"] = "1",
                ["b"] = "1",
                ["c"] = "1"
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var result = await DataLensEngine.Analyze(df, OnlyOutlier());

        // 사전 진단 (DataQualityScreener) SingularCovariance — UpstreamCategory 없음 (DataLens 측 진단).
        var preDiag = result.Warnings.FirstOrDefault(w =>
            w.Analyzer == "Mahalanobis" &&
            w.Category == WarningCategory.SingularCovariance &&
            w.UpstreamCategory is null);
        Assert.NotNull(preDiag);

        // UInsight 0.8.1 throw → SingularCovariance + UpstreamCategory.DegenerateData 보존.
        var upstreamDiag = result.Warnings.FirstOrDefault(w =>
            w.Analyzer == "Mahalanobis" &&
            w.UpstreamCategory == InsightErrorCategory.DegenerateData);
        Assert.NotNull(upstreamDiag);
        Assert.Equal(WarningCategory.SingularCovariance, upstreamDiag.Category);
    }

    // 매핑 로직 자체의 단위 테스트는 InsightException 직접 인스턴스 생성이 어려워 (native error code 의존)
    // 통합 테스트 Engine_MahalanobisDegenerateInput_* 으로 대체한다 — UInsight 0.8.1 의 실제 throw 가
    // SingularCovariance + UpstreamCategory.DegenerateData 로 매핑되는지 end-to-end 검증.

    private static AnalysisOptions OnlyOutlier() => new()
    {
        IncludeProfiling = false,
        IncludeDescriptive = false,
        IncludeCorrelation = false,
        IncludeRegression = false,
        IncludeClustering = false,
        IncludeDistribution = false,
        IncludeFeatures = false,
        IncludePca = false,
        IncludeChangepoints = false
    };
}
