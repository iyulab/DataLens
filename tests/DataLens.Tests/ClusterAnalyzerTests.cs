using DataLens.Adapters;
using DataLens.Analyzers;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class ClusterAnalyzerTests
{
    [Fact]
    public async Task Clustering_AssignsLabels()
    {
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);

        // 3개의 명확한 클러스터 생성
        for (int i = 0; i < 30; i++)
            data.Add(new() { ["X"] = (1.0 + rng.NextDouble() * 0.5).ToString(), ["Y"] = (1.0 + rng.NextDouble() * 0.5).ToString() });
        for (int i = 0; i < 30; i++)
            data.Add(new() { ["X"] = (5.0 + rng.NextDouble() * 0.5).ToString(), ["Y"] = (5.0 + rng.NextDouble() * 0.5).ToString() });
        for (int i = 0; i < 30; i++)
            data.Add(new() { ["X"] = (9.0 + rng.NextDouble() * 0.5).ToString(), ["Y"] = (1.0 + rng.NextDouble() * 0.5).ToString() });

        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions { MaxClusters = 8 };

        var analyzer = new ClusterAnalyzer();
        var report = await analyzer.AnalyzeAsync(adapter, options);

        Assert.NotNull(report.KMeans);
        Assert.True(report.KMeans.Labels.Length == 90);
    }
}
