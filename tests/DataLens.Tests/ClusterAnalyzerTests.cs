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

    [Fact]
    public async Task SmallDataset_UsesStandardKMeans()
    {
        var data = MakeThreeClusters(rows: 30);
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions
        {
            MaxClusters = 8,
            MiniBatchKMeansRowThreshold = 10000
        };

        var report = await new ClusterAnalyzer().AnalyzeAsync(adapter, options);

        Assert.NotNull(report.KMeans);
        Assert.False(report.KMeans.UsedMiniBatch, "Standard KMeans should be used below threshold");
    }

    [Fact]
    public async Task LargeDataset_UsesMiniBatchKMeans()
    {
        var data = MakeThreeClusters(rows: 200);
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions
        {
            MaxClusters = 8,
            MiniBatchKMeansRowThreshold = 100  // 인위적으로 낮춰서 분기 강제
        };

        var report = await new ClusterAnalyzer().AnalyzeAsync(adapter, options);

        Assert.NotNull(report.KMeans);
        Assert.True(report.KMeans.UsedMiniBatch, "MiniBatchKMeans should be used above threshold");
        Assert.Equal(600u, (uint)report.KMeans.Labels.Length);
    }

    [Fact]
    public async Task Clustering_PopulatesHdbscan()
    {
        var data = MakeThreeClusters(rows: 30);
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions
        {
            MaxClusters = 8,
            HdbscanMinClusterSize = 5
        };

        var report = await new ClusterAnalyzer().AnalyzeAsync(adapter, options);

        Assert.NotNull(report.Hdbscan);
        Assert.Equal(90, report.Hdbscan.Labels.Length);
        Assert.Equal(90, report.Hdbscan.Probabilities.Length);
    }

    [Fact]
    public async Task KMeans_PopulatesSilhouetteWhenEnabled()
    {
        var data = MakeThreeClusters(rows: 30);
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions
        {
            MaxClusters = 8,
            ComputeSilhouette = true
        };

        var report = await new ClusterAnalyzer().AnalyzeAsync(adapter, options);

        Assert.NotNull(report.KMeans);
        Assert.NotNull(report.KMeans.SilhouetteAvg);
        Assert.InRange(report.KMeans.SilhouetteAvg!.Value, -1.0, 1.0);
        // 잘 분리된 3 cluster — silhouette 가 양호 영역(>0.4)
        Assert.True(report.KMeans.SilhouetteAvg.Value > 0.4,
            $"Expected silhouette > 0.4 for well-separated clusters, got {report.KMeans.SilhouetteAvg.Value}");
        Assert.NotNull(report.KMeans.SilhouettePerSample);
        Assert.Equal(report.KMeans.Labels.Length, report.KMeans.SilhouettePerSample!.Length);
    }

    [Fact]
    public async Task KMeans_SkipsSilhouetteWhenDisabled()
    {
        var data = MakeThreeClusters(rows: 30);
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions
        {
            MaxClusters = 8,
            ComputeSilhouette = false
        };

        var report = await new ClusterAnalyzer().AnalyzeAsync(adapter, options);

        Assert.NotNull(report.KMeans);
        Assert.Null(report.KMeans.SilhouetteAvg);
        Assert.Null(report.KMeans.SilhouettePerSample);
    }

    [Fact]
    public async Task KMeans_SkipsSilhouetteWhenAboveRowThreshold()
    {
        var data = MakeThreeClusters(rows: 30);
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions
        {
            MaxClusters = 8,
            ComputeSilhouette = true,
            SilhouetteRowThreshold = 50  // 90 행이 임계 초과 → skip
        };

        var report = await new ClusterAnalyzer().AnalyzeAsync(adapter, options);

        Assert.NotNull(report.KMeans);
        Assert.Null(report.KMeans.SilhouetteAvg);
    }

    [Fact]
    public async Task MiniBatchKMeansThresholdZero_AlwaysUsesStandardKMeans()
    {
        var data = MakeThreeClusters(rows: 200);
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);
        var options = new AnalysisOptions
        {
            MaxClusters = 8,
            MiniBatchKMeansRowThreshold = 0  // 0 = 항상 KMeans
        };

        var report = await new ClusterAnalyzer().AnalyzeAsync(adapter, options);

        Assert.NotNull(report.KMeans);
        Assert.False(report.KMeans.UsedMiniBatch);
    }

    private static List<Dictionary<string, string>> MakeThreeClusters(int rows)
    {
        var data = new List<Dictionary<string, string>>();
        var rng = new Random(42);
        for (int i = 0; i < rows; i++)
            data.Add(new() { ["X"] = (1.0 + rng.NextDouble() * 0.5).ToString(), ["Y"] = (1.0 + rng.NextDouble() * 0.5).ToString() });
        for (int i = 0; i < rows; i++)
            data.Add(new() { ["X"] = (5.0 + rng.NextDouble() * 0.5).ToString(), ["Y"] = (5.0 + rng.NextDouble() * 0.5).ToString() });
        for (int i = 0; i < rows; i++)
            data.Add(new() { ["X"] = (9.0 + rng.NextDouble() * 0.5).ToString(), ["Y"] = (1.0 + rng.NextDouble() * 0.5).ToString() });
        return data;
    }
}
