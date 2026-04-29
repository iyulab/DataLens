namespace DataLens.Models;

/// <summary>
/// 단변량 outlier 검출 방법.
/// </summary>
public enum UnivariateOutlierMethod
{
    /// <summary>Tukey IQR fence (Q1 - k·IQR ~ Q3 + k·IQR). 정규성 가정 없음, robust.</summary>
    Tukey,

    /// <summary>3-Sigma rule (mean ± k·std). 정규분포 가정.</summary>
    ThreeSigma,

    /// <summary>Hampel identifier (median ± k · 1.4826 · MAD). 가장 robust.</summary>
    Hampel,
}

/// <summary>
/// 단변량 outlier 통합 결과 — 방법별로 per-column 결과를 보관한다.
/// </summary>
public class UnivariateOutlierReport
{
    /// <summary>Tukey IQR fence — 컬럼명 → 결과.</summary>
    public Dictionary<string, UnivariateOutlierResult> Tukey { get; init; } = [];

    /// <summary>3-Sigma — 컬럼명 → 결과.</summary>
    public Dictionary<string, UnivariateOutlierResult> ThreeSigma { get; init; } = [];

    /// <summary>Hampel identifier — 컬럼명 → 결과.</summary>
    public Dictionary<string, UnivariateOutlierResult> Hampel { get; init; } = [];
}

/// <summary>
/// 단일 컬럼의 단변량 outlier 결과. fence + center + spread + anomaly index 노출.
/// </summary>
public class UnivariateOutlierResult
{
    public UnivariateOutlierMethod Method { get; init; }

    /// <summary>Outlier 임계 하한.</summary>
    public double LowerFence { get; init; }

    /// <summary>Outlier 임계 상한.</summary>
    public double UpperFence { get; init; }

    /// <summary>중심 통계 (Tukey/Hampel: median, ThreeSigma: mean).</summary>
    public double Center { get; init; }

    /// <summary>퍼짐 통계 (Tukey: IQR, ThreeSigma: std, Hampel: 1.4826·MAD).</summary>
    public double Spread { get; init; }

    /// <summary>
    /// Outlier 행 인덱스 (입력 array 기준). 결측 (NaN/Inf) 행은 포함하지 않는다.
    /// </summary>
    public List<int> AnomalyIndices { get; init; } = [];

    public int AnomalyCount { get; init; }
}
