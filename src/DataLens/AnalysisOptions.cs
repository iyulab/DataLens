namespace DataLens;

/// <summary>
/// 분석 실행 옵션. 어떤 분석을 포함/제외할지 제어한다.
/// </summary>
public class AnalysisOptions
{
    public bool IncludeProfiling { get; set; } = true;
    public bool IncludeDescriptive { get; set; } = true;
    public bool IncludeCorrelation { get; set; } = true;
    public bool IncludeRegression { get; set; } = true;
    public bool IncludeClustering { get; set; } = true;
    public bool IncludeOutliers { get; set; } = true;
    public bool IncludeDistribution { get; set; } = true;
    public bool IncludeFeatures { get; set; } = true;
    public bool IncludePca { get; set; } = true;

    /// <summary>
    /// 상관 분석에서 "고상관"으로 간주할 임계값 (|r| > threshold).
    /// </summary>
    public double CorrelationThreshold { get; set; } = 0.7;

    /// <summary>
    /// 이상치 탐지 오염 비율 (IsolationForest 파라미터).
    /// </summary>
    public double OutlierContamination { get; set; } = 0.1;

    /// <summary>
    /// 분포 정규성 검정 유의수준.
    /// </summary>
    public double SignificanceLevel { get; set; } = 0.05;

    /// <summary>
    /// 회귀/피처 중요도의 타겟 컬럼명. null이면 자동 생략.
    /// </summary>
    public string? TargetColumn { get; set; }

    /// <summary>
    /// PCA 설명 분산 누적 비율 기준 (자동 nComponents 선택).
    /// </summary>
    public double PcaVarianceThreshold { get; set; } = 0.95;

    /// <summary>
    /// 클러스터링 최대 K (GapStatistic 탐색 범위).
    /// </summary>
    public uint MaxClusters { get; set; } = 10;

    public static AnalysisOptions Default => new();
}
