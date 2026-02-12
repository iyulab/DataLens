using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class OutlierAnalyzer : IAnalyzer<OutlierReport>
{
    public Task<OutlierReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        var matrix = adapter.ToCleanMatrix();
        int nRows = matrix.GetLength(0);
        if (nRows < 3 || matrix.GetLength(1) < 1)
            return Task.FromResult(new OutlierReport { TotalRows = nRows });

        using var client = new InsightClient();

        // Isolation Forest
        IsolationForestReport? ifReport = null;
        try
        {
            var ifResult = client.IsolationForest(matrix, contamination: options.OutlierContamination);
            ifReport = new IsolationForestReport
            {
                Scores = ifResult.Scores,
                Anomalies = ifResult.Anomalies.Select(b => b != 0).ToArray(),
                AnomalyCount = ifResult.AnomalyCount,
                Threshold = ifResult.Threshold
            };
        }
        catch { }

        // LOF
        LofReport? lofReport = null;
        try
        {
            var lofResult = client.Lof(matrix);
            lofReport = new LofReport
            {
                Scores = lofResult.Scores,
                Anomalies = lofResult.Anomalies.Select(b => b != 0).ToArray(),
                AnomalyCount = lofResult.AnomalyCount,
                Threshold = lofResult.Threshold
            };
        }
        catch { }

        // Mahalanobis
        MahalanobisReport? mahReport = null;
        try
        {
            var mahResult = client.Mahalanobis(matrix);
            mahReport = new MahalanobisReport
            {
                Distances = mahResult.Distances,
                Anomalies = mahResult.Anomalies.Select(b => b != 0).ToArray(),
                OutlierCount = mahResult.OutlierCount,
                Threshold = mahResult.Threshold
            };
        }
        catch { }

        // 앙상블 이상치 카운트 (2/3 이상이 이상치로 판정한 경우)
        int outlierCount = 0;
        for (int i = 0; i < nRows; i++)
        {
            int votes = 0;
            if (ifReport?.Anomalies != null && i < ifReport.Anomalies.Length && ifReport.Anomalies[i]) votes++;
            if (lofReport?.Anomalies != null && i < lofReport.Anomalies.Length && lofReport.Anomalies[i]) votes++;
            if (mahReport?.Anomalies != null && i < mahReport.Anomalies.Length && mahReport.Anomalies[i]) votes++;
            if (votes >= 2) outlierCount++;
        }

        return Task.FromResult(new OutlierReport
        {
            IsolationForest = ifReport,
            Lof = lofReport,
            Mahalanobis = mahReport,
            TotalRows = nRows,
            OutlierCount = outlierCount,
            OutlierPercentage = nRows > 0 ? (double)outlierCount / nRows * 100 : 0
        });
    }
}
