using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class ClusterAnalyzer : IAnalyzer<ClusterReport>
{
    public Task<ClusterReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        var matrix = adapter.ToCleanMatrix();
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
            var kmeans = client.KMeans(matrix, optimalK);
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

            kmeansReport = new KMeansReport
            {
                K = kmeans.K,
                Wcss = kmeans.Wcss,
                Iterations = kmeans.Iterations,
                Labels = kmeans.Labels,
                ClusterSizes = clusterSizes
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

        return Task.FromResult(new ClusterReport
        {
            OptimalK = optimalK,
            GapValues = gapValues,
            KMeans = kmeansReport,
            Dbscan = dbscanReport,
            Hierarchical = hierarchicalReport
        });
    }
}
