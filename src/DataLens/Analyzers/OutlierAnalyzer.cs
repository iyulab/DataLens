using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class OutlierAnalyzer : IAnalyzer<OutlierReport>
{
    public Task<OutlierReport> AnalyzeAsync(
        DataAdapter adapter,
        AnalysisOptions options,
        ICollection<AnalysisWarning>? warnings = null)
    {
        // 이상치 탐지는 모든 행이 필요 → 결측값 중앙값 대체 (정규화는 안 함, 원본 스케일에서 탐지)
        var matrix = adapter.ToImputedMatrix();
        int nRows = matrix.GetLength(0);
        if (nRows < 3 || matrix.GetLength(1) < 1)
        {
            warnings?.Add(new AnalysisWarning(
                Analyzer: "Outlier",
                Category: WarningCategory.InsufficientRows,
                Message: $"이상치 탐지 입력 행 수가 {nRows}, 컬럼 수가 {matrix.GetLength(1)} (최소 3 행, 1 컬럼 필요)."));
            return Task.FromResult(new OutlierReport { TotalRows = nRows });
        }

        // Mahalanobis 사전 진단: constant 컬럼이 있으면 공분산 행렬이 거의 확실히 singular.
        var qualityReport = adapter.DataQuality;
        if (qualityReport.HasSingularCovarianceRisk)
        {
            warnings?.Add(new AnalysisWarning(
                Analyzer: "Mahalanobis",
                Category: WarningCategory.SingularCovariance,
                Message: $"분산 0 컬럼 {qualityReport.ConstantColumns.Count} 개 발견 — Mahalanobis 공분산 행렬이 특이일 수 있습니다.",
                AffectedColumns: qualityReport.ConstantColumns));
        }

        using var client = new InsightClient();

        // 알고리즘별 emit-and-continue: 한 알고리즘 실패가 다른 알고리즘 실행을 막지 않는다.
        var ifReport = TryRun("IsolationForest", warnings, () =>
        {
            var ifResult = client.IsolationForest(matrix, contamination: options.OutlierContamination);
            return new IsolationForestReport
            {
                Scores = ifResult.Scores,
                Anomalies = ifResult.Anomalies.Select(b => b != 0).ToArray(),
                AnomalyCount = ifResult.AnomalyCount,
                Threshold = ifResult.Threshold
            };
        });

        var lofReport = TryRun("Lof", warnings, () =>
        {
            var lofResult = client.Lof(matrix);
            return new LofReport
            {
                Scores = lofResult.Scores,
                Anomalies = lofResult.Anomalies.Select(b => b != 0).ToArray(),
                AnomalyCount = lofResult.AnomalyCount,
                Threshold = lofResult.Threshold
            };
        });

        var mahReport = TryRun("Mahalanobis", warnings, () =>
        {
            var mahResult = client.Mahalanobis(matrix);
            return new MahalanobisReport
            {
                Distances = mahResult.Distances,
                Anomalies = mahResult.Anomalies.Select(b => b != 0).ToArray(),
                OutlierCount = mahResult.OutlierCount,
                Threshold = mahResult.Threshold
            };
        });

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

        // 단변량 (per-column) outlier — 다변량 결과와 직교한 시각으로 보강.
        // 원본 (imputation 전) 컬럼 값을 사용하여 결측 행을 생략한 fence 계산.
        var univariate = ComputeUnivariateOutliers(adapter);

        return Task.FromResult(new OutlierReport
        {
            IsolationForest = ifReport,
            Lof = lofReport,
            Mahalanobis = mahReport,
            Univariate = univariate,
            TotalRows = nRows,
            OutlierCount = outlierCount,
            OutlierPercentage = nRows > 0 ? (double)outlierCount / nRows * 100 : 0
        });
    }

    private static UnivariateOutlierReport ComputeUnivariateOutliers(DataAdapter adapter)
    {
        var report = new UnivariateOutlierReport();
        foreach (var col in adapter.NumericColumns)
        {
            var values = adapter.ToArray(col); // 결측은 NaN — detector 내부에서 finite-only 필터링
            report.Tukey[col] = UnivariateOutlierDetector.Tukey(values);
            report.ThreeSigma[col] = UnivariateOutlierDetector.ThreeSigma(values);
            report.Hampel[col] = UnivariateOutlierDetector.Hampel(values);
        }
        return report;
    }

    /// <summary>
    /// 알고리즘별 단일 호출 wrapper. 실패 시 emit + null 반환 (re-throw 금지 — 다른 알고리즘 실행 보호).
    /// </summary>
    private static T? TryRun<T>(
        string algorithmName,
        ICollection<AnalysisWarning>? warnings,
        Func<T> run) where T : class
    {
        try
        {
            return run();
        }
        catch (InsightException ex)
        {
            warnings?.Add(AnalysisWarning.FromInsightException(algorithmName, ex));
            return null;
        }
        catch (Exception ex)
        {
            warnings?.Add(new AnalysisWarning(
                Analyzer: algorithmName,
                Category: WarningCategory.ComputationFailed,
                Message: ex.Message));
            return null;
        }
    }
}
