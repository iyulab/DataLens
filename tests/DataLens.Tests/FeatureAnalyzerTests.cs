using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

/// <summary>
/// FeatureAnalyzer target-aware importance — 타깃 컬럼이 피처 행렬에서 제외되어야
/// permutation importance가 self-prediction으로 0이 되지 않는다 (F-06).
/// </summary>
public class FeatureAnalyzerTests
{
    // 타깃 Y = 2*X (+미세 noise), N = 무관 noise. 타깃을 행렬에서 제외하면
    // X는 Y를 예측하므로 permutation importance가 0이 아니어야 하고,
    // 결과에 타깃 Y가 self-reference로 등장하지 않아야 한다.
    private static DataAdapter BuildAdapter(int n = 80)
    {
        var rng = new Random(7);
        var data = new List<Dictionary<string, string>>(n);
        for (int i = 0; i < n; i++)
        {
            double x = i;
            double noise = rng.NextDouble() * 100;
            double y = 2 * x + (rng.NextDouble() - 0.5); // X로 강하게 예측 가능
            data.Add(new Dictionary<string, string>
            {
                ["X"] = x.ToString("R"),
                ["N"] = noise.ToString("R"),
                ["Y"] = y.ToString("R"),
            });
        }
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    [Fact]
    public async Task NumericTarget_ExcludedFromMatrix_PermutationNotAllZero()
    {
        var adapter = BuildAdapter();

        var report = await new FeatureAnalyzer().AnalyzeAsync(
            adapter, new AnalysisOptions { TargetColumn = "Y" });

        Assert.NotNull(report.Permutation);
        var feats = report.Permutation!.Features;

        // (1) 타깃 Y가 피처 결과에 self-reference로 등장하지 않는다.
        Assert.DoesNotContain(feats, f => f.Name == "Y");
        // 피처는 X, N 둘뿐(타깃 제외).
        Assert.Equal(2, feats.Count);

        // (2) 핵심 회귀: 실피처 importance가 전부 0이 아니다(이전엔 self-prediction으로 전부 0).
        Assert.Contains(feats, f => Math.Abs(f.Importance) > 1e-9);

        // (3) 예측적 피처 X가 무관 noise N보다 중요.
        var x = feats.First(f => f.Name == "X");
        var noise = feats.First(f => f.Name == "N");
        Assert.True(x.Importance > noise.Importance,
            $"X({x.Importance}) should outrank noise N({noise.Importance})");
    }
}
