using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class ClusterAnalyzer : IAnalyzer<ClusterReport>
{
    public Task<ClusterReport> AnalyzeAsync(
        DataAdapter adapter,
        AnalysisOptions options,
        ICollection<AnalysisWarning>? warnings = null)
    {
        // 클러스터링은 스케일에 민감 → 결측값 대체 + Z-Score 정규화
        var matrix = adapter.ToScaledMatrix();
        if (matrix.GetLength(0) < 3 || matrix.GetLength(1) < 1)
            return Task.FromResult(new ClusterReport());

        using var client = new InsightClient();

        // GapStatistic으로 최적 K 탐색
        uint optimalK = 3;
        double[]? gapValues = null;
        try
        {
            var gap = client.GapStatistic(matrix, 2, options.MaxClusters);
            optimalK = gap.BestK;
            gapValues = gap.GapValues;
        }
        catch
        {
            // GapStatistic 실패 시 기본 K=3
        }

        // K-Means
        KMeansReport? kmeansReport = null;
        try
        {
            // KMeans / MiniBatchKMeans 자동 분기
            bool useMiniBatch = options.MiniBatchKMeansRowThreshold > 0
                && (uint)matrix.GetLength(0) >= options.MiniBatchKMeansRowThreshold;
            var kmeans = useMiniBatch
                ? client.MiniBatchKMeans(matrix, optimalK)
                : client.KMeans(matrix, optimalK);
            var clusterSizes = kmeans.Labels
                .GroupBy(l => l)
                .Select(g => new ClusterSummary
                {
                    ClusterId = g.Key,
                    Size = g.Count(),
                    Percentage = (double)g.Count() / kmeans.Labels.Length * 100
                })
                .OrderBy(s => s.ClusterId)
                .ToList();

            // silhouette 산출 — O(n²) 이므로 옵션 + 행 수 임계로 보호.
            double? silhouetteAvg = null;
            double[]? silhouettePerSample = null;
            if (options.ComputeSilhouette
                && kmeans.K >= 2
                && (options.SilhouetteRowThreshold == 0
                    || (uint)matrix.GetLength(0) <= options.SilhouetteRowThreshold))
            {
                try
                {
                    var sil = client.Silhouette(matrix, kmeans.Labels, kmeans.K);
                    silhouetteAvg = sil.Avg;
                    silhouettePerSample = sil.PerSample;
                }
                catch (InsightException ex)
                {
                    warnings?.Add(AnalysisWarning.FromInsightException("Silhouette", ex));
                }
            }

            kmeansReport = new KMeansReport
            {
                K = kmeans.K,
                Wcss = kmeans.Wcss,
                Iterations = kmeans.Iterations,
                Labels = kmeans.Labels,
                ClusterSizes = clusterSizes,
                UsedMiniBatch = useMiniBatch,
                SilhouetteAvg = silhouetteAvg,
                SilhouettePerSample = silhouettePerSample
            };
        }
        catch { }

        // DBSCAN
        DbscanReport? dbscanReport = null;
        try
        {
            var dbscan = client.Dbscan(matrix, 0.5, 5);
            dbscanReport = new DbscanReport
            {
                NClusters = dbscan.NClusters,
                NoiseCount = dbscan.NoiseCount,
                Labels = dbscan.Labels
            };
        }
        catch { }

        // Hierarchical
        HierarchicalReport? hierarchicalReport = null;
        try
        {
            var hier = client.Hierarchical(matrix, 0, optimalK); // linkage=0 (Ward)
            hierarchicalReport = new HierarchicalReport
            {
                NClusters = hier.NClusters,
                Labels = hier.Labels,
                MergeDistances = hier.MergeDistances
            };
        }
        catch { }

        // HDBSCAN
        HdbscanReport? hdbscanReport = null;
        try
        {
            uint minSamples = options.HdbscanMinSamples == 0
                ? options.HdbscanMinClusterSize
                : options.HdbscanMinSamples;
            var hdb = client.Hdbscan(matrix, options.HdbscanMinClusterSize, minSamples);
            hdbscanReport = new HdbscanReport
            {
                NClusters = hdb.NClusters,
                NoiseCount = hdb.NoiseCount,
                Labels = hdb.Labels,
                Probabilities = hdb.Probabilities
            };
        }
        catch { }

        return Task.FromResult(new ClusterReport
        {
            OptimalK = optimalK,
            GapValues = gapValues,
            KMeans = kmeansReport,
            Dbscan = dbscanReport,
            Hierarchical = hierarchicalReport,
            Hdbscan = hdbscanReport
        });
    }
}
