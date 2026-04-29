namespace DataLens.Adapters;

/// <summary>
/// 데이터 품질 사전 진단 결과. <see cref="DataQualityScreener.Run"/> 의 반환값.
/// </summary>
/// <param name="ConstantColumns">분산 0 또는 (max - min) ≤ tolerance 인 컬럼들. 분석에 무용 + 다변량 inverse 의 singular 원인.</param>
/// <param name="CoMissingGroups">정확히 동일한 행에서 함께 결측되는 컬럼 그룹들 — 구조적 결측 패턴.</param>
/// <param name="DuplicateRowCount">완전히 동일한 행의 추가 출현 수 (첫 출현 1건은 포함하지 않음).</param>
/// <remarks>
/// DuplicateColumns (|r| ≈ 1) 는 본 screener 에서 검출하지 않는다 — 이는 Pearson 매트릭스 계산 후 가능하므로
/// <see cref="Analyzers.CorrelationAnalyzer"/> 가 매트릭스 산출 직후 자체 emit 한다 (computation 중복 회피).
/// </remarks>
public sealed record DataQualityReport(
    IReadOnlyList<string> ConstantColumns,
    IReadOnlyList<IReadOnlyList<string>> CoMissingGroups,
    int DuplicateRowCount
)
{
    public static DataQualityReport Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<IReadOnlyList<string>>(),
        0);

    /// <summary>다변량 inverse (Mahalanobis 등) 의 singular covariance 위험이 있는지.</summary>
    public bool HasSingularCovarianceRisk => ConstantColumns.Count > 0;
}

/// <summary>
/// 데이터 품질 사전 스크리너. 입력 단계에서 1회 실행되어 분석기들이 공유 참조한다.
/// </summary>
/// <remarks>
/// 비용: O(rows × numericColumns) 상수/결측 + O(rows × cols) duplicate row 해시. UInsight 호출 없이 .NET 단독.
/// <see cref="DataAdapter.DataQuality"/> 가 lazy 로 본 메서드를 호출하고 결과를 캐시.
/// </remarks>
public static class DataQualityScreener
{
    /// <summary>
    /// 데이터 품질 사전 진단을 수행한다.
    /// <list type="bullet">
    /// <item>D2: 분산 0 / 준상수 컬럼 (tolerance: max - min 임계값).</item>
    /// <item>D3: 동일 행에서 함께 결측되는 컬럼 그룹 — 구조적 결측 패턴 (3 row 이상 + 컬럼 ≥ 2).</item>
    /// <item>D4: 완전히 동일한 행의 추가 출현 수.</item>
    /// </list>
    /// </summary>
    public static DataQualityReport Run(DataAdapter adapter, double tolerance = 1e-10)
    {
        var constants = DetectConstantColumns(adapter, tolerance);
        var coMissingGroups = DetectCoMissingGroups(adapter);
        var duplicateRowCount = DetectDuplicateRows(adapter);
        return new DataQualityReport(constants, coMissingGroups, duplicateRowCount);
    }

    private static List<string> DetectConstantColumns(DataAdapter adapter, double tolerance)
    {
        var constants = new List<string>();
        foreach (var col in adapter.NumericColumns)
        {
            var values = adapter.ToCleanArray(col);
            if (values.Length == 0) continue; // 모든 값 결측 — coMissing 검출에서 처리.
            var min = double.PositiveInfinity;
            var max = double.NegativeInfinity;
            foreach (var v in values)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
            if (max - min <= tolerance) constants.Add(col);
        }
        return constants;
    }

    /// <summary>
    /// 동일한 결측 패턴(어느 행이 결측인가)을 가지는 컬럼들을 그룹으로 묶는다.
    /// 결측이 전혀 없는 컬럼 / 모든 행이 결측인 컬럼은 그룹화 대상에서 제외.
    /// 그룹 크기 ≥ 2 인 그룹만 emit.
    /// </summary>
    private static List<IReadOnlyList<string>> DetectCoMissingGroups(DataAdapter adapter)
    {
        var rows = adapter.DataFrame.Rows;
        if (rows.Count < 3) return [];

        // 컬럼별 missing pattern signature — bool[] 의 SHA-style hash 로 그룹화.
        var byPattern = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var col in adapter.AllColumns)
        {
            var pattern = new char[rows.Count];
            int missingCount = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].TryGetValue(col, out var v);
                bool missing = string.IsNullOrWhiteSpace(v);
                if (missing) missingCount++;
                pattern[i] = missing ? '1' : '0';
            }

            // 결측 0 또는 100% 인 컬럼은 패턴 비교 의미 없음.
            if (missingCount == 0 || missingCount == rows.Count) continue;

            var key = new string(pattern);
            if (!byPattern.TryGetValue(key, out var list))
            {
                list = new List<string>();
                byPattern[key] = list;
            }
            list.Add(col);
        }

        var groups = new List<IReadOnlyList<string>>();
        foreach (var members in byPattern.Values)
        {
            if (members.Count >= 2) groups.Add(members);
        }
        return groups;
    }

    /// <summary>
    /// 모든 컬럼 값이 동일한 행의 추가 출현 수를 센다 (첫 출현 1건은 미포함).
    /// </summary>
    private static int DetectDuplicateRows(DataAdapter adapter)
    {
        var rows = adapter.DataFrame.Rows;
        if (rows.Count < 2) return 0;

        var columns = adapter.AllColumns;
        if (columns.Count == 0) return 0;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        int duplicates = 0;
        foreach (var row in rows)
        {
            // 컬럼명 순서로 결합.  는 unit separator — 일반 데이터에 거의 없는 구분자.
            var sb = new System.Text.StringBuilder();
            for (int j = 0; j < columns.Count; j++)
            {
                if (j > 0) sb.Append('');
                row.TryGetValue(columns[j], out var v);
                sb.Append(v ?? string.Empty);
            }
            if (!seen.Add(sb.ToString())) duplicates++;
        }
        return duplicates;
    }
}
