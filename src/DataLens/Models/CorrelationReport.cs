namespace DataLens.Models;

public class CorrelationReport
{
    public List<string> ColumnNames { get; init; } = [];
    public double[,]? Matrix { get; init; }
    public List<CorrelationPair> HighCorrelationPairs { get; init; } = [];
    public List<CategoricalAssociation> CategoricalAssociations { get; init; } = [];

    /// <summary>
    /// 매트릭스 산출에 사용된 상관 방법 (Pearson / Spearman / Kendall).
    /// </summary>
    public CorrelationMethod Method { get; init; } = CorrelationMethod.Pearson;

    /// <summary>
    /// Pearson 상관 행렬의 condition number (λmax / λmin). 다중공선성 진단:
    /// <list type="bullet">
    /// <item>cond &lt; 30: 안정적</item>
    /// <item>30 ≤ cond &lt; 100: 다중공선성 의심</item>
    /// <item>cond ≥ 100: 심각한 다중공선성</item>
    /// </list>
    /// UInsight <c>FeatureImportanceResult.ConditionNumber</c> 산출. <c>null</c> 이면 미산출 (행/컬럼 부족 또는 호출 실패).
    /// VIF (per-column) 는 UInsight C# 바인딩 미노출 — 후속 별도 이슈에서 처리.
    /// </summary>
    public double? ConditionNumber { get; init; }
}

public class CorrelationPair
{
    public string Column1 { get; init; } = "";
    public string Column2 { get; init; } = "";
    public double Value { get; init; }
    public double AbsValue => Math.Abs(Value);
}

public class CategoricalAssociation
{
    public string Column1 { get; init; } = "";
    public string Column2 { get; init; } = "";
    public double CramersV { get; init; }
    public double ChiSquared { get; init; }
    public double PValue { get; init; }
}
