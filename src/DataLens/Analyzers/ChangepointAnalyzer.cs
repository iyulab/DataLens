using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class ChangepointAnalyzer : IAnalyzer<ChangepointReport>
{
    public Task<ChangepointReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        var numericColumns = adapter.NumericColumns;
        if (numericColumns.Count == 0)
            return Task.FromResult(new ChangepointReport());

        // PELT는 시계열 데이터에 적용 → 각 숫자 컬럼을 독립적으로 분석
        // 결측값은 제거한 clean array 사용 (PELT는 연속 데이터 필요)
        using var client = new InsightClient();
        var results = new List<ColumnChangepointResult>();

        foreach (var col in numericColumns)
        {
            try
            {
                var data = adapter.ToCleanArray(col);
                if (data.Length < options.ChangepointMinSegmentLength * 2)
                {
                    // 최소 2개 세그먼트 분량의 데이터가 필요
                    continue;
                }

                var peltResult = client.Pelt(
                    data,
                    cost: options.ChangepointCost,
                    penalty: options.ChangepointPenalty,
                    minSegmentLen: options.ChangepointMinSegmentLength);

                var changepoints = peltResult.Changepoints;
                var segments = BuildSegmentSummaries(data, changepoints);

                results.Add(new ColumnChangepointResult
                {
                    Name = col,
                    SampleSize = data.Length,
                    Changepoints = changepoints,
                    NSegments = peltResult.NSegments,
                    Segments = segments
                });
            }
            catch
            {
                // 개별 컬럼 실패 시 건너뛰기 (다른 분석기와 동일한 패턴)
            }
        }

        return Task.FromResult(new ChangepointReport { Columns = results });
    }

    private static List<SegmentSummary> BuildSegmentSummaries(double[] data, uint[] changepoints)
    {
        var boundaries = new List<int> { 0 };
        foreach (var cp in changepoints)
            boundaries.Add((int)cp);
        boundaries.Add(data.Length);

        var segments = new List<SegmentSummary>();
        for (int s = 0; s < boundaries.Count - 1; s++)
        {
            int start = boundaries[s];
            int end = boundaries[s + 1];
            int length = end - start;
            if (length == 0) continue;

            double sum = 0;
            for (int i = start; i < end; i++) sum += data[i];
            double mean = sum / length;

            double ssq = 0;
            for (int i = start; i < end; i++) ssq += (data[i] - mean) * (data[i] - mean);
            double std = length > 1 ? Math.Sqrt(ssq / (length - 1)) : 0;

            segments.Add(new SegmentSummary
            {
                Start = start,
                End = end,
                Length = length,
                Mean = mean,
                StdDev = std
            });
        }

        return segments;
    }
}
