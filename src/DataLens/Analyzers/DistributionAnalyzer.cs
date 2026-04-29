using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class DistributionAnalyzer : IAnalyzer<DistributionReport>
{
    public Task<DistributionReport> AnalyzeAsync(
        DataAdapter adapter,
        AnalysisOptions options,
        ICollection<AnalysisWarning>? warnings = null)
    {
        using var client = new InsightClient();
        var columns = new List<ColumnDistribution>();

        foreach (var col in adapter.NumericColumns)
        {
            try
            {
                var data = adapter.ToCleanArray(col);
                if (data.Length < 8) continue; // 정규성 검정에는 최소 샘플 수 필요

                var result = client.Distribution(data, options.SignificanceLevel);
                var (skew, kurtosis) = ComputeSkewnessKurtosis(data);
                var shape = ClassifyShape(result.IsNormal, skew, kurtosis);
                columns.Add(new ColumnDistribution
                {
                    Name = col,
                    SampleSize = result.N,
                    KsStatistic = result.KsStatistic,
                    KsPValue = result.KsPValue,
                    JbStatistic = result.JbStatistic,
                    JbPValue = result.JbPValue,
                    SwStatistic = result.SwStatistic,
                    SwPValue = result.SwPValue,
                    AdStatistic = result.AdStatistic,
                    AdPValue = result.AdPValue,
                    IsNormal = result.IsNormal,
                    Skewness = skew,
                    Kurtosis = kurtosis,
                    Shape = shape
                });
            }
            catch
            {
                // 분포 분석 실패 시 무시
            }
        }

        return Task.FromResult(new DistributionReport { Columns = columns });
    }

    /// <summary>
    /// 표본 왜도 (g1) + excess 첨도 (g2 - 3) 계산. 모집단 가정 없는 g1, g2 형태 (numpy.skew/kurtosis 와 일치).
    /// </summary>
    private static (double? skew, double? kurtosis) ComputeSkewnessKurtosis(double[] data)
    {
        if (data.Length < 4) return (null, null);

        double n = data.Length;
        double mean = 0;
        for (int i = 0; i < data.Length; i++) mean += data[i];
        mean /= n;

        double m2 = 0, m3 = 0, m4 = 0;
        for (int i = 0; i < data.Length; i++)
        {
            var d = data[i] - mean;
            var d2 = d * d;
            m2 += d2;
            m3 += d2 * d;
            m4 += d2 * d2;
        }
        m2 /= n;
        m3 /= n;
        m4 /= n;

        if (m2 <= 0) return (null, null); // 분산 0 — 분류 불가.
        var stdCubed = Math.Pow(m2, 1.5);
        var skew = m3 / stdCubed;
        var excessKurt = m4 / (m2 * m2) - 3.0;
        return (skew, excessKurt);
    }

    /// <summary>
    /// IsNormal 우선 적용. 비정규일 때 skewness/kurtosis 임계값으로 분기.
    /// </summary>
    private static DistributionShape ClassifyShape(bool isNormal, double? skew, double? kurtosis)
    {
        if (isNormal) return DistributionShape.NormalLike;
        if (skew is null || kurtosis is null) return DistributionShape.Unknown;

        // 임계값: Bulmer (1979) "Principles of Statistics" + 일반 EDA 관행.
        if (skew > 0.5) return DistributionShape.RightSkewed;
        if (skew < -0.5) return DistributionShape.LeftSkewed;
        if (kurtosis > 1.0) return DistributionShape.HeavyTailed;
        if (kurtosis < -1.0) return DistributionShape.Platykurtic;
        // 정규성 검정 reject 됐지만 왜도/첨도가 모두 임계 안 → 라벨 부여 보류.
        return DistributionShape.Unknown;
    }
}
