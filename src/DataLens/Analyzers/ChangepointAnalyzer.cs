using DataLens.Adapters;
using DataLens.Models;
using UAnalytics;

namespace DataLens.Analyzers;

public class ChangepointAnalyzer : IAnalyzer<ChangepointReport>
{
    public Task<ChangepointReport> AnalyzeAsync(
        DataAdapter adapter,
        AnalysisOptions options,
        ICollection<AnalysisWarning>? warnings = null)
    {
        var numericColumns = adapter.NumericColumns;
        if (numericColumns.Count == 0)
            return Task.FromResult(new ChangepointReport());

        // 변화점이 드문 데이터에서 다변량 PELT 와 Normal 비용의 PELT 는 가지치기가 듣지 않아 행 수에 O(n²) — 임계를 넘으면
        // 그 탐지만 건너뛰고 알린다. L2 비용의 컬럼별 PELT 는 함수형 가지치기로 근-선형이라 언제나 돈다.
        bool tooLarge = options.ChangepointRowThreshold != 0 && (uint)adapter.RowCount > options.ChangepointRowThreshold;
        void SkippedForSize(string what) => warnings?.Add(new AnalysisWarning(
            Analyzer: "Changepoints",
            Category: WarningCategory.SkippedForSize,
            Message: $"행 수 {adapter.RowCount} 가 ChangepointRowThreshold({options.ChangepointRowThreshold}) 를 넘어 {what}을 건너뜀 (행 수에 O(n²)). ChangepointRowThreshold = 0 이면 항상 실행."));
        bool perColumn = !(tooLarge && options.ChangepointCost == ChangepointCost.Normal);
        if (!perColumn)
            SkippedForSize("Normal 비용의 컬럼별 변화점 탐지");

        // PELT는 시계열 데이터에 적용 → 각 숫자 컬럼을 독립적으로 분석
        // 결측값은 제거한 clean array 사용 (PELT는 연속 데이터 필요)
        using var client = new AnalyticsClient();
        var results = new List<ColumnChangepointResult>();

        foreach (var col in perColumn ? numericColumns : [])
        {
            try
            {
                var data = adapter.ToCleanArray(col);
                if (data.Length < options.ChangepointMinSegmentLength * 2)
                {
                    // 최소 2개 세그먼트 분량의 데이터가 필요
                    continue;
                }

                var peltResult = client.DetectChangepoints(
                    data,
                    penalty: Penalty(options),
                    minSegmentLen: checked((int)options.ChangepointMinSegmentLength),
                    cost: ToPeltCost(options.ChangepointCost));

                var changepoints = Indices(peltResult.Changepoints);
                var segments = BuildSegmentSummaries(data, changepoints);

                results.Add(new ColumnChangepointResult
                {
                    Name = col,
                    SampleSize = data.Length,
                    Changepoints = changepoints,
                    NSegments = checked((uint)peltResult.NSegments),
                    Segments = segments
                });
            }
            catch (Exception ex)
            {
                // 개별 컬럼 실패는 그 컬럼만 건너뛴다
                warnings?.Add(AnalysisWarning.FromException("Changepoints", ex, [col]));
            }
        }

        // 다변량 PELT (모든 숫자 컬럼 동시 분석)
        MultivariateChangepointResult? multivariate = null;
        if (numericColumns.Count >= 2 && tooLarge)
            SkippedForSize("다변량 변화점 탐지");
        else if (numericColumns.Count >= 2)
        {
            try
            {
                var matrix = adapter.ToCleanMatrix();
                int nRows = matrix.GetLength(0);
                if (nRows >= options.ChangepointMinSegmentLength * 2)
                {
                    // One signal per column: the engine takes channels, not rows.
                    int nCols = matrix.GetLength(1);
                    var signals = new double[nCols][];
                    for (int c = 0; c < nCols; c++)
                    {
                        signals[c] = new double[nRows];
                        for (int r = 0; r < nRows; r++)
                            signals[c][r] = matrix[r, c];
                    }
                    var multiResult = client.DetectChangepointsMulti(
                        signals,
                        penalty: Penalty(options),
                        minSegmentLen: checked((int)options.ChangepointMinSegmentLength),
                        cost: ToPeltCost(options.ChangepointCost));

                    multivariate = new MultivariateChangepointResult
                    {
                        SampleSize = nRows,
                        Columns = numericColumns.ToList(),
                        Changepoints = Indices(multiResult.Changepoints),
                        NSegments = checked((uint)multiResult.NSegments)
                    };
                }
            }
            catch (Exception ex)
            {
                // 다변량 실패는 단변량 결과를 막지 않는다
                warnings?.Add(AnalysisWarning.FromException("Changepoints", ex));
            }
        }

        return Task.FromResult(new ChangepointReport
        {
            Columns = results,
            Multivariate = multivariate
        });
    }

    // 0 is this library's way of asking for BIC; the engine asks for it with no penalty at all.
    private static double? Penalty(AnalysisOptions options) =>
        options.ChangepointPenalty == 0.0 ? null : options.ChangepointPenalty;

    private static uint[] Indices(IReadOnlyList<int> changepoints) =>
        [.. changepoints.Select(c => checked((uint)c))];

    private static PeltCost ToPeltCost(ChangepointCost cost) => cost switch
    {
        ChangepointCost.L2 => PeltCost.L2,
        ChangepointCost.Normal => PeltCost.Normal,
        _ => throw new ArgumentOutOfRangeException(nameof(cost), cost, null),
    };

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
