using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

/// <summary>
/// v0.11.0 EDA 확장 기능 검증:
/// - Tier 1-A: Spearman/Kendall correlation method
/// - Tier 1-B: 단변량 outlier (Tukey/3σ/Hampel)
/// - Tier 2-B: Condition number 다중공선성 진단
/// - D3: MissingnessPattern (co-missing 컬럼 그룹)
/// - D4: DuplicateRows
/// - D5: 분포 라벨 (DistributionShape)
/// </summary>
public class V0_11_0_FeatureTests
{
    // ───────────────────── Tier 1-A: Spearman / Kendall ─────────────────────

    [Fact]
    public async Task Correlation_PearsonDefault_RecordsMethod()
    {
        var df = LinearDataFrame(50);
        var adapter = new DataAdapter(df);

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions());

        Assert.Equal(CorrelationMethod.Pearson, report.Method);
    }

    [Fact]
    public async Task Correlation_Spearman_DetectsMonotonicNonLinear()
    {
        // Y = X^3 — Pearson < 1 이지만 monotonic 이라 Spearman ≈ 1.
        var data = new List<Dictionary<string, string>>();
        for (int i = 1; i <= 50; i++)
        {
            var x = i;
            var y = i * i * i;
            data.Add(new Dictionary<string, string>
            {
                ["X"] = x.ToString(),
                ["Y"] = y.ToString()
            });
        }
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter,
            new AnalysisOptions { CorrelationMethod = CorrelationMethod.Spearman });

        Assert.Equal(CorrelationMethod.Spearman, report.Method);
        Assert.NotNull(report.Matrix);
        // Spearman 은 monotonic 관계에서 ≈ 1.
        Assert.True(Math.Abs(report.Matrix![0, 1]) > 0.99);
    }

    [Fact]
    public async Task Correlation_Kendall_RunsWithoutError()
    {
        var df = LinearDataFrame(30);
        var adapter = new DataAdapter(df);

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter,
            new AnalysisOptions { CorrelationMethod = CorrelationMethod.Kendall });

        Assert.Equal(CorrelationMethod.Kendall, report.Method);
        Assert.NotNull(report.Matrix);
    }

    // ───────────────────── Tier 2-B: Condition number ─────────────────────

    [Fact]
    public async Task Correlation_ProducesConditionNumber_OnIndependentColumns()
    {
        // Independent (uncorrelated) columns → condition number 가 정의됨.
        // LinearDataFrame 은 X, Y, Z 가 i 의 선형 함수라 rank-1 (singular) 이라 FeatureImportance 가 fail.
        var rng = new Random(7);
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 50; i++)
        {
            data.Add(new()
            {
                ["X"] = rng.NextDouble().ToString("F6"),
                ["Y"] = rng.NextDouble().ToString("F6"),
                ["Z"] = rng.NextDouble().ToString("F6")
            });
        }
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions());

        Assert.NotNull(report.ConditionNumber);
        Assert.True(report.ConditionNumber > 0);
        // 독립 컬럼 → cond 가 작은 값 (보통 < 10).
        Assert.True(report.ConditionNumber < 100);
    }

    [Fact]
    public async Task Correlation_ConditionNumber_NullForSingleColumn()
    {
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 30; i++) data.Add(new() { ["X"] = i.ToString() });
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = await new CorrelationAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions());

        Assert.Null(report.ConditionNumber);
    }

    // ───────────────────── Tier 1-B: 단변량 outlier ─────────────────────

    [Fact]
    public void UnivariateOutlier_Tukey_DetectsExtremeOutlier()
    {
        var values = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 100 }; // 100 = outlier

        var result = UnivariateOutlierDetector.Tukey(values);

        Assert.Equal(UnivariateOutlierMethod.Tukey, result.Method);
        Assert.Contains(10, result.AnomalyIndices); // index 10 = value 100
        Assert.True(result.AnomalyCount >= 1);
        Assert.True(result.UpperFence < 100);
    }

    [Fact]
    public void UnivariateOutlier_ThreeSigma_UsesMeanAndStd()
    {
        // 다수의 정상값 + 명확한 outlier — 3σ 가 mean shift 영향을 덜 받게.
        var values = Enumerable.Range(0, 30).Select(i => (double)i).Concat(new double[] { 500 }).ToArray();

        var result = UnivariateOutlierDetector.ThreeSigma(values);

        Assert.Equal(UnivariateOutlierMethod.ThreeSigma, result.Method);
        Assert.Contains(30, result.AnomalyIndices); // index 30 = value 500
    }

    [Fact]
    public void UnivariateOutlier_Hampel_RobustToOutliers()
    {
        // 다수 outlier (3σ 가 mean shift 로 일부 놓치는 케이스).
        var values = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 100, 101, 102 };

        var result = UnivariateOutlierDetector.Hampel(values);

        Assert.Equal(UnivariateOutlierMethod.Hampel, result.Method);
        // Hampel 은 median + MAD 로 robust — 100/101/102 모두 검출.
        Assert.True(result.AnomalyCount >= 3);
    }

    [Fact]
    public void UnivariateOutlier_InsufficientData_ReturnsEmpty()
    {
        var result = UnivariateOutlierDetector.Tukey(new double[] { 1, 2, 3 });

        Assert.Empty(result.AnomalyIndices);
        Assert.Equal(0, result.AnomalyCount);
    }

    [Fact]
    public async Task OutlierAnalyzer_PopulatesUnivariateReport()
    {
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 30; i++)
        {
            data.Add(new()
            {
                ["X"] = i.ToString(),
                ["Y"] = (i * 2).ToString()
            });
        }
        // 한 행만 극단값.
        data[15]["X"] = "10000";
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = await new OutlierAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions());

        Assert.NotNull(report.Univariate);
        Assert.Contains("X", report.Univariate.Tukey.Keys);
        Assert.Contains("X", report.Univariate.ThreeSigma.Keys);
        Assert.Contains("X", report.Univariate.Hampel.Keys);
        Assert.True(report.Univariate.Tukey["X"].AnomalyCount > 0);
    }

    // ───────────────────── D3: MissingnessPattern ─────────────────────

    [Fact]
    public async Task DataQuality_EmitsMissingnessPattern_ForCoMissingColumns()
    {
        var data = new List<Dictionary<string, string>>();
        // 6 행 중 2 행만 PlanSpec/EffectiveSpec 동시 결측 (정확히 동일 행).
        for (int i = 0; i < 6; i++)
        {
            var row = new Dictionary<string, string>
            {
                ["Id"] = i.ToString(),
                ["Spec"] = "10"
            };
            if (i != 2 && i != 4) // 행 2, 4 만 결측.
            {
                row["PlanSpec"] = "11";
                row["EffectiveSpec"] = "12";
            }
            else
            {
                row["PlanSpec"] = "";
                row["EffectiveSpec"] = "";
            }
            data.Add(row);
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
            IncludeChangepoints = false
        });

        var coMissing = result.Warnings
            .FirstOrDefault(w => w.Category == WarningCategory.MissingnessPattern);
        Assert.NotNull(coMissing);
        Assert.NotNull(coMissing.AffectedColumns);
        Assert.Contains("PlanSpec", coMissing.AffectedColumns!);
        Assert.Contains("EffectiveSpec", coMissing.AffectedColumns!);
    }

    // ───────────────────── D4: DuplicateRows ─────────────────────

    [Fact]
    public async Task DataQuality_EmitsDuplicateRows_ForRepeatedRows()
    {
        var data = new List<Dictionary<string, string>>();
        // 3 unique 행 + 같은 행 2 개 추가 → DuplicateRowCount = 2.
        data.Add(new() { ["X"] = "1", ["Y"] = "10" });
        data.Add(new() { ["X"] = "2", ["Y"] = "20" });
        data.Add(new() { ["X"] = "3", ["Y"] = "30" });
        data.Add(new() { ["X"] = "1", ["Y"] = "10" }); // dup
        data.Add(new() { ["X"] = "2", ["Y"] = "20" }); // dup
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
            IncludeChangepoints = false
        });

        var dup = result.Warnings.FirstOrDefault(w => w.Category == WarningCategory.DuplicateRows);
        Assert.NotNull(dup);
        Assert.Contains("2", dup.Message); // count = 2
    }

    [Fact]
    public async Task DataQuality_NoDuplicateRows_NoWarning()
    {
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 5; i++) data.Add(new() { ["X"] = i.ToString() });
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
            IncludeChangepoints = false
        });

        Assert.DoesNotContain(result.Warnings, w => w.Category == WarningCategory.DuplicateRows);
    }

    // ───────────────────── D5: 분포 라벨 ─────────────────────

    [Fact]
    public async Task Distribution_NormalInput_ClassifiedAsNormalLike()
    {
        // Box-Muller 로 ~정규 분포 생성.
        var rng = new Random(42);
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 200; i++)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            data.Add(new() { ["X"] = z.ToString("F6") });
        }
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = await new DistributionAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions());

        var col = Assert.Single(report.Columns);
        Assert.Equal(DistributionShape.NormalLike, col.Shape);
        Assert.NotNull(col.Skewness);
        Assert.NotNull(col.Kurtosis);
    }

    [Fact]
    public async Task Distribution_RightSkewed_ClassifiedCorrectly()
    {
        // 지수 분포 (오른쪽 꼬리).
        var rng = new Random(42);
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 200; i++)
        {
            double u = 1.0 - rng.NextDouble();
            double exp = -Math.Log(u);
            data.Add(new() { ["X"] = exp.ToString("F6") });
        }
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = await new DistributionAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions());

        var col = Assert.Single(report.Columns);
        Assert.True(col.Shape == DistributionShape.RightSkewed
                    || col.Shape == DistributionShape.HeavyTailed,
            $"Expected RightSkewed or HeavyTailed, got {col.Shape}");
        Assert.True(col.Skewness > 0.5);
    }

    // ───────────────────── helpers ─────────────────────

    private static DataFrame LinearDataFrame(int n)
    {
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < n; i++)
        {
            data.Add(new()
            {
                ["X"] = i.ToString(),
                ["Y"] = (i * 2 + 1).ToString(),
                ["Z"] = ((i + 7) * 3).ToString()
            });
        }
        return DataPipeline.FromData(data).ToDataFrame();
    }
}
