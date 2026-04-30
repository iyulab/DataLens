using DataLens.Adapters;
using DataLens.Analyzers;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class PcaAnalyzerTests
{
    [Fact]
    public async Task Pca_PopulatesScoresAndLoadings()
    {
        // 3 컬럼, 50 행. PC 공간 사영(Scores)과 적재량(Loadings)을 함께 검증한다.
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(7);
        for (int i = 0; i < 50; i++)
        {
            double x = rng.NextDouble() * 10;
            data.Add(new()
            {
                ["X"] = x.ToString("R"),
                ["Y"] = (x * 1.5 + rng.NextDouble()).ToString("R"),
                ["Z"] = (rng.NextDouble() * 2).ToString("R")
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = await new PcaAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.True(report.NComponents >= 1);
        Assert.Equal(3u, report.NFeatures);
        Assert.Equal(50u, report.NSamples);

        // ExplainedVariance / CumulativeVariance 길이 동일.
        Assert.Equal(report.ExplainedVariance.Length, report.CumulativeVariance.Length);
        Assert.True(report.CumulativeVariance[^1] <= 1.0 + 1e-9);

        // Scores: NSamples 행 × NComponents 열 (산출 성분 전체).
        Assert.Equal(50, report.Scores.Length);
        Assert.Equal(report.ExplainedVariance.Length, report.Scores[0].Length);

        // Loadings: NComponents 행 × NFeatures 열, 각 행 단위 노름 ≈ 1.
        Assert.NotNull(report.Loadings);
        Assert.Equal(report.ExplainedVariance.Length, report.Loadings!.GetLength(0));
        Assert.Equal(3, report.Loadings.GetLength(1));
        for (int k = 0; k < report.Loadings.GetLength(0); k++)
        {
            double normSq = 0;
            for (int j = 0; j < report.Loadings.GetLength(1); j++)
                normSq += report.Loadings[k, j] * report.Loadings[k, j];
            Assert.True(Math.Abs(normSq - 1.0) < 1e-6,
                $"Loadings row {k} expected unit norm, got {Math.Sqrt(normSq)}");
        }
    }

    [Fact]
    public async Task Pca_TotalExplainedVarianceMatchesCumulativeAtNComponents()
    {
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(13);
        for (int i = 0; i < 40; i++)
        {
            data.Add(new()
            {
                ["A"] = rng.NextDouble().ToString("R"),
                ["B"] = rng.NextDouble().ToString("R"),
                ["C"] = rng.NextDouble().ToString("R"),
                ["D"] = rng.NextDouble().ToString("R")
            });
        }

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var report = await new PcaAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default);

        Assert.True(report.NComponents >= 1);
        // TotalExplainedVariance 는 NComponents 위치의 누적값과 일치해야 한다.
        Assert.Equal(report.CumulativeVariance[(int)report.NComponents - 1], report.TotalExplainedVariance, 6);
    }
}
