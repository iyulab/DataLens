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
    public bool IncludeChangepoints { get; set; } = true;

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

    /// <summary>
    /// MiniBatchKMeans로 자동 전환할 행 수 임계값.
    /// 데이터 행 수가 이 값 이상이면 KMeans 대신 MiniBatchKMeans 사용.
    /// 0이면 항상 KMeans 사용.
    /// </summary>
    public uint MiniBatchKMeansRowThreshold { get; set; } = 10000;

    /// <summary>
    /// HDBSCAN 최소 클러스터 크기.
    /// </summary>
    public uint HdbscanMinClusterSize { get; set; } = 5;

    /// <summary>
    /// HDBSCAN 최소 샘플 수 (밀도 추정). 0이면 MinClusterSize와 동일하게 사용.
    /// </summary>
    public uint HdbscanMinSamples { get; set; } = 0;

    /// <summary>
    /// PELT 변화점 탐지 비용 함수. 0 = L2 (mean change), 1 = Normal (mean+variance).
    /// </summary>
    public uint ChangepointCost { get; set; } = 0;

    /// <summary>
    /// PELT 변화점 패널티. 0.0이면 BIC 자동 계산.
    /// </summary>
    public double ChangepointPenalty { get; set; } = 0.0;

    /// <summary>
    /// PELT 최소 세그먼트 길이 (>= 2).
    /// </summary>
    public uint ChangepointMinSegmentLength { get; set; } = 2;

    /// <summary>
    /// 분석 대상 컬럼 allowlist (DataFrame 적재 후 단계).
    /// null/empty 면 모든 컬럼이 분석 대상. 지정 시 이 목록 외 컬럼은 numeric/categorical 분류에서 제외된다.
    /// </summary>
    /// <remarks>
    /// <see cref="EnumerableSourceOptions{T}.IncludeProperties"/> 와 의미가 다르다 — 후자는 DataFrame
    /// 으로 적재할 *POCO 속성* 을 제한, 전자는 분석에 사용할 *DataFrame 컬럼* 을 제한한다.
    /// CSV/JSON 파일 입력에서도 일관 동작한다.
    /// </remarks>
    public IReadOnlyList<string>? IncludeColumns { get; set; }

    /// <summary>
    /// 분석 대상 컬럼 denylist (DataFrame 적재 후 단계).
    /// ID/FK 식별자 컬럼처럼 적재는 하되 분석에서만 빼야 하는 컬럼에 사용한다.
    /// <see cref="IncludeColumns"/> 와 동시 지정 시 둘 다 적용 (Include 통과 + Exclude 미해당).
    /// </summary>
    public IReadOnlyList<string>? ExcludeColumns { get; set; }

    public static AnalysisOptions Default => new();
}
