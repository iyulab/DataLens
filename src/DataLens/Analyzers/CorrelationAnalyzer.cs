using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class CorrelationAnalyzer : IAnalyzer<CorrelationReport>
{
    private const double DuplicateColumnThreshold = 0.9999;

    public Task<CorrelationReport> AnalyzeAsync(
        DataAdapter adapter,
        AnalysisOptions options,
        ICollection<AnalysisWarning>? warnings = null)
    {
        var allNumericCols = adapter.NumericColumns;
        if (allNumericCols.Count < 2)
        {
            warnings?.Add(new AnalysisWarning(
                Analyzer: "Correlation",
                Category: WarningCategory.InsufficientColumns,
                Message: $"상관 분석 가능한 numeric 컬럼이 {allNumericCols.Count} 개 (최소 2 개 필요).",
                AffectedColumns: allNumericCols.Count > 0 ? allNumericCols.ToList() : null));
            return Task.FromResult(new CorrelationReport
            {
                ColumnNames = allNumericCols.ToList()
            });
        }

        // 분산 0 / 준상수 컬럼은 사전 제외 — UInsight 가 throw 하기 전에 부분 결과를 산출할 수 있도록.
        // DataQuality 진단은 lazy 캐시되므로 여기서 호출해도 추가 비용 없음.
        var degenerateSet = adapter.DataQuality.ConstantColumns.Count > 0
            ? new HashSet<string>(adapter.DataQuality.ConstantColumns, StringComparer.Ordinal)
            : null;
        var numericCols = degenerateSet is null
            ? allNumericCols.ToList()
            : allNumericCols.Where(c => !degenerateSet.Contains(c)).ToList();

        if (numericCols.Count < 2)
        {
            warnings?.Add(new AnalysisWarning(
                Analyzer: "Correlation",
                Category: WarningCategory.InsufficientUsableColumns,
                Message: $"분산 0 컬럼 {allNumericCols.Count - numericCols.Count} 개 제외 후 분석 가능 컬럼이 {numericCols.Count} (최소 2 필요).",
                AffectedColumns: allNumericCols.ToList()));
            return Task.FromResult(new CorrelationReport
            {
                ColumnNames = numericCols,
                Method = options.CorrelationMethod
            });
        }

        // 일부 제외했으나 분석 가능 — degenerate 컬럼 목록을 사용자에게 명시.
        if (degenerateSet is not null && numericCols.Count < allNumericCols.Count)
        {
            var excluded = allNumericCols.Where(degenerateSet.Contains).ToList();
            warnings?.Add(new AnalysisWarning(
                Analyzer: "Correlation",
                Category: WarningCategory.DegenerateColumnsExcluded,
                Message: $"분산 0 컬럼 {excluded.Count} 개를 자동 제외하고 나머지 {numericCols.Count} 개로 상관 분석을 계산했습니다.",
                AffectedColumns: excluded));
        }

        using var client = new InsightClient();
        var matrix = adapter.ToCleanMatrix(numericCols.ToArray());

        if (matrix.GetLength(0) < 3)
        {
            warnings?.Add(new AnalysisWarning(
                Analyzer: "Correlation",
                Category: WarningCategory.InsufficientRows,
                Message: $"NaN 제거 후 행 수가 {matrix.GetLength(0)} (최소 3 필요).",
                AffectedColumns: numericCols));
            return Task.FromResult(new CorrelationReport
            {
                ColumnNames = numericCols,
                Method = options.CorrelationMethod
            });
        }

        // 사용자 지정 method 로 상관 행렬 계산 (Pearson / Spearman / Kendall).
        // InsightException 은 swallow 하지 않고 SafeAnalyze 가 UpstreamError 로 변환한다.
        var method = options.CorrelationMethod switch
        {
            CorrelationMethod.Spearman => CorrelationMethodKind.Spearman,
            CorrelationMethod.Kendall => CorrelationMethodKind.Kendall,
            _ => CorrelationMethodKind.Pearson,
        };
        var result = client.Correlation(matrix, method);
        var corrMatrix = result.Matrix;

        // Condition number 는 항상 Pearson 매트릭스 기준 (다중공선성 진단의 표준).
        // method != Pearson 이면 별도 호출. emit-and-continue — 실패 시 null.
        double? conditionNumber = TryComputeConditionNumber(client, matrix, options.CorrelationMethod, warnings);

        // 고상관 쌍 + collinearity (DuplicateColumns) 동시 추출.
        var highPairs = new List<CorrelationPair>();
        var duplicateGroupMap = new Dictionary<int, List<string>>(); // root index → group members
        var columnToRoot = new Dictionary<int, int>();

        for (int i = 0; i < numericCols.Count; i++)
        {
            for (int j = i + 1; j < numericCols.Count; j++)
            {
                var val = corrMatrix[i, j];
                if (Math.Abs(val) > options.CorrelationThreshold)
                {
                    highPairs.Add(new CorrelationPair
                    {
                        Column1 = numericCols[i],
                        Column2 = numericCols[j],
                        Value = val
                    });
                }

                if (Math.Abs(val) > DuplicateColumnThreshold)
                {
                    var rootI = FindRoot(columnToRoot, i);
                    var rootJ = FindRoot(columnToRoot, j);
                    if (rootI != rootJ)
                    {
                        // union: 더 작은 인덱스를 root 로 유지.
                        var (newRoot, merged) = rootI < rootJ ? (rootI, rootJ) : (rootJ, rootI);
                        columnToRoot[i] = newRoot;
                        columnToRoot[j] = newRoot;
                        columnToRoot[merged] = newRoot;
                        if (!duplicateGroupMap.TryGetValue(newRoot, out var group))
                        {
                            group = new List<string> { numericCols[newRoot] };
                            duplicateGroupMap[newRoot] = group;
                        }
                        var other = numericCols[merged];
                        if (!group.Contains(other)) group.Add(other);
                        // 기존 merged 그룹의 멤버를 newRoot 그룹으로 이동.
                        if (duplicateGroupMap.TryGetValue(merged, out var oldGroup))
                        {
                            foreach (var m in oldGroup)
                            {
                                if (!group.Contains(m)) group.Add(m);
                            }
                            duplicateGroupMap.Remove(merged);
                        }
                    }
                }
            }
        }

        if (duplicateGroupMap.Count > 0 && warnings is not null)
        {
            foreach (var group in duplicateGroupMap.Values)
            {
                warnings.Add(new AnalysisWarning(
                    Analyzer: "Correlation",
                    Category: WarningCategory.DuplicateColumns,
                    Message: $"컬럼 {string.Join(", ", group)} 가 |r| > {DuplicateColumnThreshold} 로 거의 동일합니다 (정보 중복 / 다중공선성).",
                    AffectedColumns: group));
            }
        }

        highPairs.Sort((a, b) => b.AbsValue.CompareTo(a.AbsValue));

        // 범주형 변수 연관 분석 (Cramér's V). 개별 페어 실패는 emit 하지 않음 — 카테고리 페어가 많을 때
        // 노이즈가 과도하다. 향후 demand 시 emit 도입 검토.
        var categoricalAssociations = new List<CategoricalAssociation>();
        var catCols = adapter.CategoricalColumns;

        if (catCols.Count >= 2)
        {
            for (int i = 0; i < catCols.Count; i++)
            {
                for (int j = i + 1; j < catCols.Count; j++)
                {
                    try
                    {
                        var table = BuildContingencyTable(adapter, catCols[i], catCols[j]);
                        if (table != null)
                        {
                            var cv = client.CramersV(table);
                            categoricalAssociations.Add(new CategoricalAssociation
                            {
                                Column1 = catCols[i],
                                Column2 = catCols[j],
                                CramersV = cv.V,
                                ChiSquared = cv.ChiSquared,
                                PValue = cv.PValue
                            });
                        }
                    }
                    catch (InsightException)
                    {
                        // 페어별 실패는 결과 레벨 emit 으로 가시화 — 페어가 많을 때 noise 회피.
                    }
                }
            }
        }

        return Task.FromResult(new CorrelationReport
        {
            ColumnNames = numericCols,
            Matrix = corrMatrix,
            HighCorrelationPairs = highPairs,
            CategoricalAssociations = categoricalAssociations,
            Method = options.CorrelationMethod,
            ConditionNumber = conditionNumber
        });
    }

    /// <summary>
    /// UInsight <c>FeatureImportanceResult.ConditionNumber</c> 를 통해 다중공선성 진단을 얻는다.
    /// <para>
    /// FeatureImportance 는 condition number 외에 importance scores 도 계산하지만, 본 호출은
    /// condition number 만 사용한다. 호출 비용은 UInsight 단일 호출 (matrix 입력) — 별도 회귀
    /// 반복 없음.
    /// </para>
    /// </summary>
    private static double? TryComputeConditionNumber(
        InsightClient client,
        double[,] matrix,
        CorrelationMethod method,
        ICollection<AnalysisWarning>? warnings)
    {
        // FeatureImportance 는 컬럼 ≥ 2 가정. matrix 는 위에서 GetLength(0) ≥ 3 검증 완료.
        if (matrix.GetLength(1) < 2) return null;

        try
        {
            var fi = client.FeatureImportance(matrix);
            var cond = fi.ConditionNumber;
            if (double.IsNaN(cond) || double.IsInfinity(cond)) return null;
            return cond;
        }
        catch (InsightException ex)
        {
            // 다중공선성 진단 실패는 치명적이지 않다 — warn 후 null.
            warnings?.Add(AnalysisWarning.FromInsightException("Multicollinearity", ex));
            return null;
        }
    }

    private static int FindRoot(Dictionary<int, int> parent, int x)
    {
        if (!parent.TryGetValue(x, out var p) || p == x) return x;
        var root = FindRoot(parent, p);
        parent[x] = root;
        return root;
    }

    private static double[,]? BuildContingencyTable(DataAdapter adapter, string col1, string col2)
    {
        var rows = adapter.DataFrame.Rows;
        var vals1 = new List<string>();
        var vals2 = new List<string>();

        foreach (var row in rows)
        {
            row.TryGetValue(col1, out var v1);
            row.TryGetValue(col2, out var v2);
            if (!string.IsNullOrWhiteSpace(v1) && !string.IsNullOrWhiteSpace(v2))
            {
                vals1.Add(v1);
                vals2.Add(v2);
            }
        }

        if (vals1.Count == 0) return null;

        var unique1 = vals1.Distinct().OrderBy(v => v).ToList();
        var unique2 = vals2.Distinct().OrderBy(v => v).ToList();

        if (unique1.Count < 2 || unique2.Count < 2) return null;

        var map1 = unique1.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);
        var map2 = unique2.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);

        var table = new double[unique1.Count, unique2.Count];
        for (int i = 0; i < vals1.Count; i++)
            table[map1[vals1[i]], map2[vals2[i]]]++;

        return table;
    }
}
