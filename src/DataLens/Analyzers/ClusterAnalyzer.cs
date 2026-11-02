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
            // 탐색 상한은 MaxClusters 이되 행 수를 넘지 않는다 — UInsight 0.26 부터
            // 행 수보다 큰 k_max 는 조정되지 않고 거절된다.
            uint kMax = Math.Min(options.MaxClusters, (uint)matrix.GetLength(0));
            var gap = client.GapStatistic(matrix, 2, kMax);
            optimalK = gap.BestK;
            gapValues = gap.GapValues;
        }
        catch (Exception ex)
        {
            // GapStatistic 실패 시 기본 K=3 으로 계속한다 — 그 사실을 경고로 남긴다
            warnings?.Add(AnalysisWarning.FromException("GapStatistic", ex));
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
        catch (Exception ex)
        {
            warnings?.Add(AnalysisWarning.FromException("KMeans", ex));
        }

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
        catch (Exception ex)
        {
            warnings?.Add(AnalysisWarning.FromException("DBSCAN", ex));
        }

        // Hierarchical
        HierarchicalReport? hierarchicalReport = null;
        if (!SkippedForSize("Hierarchical", nameof(AnalysisOptions.HierarchicalRowThreshold), options.HierarchicalRowThreshold, matrix.GetLength(0), "압축 거리 행렬 n(n−1)/2", warnings))
        {
            try
            {
                var hier = client.Hierarchical(matrix, Linkage.Ward, optimalK);
                hierarchicalReport = new HierarchicalReport
                {
                    NClusters = hier.NClusters,
                    Labels = hier.Labels,
                    MergeDistances = hier.MergeDistances
                };
            }
            catch (Exception ex)
            {
                warnings?.Add(AnalysisWarning.FromException("Hierarchical", ex));
            }
        }

        // HDBSCAN
        HdbscanReport? hdbscanReport = null;
        if (!SkippedForSize("HDBSCAN", nameof(AnalysisOptions.HdbscanRowThreshold), options.HdbscanRowThreshold, matrix.GetLength(0), "행 수에 O(n²)", warnings))
        {
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
            catch (Exception ex)
            {
                warnings?.Add(AnalysisWarning.FromException("HDBSCAN", ex));
            }
        }

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

    /// <summary>행 수가 <paramref name="threshold"/> 를 넘으면 경고를 남기고 true(건너뜀). 0 은 임계 없음.</summary>
    private static bool SkippedForSize(string analyzer, string option, uint threshold, int rows, string why, ICollection<AnalysisWarning>? warnings)
    {
        if (threshold == 0 || (uint)rows <= threshold)
            return false;
        warnings?.Add(new AnalysisWarning(
            Analyzer: analyzer,
            Category: WarningCategory.SkippedForSize,
            Message: $"행 수 {rows} 가 {option}({threshold}) 를 넘어 {analyzer} 를 건너뜀 ({why}). {option} = 0 이면 항상 실행."));
        return true;
    }
}
