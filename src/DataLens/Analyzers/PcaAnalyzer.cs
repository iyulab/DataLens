using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class PcaAnalyzer : IAnalyzer<PcaReport>
{
    public Task<PcaReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        // PCA는 스케일에 민감 → 결측값 대체 + Z-Score 정규화
        var matrix = adapter.ToScaledMatrix();
        int nRows = matrix.GetLength(0);
        int nCols = matrix.GetLength(1);
        if (nRows < 3 || nCols < 2)
            return Task.FromResult(new PcaReport());

        using var client = new InsightClient();

        // 자동 nComponents 선택: 전체 컬럼 수로 PCA 수행 후 threshold 기준 절단
        uint maxComponents = (uint)Math.Min(nRows, nCols);
        var result = client.Pca(matrix, maxComponents);

        // 누적 분산 비율 계산
        var explained = result.ExplainedVariance;
        var cumulative = new double[explained.Length];
        double sum = 0;
        for (int i = 0; i < explained.Length; i++)
        {
            sum += explained[i];
            cumulative[i] = sum;
        }

        // threshold 기준으로 자동 nComponents 결정
        uint autoComponents = maxComponents;
        for (int i = 0; i < cumulative.Length; i++)
        {
            if (cumulative[i] >= options.PcaVarianceThreshold)
            {
                autoComponents = (uint)(i + 1);
                break;
            }
        }

        return Task.FromResult(new PcaReport
        {
            NComponents = autoComponents,
            ExplainedVariance = explained,
            CumulativeVariance = cumulative,
            TotalExplainedVariance = sum
        });
    }
}
