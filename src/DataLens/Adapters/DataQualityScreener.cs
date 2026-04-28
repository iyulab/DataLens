namespace DataLens.Adapters;

/// <summary>
/// 데이터 품질 사전 진단 결과. <see cref="DataQualityScreener.Run"/> 의 반환값.
/// </summary>
/// <param name="ConstantColumns">분산 0 또는 (max - min) ≤ tolerance 인 컬럼들. 분석에 무용 + 다변량 inverse 의 singular 원인.</param>
/// <remarks>
/// DuplicateColumns (|r| ≈ 1) 는 본 screener 에서 검출하지 않는다 — 이는 Pearson 매트릭스 계산 후 가능하므로
/// <see cref="Analyzers.CorrelationAnalyzer"/> 가 매트릭스 산출 직후 자체 emit 한다 (computation 중복 회피).
/// </remarks>
public sealed record DataQualityReport(
    IReadOnlyList<string> ConstantColumns
)
{
    public static DataQualityReport Empty { get; } = new(Array.Empty<string>());

    /// <summary>다변량 inverse (Mahalanobis 등) 의 singular covariance 위험이 있는지.</summary>
    public bool HasSingularCovarianceRisk => ConstantColumns.Count > 0;
}

/// <summary>
/// 데이터 품질 사전 스크리너. 입력 단계에서 1회 실행되어 분석기들이 공유 참조한다.
/// </summary>
/// <remarks>
/// 비용: O(rows × numericColumns). UInsight 호출 없이 .NET 단독으로 실행. <see cref="DataAdapter.DataQuality"/>
/// 가 lazy 로 본 메서드를 호출하고 결과를 캐시.
/// </remarks>
public static class DataQualityScreener
{
    /// <summary>
    /// 분산 0 / 준상수 컬럼을 검출한다. tolerance 는 (max - min) 임계값.
    /// </summary>
    public static DataQualityReport Run(DataAdapter adapter, double tolerance = 1e-10)
    {
        var constants = new List<string>();
        foreach (var col in adapter.NumericColumns)
        {
            var values = adapter.ToCleanArray(col);
            if (values.Length == 0)
            {
                // 결측만 있는 컬럼은 별도 카테고리(향후 D3) — 여기서는 무시.
                continue;
            }
            var min = double.PositiveInfinity;
            var max = double.NegativeInfinity;
            foreach (var v in values)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (max - min <= tolerance) constants.Add(col);
        }
        return new DataQualityReport(constants);
    }
}
