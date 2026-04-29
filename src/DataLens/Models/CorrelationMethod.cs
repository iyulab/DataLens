namespace DataLens;

/// <summary>
/// 상관 분석 방법. UInsight <c>CorrelationMethodKind</c> 의 DataLens-side 미러.
/// </summary>
public enum CorrelationMethod
{
    /// <summary>Pearson 선형 상관. 정규성·등분산 가정.</summary>
    Pearson,

    /// <summary>Spearman 순위 상관. 단조 관계 검출, 이상치·치우침에 robust.</summary>
    Spearman,

    /// <summary>Kendall tau-b. 순서 일치도, ties 처리 포함.</summary>
    Kendall,
}
