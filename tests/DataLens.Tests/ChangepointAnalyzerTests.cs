using DataLens.Adapters;
using DataLens.Analyzers;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class ChangepointAnalyzerTests
{
    /// <summary>
    /// 명확한 단계 변화가 있는 시계열 데이터에서 변화점을 탐지한다.
    /// </summary>
    [Fact]
    public async Task DetectsStepChange()
    {
        // 0~49: 평균 10, 50~99: 평균 50
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            double value = i < 50 ? 10 + rng.NextDouble() : 50 + rng.NextDouble();
            data.Add(new Dictionary<string, string> { ["Value"] = value.ToString("F4") });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var analyzer = new ChangepointAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.NotNull(report);
        Assert.Single(report.Columns);

        var col = report.Columns[0];
        Assert.Equal("Value", col.Name);
        Assert.True(col.Changepoints.Length >= 1, "Should detect at least one changepoint");
        // 변화점이 50 근처에 있어야 함
        Assert.Contains(col.Changepoints, cp => cp >= 45 && cp <= 55);
    }

    /// <summary>
    /// 상수 데이터에서는 변화점이 없어야 한다.
    /// </summary>
    [Fact]
    public async Task ConstantData_NoChangepoints()
    {
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 50; i++)
            data.Add(new Dictionary<string, string> { ["Value"] = "42.0" });

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var analyzer = new ChangepointAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.NotNull(report);
        if (report.Columns.Count > 0)
        {
            // 변화점이 없거나 컬럼이 건너뛰어졌어야 함
            Assert.Empty(report.Columns[0].Changepoints);
        }
    }

    /// <summary>
    /// 여러 숫자 컬럼이 있는 경우 각각 독립적으로 분석한다.
    /// </summary>
    [Fact]
    public async Task MultipleColumns_AnalyzedIndependently()
    {
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            double x = i < 50 ? 10 + rng.NextDouble() : 50 + rng.NextDouble();
            double y = rng.NextDouble() * 5; // 변화점 없는 안정적 데이터
            data.Add(new Dictionary<string, string>
            {
                ["X"] = x.ToString("F4"),
                ["Y"] = y.ToString("F4")
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var analyzer = new ChangepointAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.NotNull(report);
        Assert.Equal(2, report.Columns.Count);
    }

    /// <summary>
    /// 데이터가 너무 적으면 건너뛴다.
    /// </summary>
    [Fact]
    public async Task InsufficientData_SkipsColumn()
    {
        var data = new List<Dictionary<string, string>>
        {
            new() { ["Value"] = "1.0" },
            new() { ["Value"] = "2.0" },
            new() { ["Value"] = "3.0" }
        };

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var analyzer = new ChangepointAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.NotNull(report);
        // 최소 세그먼트 길이(2) * 2 = 4보다 적으므로 건너뛰어야 함
        Assert.Empty(report.Columns);
    }

    /// <summary>
    /// 세그먼트 요약 통계가 올바르게 계산되는지 확인한다.
    /// </summary>
    [Fact]
    public async Task SegmentSummaries_HaveCorrectStructure()
    {
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            double value = i < 50 ? 10 + rng.NextDouble() : 50 + rng.NextDouble();
            data.Add(new Dictionary<string, string> { ["Value"] = value.ToString("F4") });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var analyzer = new ChangepointAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, AnalysisOptions.Default);

        var col = report.Columns[0];
        Assert.True(col.Segments.Count >= 2, "Should have at least 2 segments");

        foreach (var seg in col.Segments)
        {
            Assert.True(seg.Length > 0);
            Assert.True(seg.End > seg.Start);
            Assert.Equal(seg.End - seg.Start, seg.Length);
            Assert.True(seg.StdDev >= 0);
        }

        // 전체 길이 합 = 데이터 크기
        Assert.Equal(100, col.Segments.Sum(s => s.Length));
    }

    /// <summary>
    /// IncludeChangepoints=false이면 엔진이 변화점 분석을 건너뛴다.
    /// </summary>
    [Fact]
    public async Task Engine_SkipsWhenDisabled()
    {
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 50; i++)
            data.Add(new Dictionary<string, string> { ["Value"] = (i * 1.0).ToString("F4") });

        var df = DataPipeline.FromData(data).ToDataFrame();
        var result = await DataLensEngine.Analyze(df, new AnalysisOptions
        {
            IncludeProfiling = false,
            IncludeDescriptive = false,
            IncludeCorrelation = false,
            IncludeRegression = false,
            IncludeDistribution = false,
            IncludeClustering = false,
            IncludeOutliers = false,
            IncludeFeatures = false,
            IncludePca = false,
            IncludeChangepoints = false,
        });

        Assert.Null(result.Changepoints);
    }

    /// <summary>
    /// 엔진 통합 테스트: 변화점 분석 결과가 JSON에 포함된다.
    /// </summary>
    [Fact]
    public async Task Engine_IncludesChangepointsInJson()
    {
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            double value = i < 50 ? 10 + rng.NextDouble() : 50 + rng.NextDouble();
            data.Add(new Dictionary<string, string> { ["Value"] = value.ToString("F4") });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var result = await DataLensEngine.Analyze(df, new AnalysisOptions
        {
            IncludeProfiling = false,
            IncludeDescriptive = false,
            IncludeCorrelation = false,
            IncludeRegression = false,
            IncludeDistribution = false,
            IncludeClustering = false,
            IncludeOutliers = false,
            IncludeFeatures = false,
            IncludePca = false,
            IncludeChangepoints = true,
        });

        Assert.NotNull(result.Changepoints);
        var json = result.ToJson();
        Assert.Contains("changepoints", json);
        Assert.Contains("segments", json);
    }

    [Fact]
    public async Task SingleColumn_NoMultivariateResult()
    {
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            double value = i < 50 ? 10 + rng.NextDouble() : 50 + rng.NextDouble();
            data.Add(new Dictionary<string, string> { ["Value"] = value.ToString("F4") });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var report = await new ChangepointAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.NotNull(report);
        Assert.Null(report.Multivariate);
    }

    [Fact]
    public async Task MultipleColumns_PopulatesMultivariateResult()
    {
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            double x = i < 50 ? 10 + rng.NextDouble() : 50 + rng.NextDouble();
            double y = i < 50 ? 1 + rng.NextDouble() : 5 + rng.NextDouble();
            data.Add(new Dictionary<string, string>
            {
                ["X"] = x.ToString("F4"),
                ["Y"] = y.ToString("F4")
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var report = await new ChangepointAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.NotNull(report);
        Assert.NotNull(report.Multivariate);
        Assert.Equal(2, report.Multivariate.Columns.Count);
        Assert.Equal(100, report.Multivariate.SampleSize);
        Assert.True(report.Multivariate.Changepoints.Length >= 1, "Should detect at least one shared changepoint");
        Assert.Contains(report.Multivariate.Changepoints, cp => cp >= 45 && cp <= 55);
    }

    [Fact]
    public async Task InsufficientMultivariateData_NullMultivariate()
    {
        var data = new List<Dictionary<string, string>>
        {
            new() { ["X"] = "1.0", ["Y"] = "1.0" },
            new() { ["X"] = "2.0", ["Y"] = "2.0" },
            new() { ["X"] = "3.0", ["Y"] = "3.0" }
        };

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var report = await new ChangepointAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.NotNull(report);
        Assert.Null(report.Multivariate);
    }
}
