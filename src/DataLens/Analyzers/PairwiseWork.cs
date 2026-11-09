using DataLens.Models;

namespace DataLens.Analyzers;

/// <summary>
/// 모든 점 쌍(또는 이웃)의 거리를 계산하는 분석(LOF · DBSCAN · HDBSCAN · silhouette · 다변량 변화점)의 작업량 보호.
/// 비용은 행 수² × 열 수다 — 행 수 임계만으로는 200 열 표(같은 2만 행이 20 배)를 거르지 못한다.
/// </summary>
internal static class PairwiseWork
{
    /// <summary>이 열 수 이하면 k-d tree 가 이웃을 행 수에 가깝게 찾아 행 수 임계만 본다 — 6 열(LOF 10 만 행 31 초)까지; 8 열부터(109 초)는 작업량이 비용을 말한다.</summary>
    internal const int TreeFriendlyColumns = 6;

    /// <summary>행² × 열이 <see cref="AnalysisOptions.PairwiseWorkThreshold"/> 를 넘는가 (열이 <see cref="TreeFriendlyColumns"/> 이하면 아니오).</summary>
    internal static bool Exceeds(AnalysisOptions options, int rows, int columns) =>
        options.PairwiseWorkThreshold != 0
        && columns > TreeFriendlyColumns
        && Work(rows, columns) > options.PairwiseWorkThreshold;

    /// <summary>넘으면 경고를 남기고 true(건너뜀).</summary>
    internal static bool Skip(string analyzer, AnalysisOptions options, int rows, int columns, ICollection<AnalysisWarning>? warnings, string? what = null)
    {
        if (!Exceeds(options, rows, columns))
            return false;
        warnings?.Add(new AnalysisWarning(
            Analyzer: analyzer,
            Category: WarningCategory.SkippedForSize,
            Message: $"행 {rows} × 열 {columns} 의 쌍별 거리 작업량(행² × 열 = {Work(rows, columns):E1})이 PairwiseWorkThreshold({options.PairwiseWorkThreshold}) 를 넘어 {what ?? analyzer} 를 건너뜀. PairwiseWorkThreshold = 0 이면 항상 실행."));
        return true;
    }

    // double: 행² 이 long 을 넘을 수 있고 비교에는 정밀도가 필요 없다.
    private static double Work(int rows, int columns) => (double)rows * rows * columns;
}
