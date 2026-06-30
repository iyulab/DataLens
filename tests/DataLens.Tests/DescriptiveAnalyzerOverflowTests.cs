using DataLens.Adapters;
using DataLens.Analyzers;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

/// <summary>
/// DescriptiveAnalyzer 의 표본 왜도(g1)/첨도(g2) 분모는 n(n-1)(n-2)(n-3) 형태의 곱을 쓴다.
/// 이 곱이 <c>int</c> 산술로 평가되면 n 이 클 때 정수 오버플로우가 발생해 통계량이 손상된다
/// (관측: n≈50,000 에서 Kurtosis ≈ -356,422). KAMP SEQ020(전해탈지, 50K행) 라이브 EDA 도그푸딩에서 발견.
/// </summary>
public class DescriptiveAnalyzerOverflowTests
{
    /// <summary>큰 n(분모 (n-1)(n-2)(n-3) 가 int.MaxValue 를 크게 초과)에서도 정규-유사 데이터의
    /// excess 첨도는 0 근처, 왜도도 0 근처여야 한다. int 오버플로우면 |값| 이 폭발한다.</summary>
    [Fact]
    public async Task LargeN_SkewnessKurtosis_NotCorruptedByIntegerOverflow()
    {
        // Box-Muller 표준정규 ~ skew≈0, excess kurt≈0. n 은 (n-1)(n-2)(n-3) > int.MaxValue 보장 크기.
        var rng = new Random(42);
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 50_000; i++)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            data.Add(new() { ["X"] = z.ToString("F6") });
        }
        var adapter = new DataAdapter(DataPipeline.FromData(data).ToDataFrame());

        var report = await new DescriptiveAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions());
        var col = Assert.Single(report.Columns);

        Assert.NotNull(col.Skewness);
        Assert.NotNull(col.Kurtosis);
        // 표본 통계량 — 정규에서 둘 다 0 근처. 오버플로우면 수만~수십만 단위로 폭발한다.
        Assert.True(Math.Abs(col.Skewness!.Value) < 1.0,
            $"Skewness 폭발(오버플로우 의심): {col.Skewness}");
        Assert.True(Math.Abs(col.Kurtosis!.Value) < 2.0,
            $"Kurtosis 폭발(오버플로우 의심): {col.Kurtosis}");
    }
}
