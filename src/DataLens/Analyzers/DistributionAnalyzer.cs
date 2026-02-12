using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class DistributionAnalyzer : IAnalyzer<DistributionReport>
{
    public Task<DistributionReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
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
                    IsNormal = result.IsNormal
                });
            }
            catch
            {
                // 분포 분석 실패 시 무시
            }
        }

        return Task.FromResult(new DistributionReport { Columns = columns });
    }
}
