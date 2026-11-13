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
    /// 상관 분석 방법.
    /// <list type="bullet">
    /// <item><see cref="CorrelationMethod.Pearson"/>: 선형 관계 (정규/등분산 가정). 기본값.</item>
    /// <item><see cref="CorrelationMethod.Spearman"/>: 단조 관계 (rank-based, robust to outliers/skew).</item>
    /// <item><see cref="CorrelationMethod.Kendall"/>: 순서 일치도 (tau-b, ties 처리).</item>
    /// </list>
    /// </summary>
    public CorrelationMethod CorrelationMethod { get; set; } = CorrelationMethod.Pearson;

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
    /// KMeans 결과에 silhouette score 를 산출할지 여부. 비용 O(n²) — 행 수가 큰 데이터셋에서는 부담.
    /// false 면 <see cref="Models.KMeansReport.SilhouetteAvg"/>·<see cref="Models.KMeansReport.SilhouettePerSample"/> 가 null.
    /// </summary>
    public bool ComputeSilhouette { get; set; } = true;

    /// <summary>
    /// silhouette 산출을 자동 skip 하는 행 수 임계값. 행 수가 이 값을 초과하면 <see cref="ComputeSilhouette"/> 가 true 여도 skip.
    /// 0 이면 임계 없음 (항상 산출). O(n²) 비용 보호용.
    /// </summary>
    public uint SilhouetteRowThreshold { get; set; } = 50_000;

    /// <summary>
    /// 행 수에 이차인 변화점 탐지(PELT)를 자동 skip 하는 행 수 임계값 — 다변량(모든 숫자 컬럼 동시) 탐지와 <see cref="ChangepointCost.Normal"/>
    /// 비용의 컬럼별 탐지에 걸린다. 변화점이 드문 데이터에서 그 둘은 가지치기가 듣지 않아 O(n²)이다(숫자 4 컬럼 50,000 행 17 초).
    /// <see cref="ChangepointCost.L2"/> 비용의 컬럼별 탐지는 평균 함수형 가지치기로 근-선형이라(UAnalytics 0.27 — 50,000 행 0.1 초) 임계 없이 언제나 돈다.
    /// 행 수가 이 값을 초과하면 해당 탐지를 건너뛰고 <see cref="Models.AnalysisWarning"/>(<see cref="Models.WarningCategory.SkippedForSize"/>)으로 알린다. 0 이면 임계 없음.
    /// </summary>
    public uint ChangepointRowThreshold { get; set; } = 20_000;

    /// <summary>
    /// HDBSCAN 을 자동 skip 하는 행 수 임계값. HDBSCAN 은 행 수에 O(n²) 시간이다(UInsight 0.39 부터 메모리는 O(n) —
    /// 숫자 4 컬럼에서 10,000 행 1.5 초, 20,000 행 11 초). 행 수가 이 값을 초과하면
    /// <see cref="IncludeClustering"/> 가 true 여도 HDBSCAN 은 건너뛰고 <see cref="Models.ClusterReport.Hdbscan"/> 가 null 이며
    /// <see cref="Models.AnalysisWarning"/>(<see cref="Models.WarningCategory.SkippedForSize"/>)으로 알린다. 0 이면 임계 없음.
    /// </summary>
    public uint HdbscanRowThreshold { get; set; } = 20_000;

    /// <summary>
    /// 모든 점 쌍(또는 이웃)의 거리를 계산하는 분석 — LOF · DBSCAN · HDBSCAN · silhouette · 다변량 변화점 — 을 자동 skip 하는 작업량 임계값: 행 수² × 열 수.
    /// 위의 행 수 임계는 열이 적을 때를 기준으로 정해져 있어, 같은 2만 행이라도 200 열이면 20 배 무겁다
    /// (2만 행 × 200 열, 단일 스레드: LOF 184 초 · DBSCAN 65 초 · silhouette 64 초 · HDBSCAN 4 분 · 다변량 변화점 6 분 — <c>mloop info --analyze</c> 가 분 단위).
    /// 작업량이 이 값을 넘고 열이 7 개를 넘으면(LOF 는 12 개를 넘으면) 건너뛰고 <see cref="Models.AnalysisWarning"/>(<see cref="Models.WarningCategory.SkippedForSize"/>)으로 알린다 —
    /// LOF 가 건너뛰면 <see cref="Models.OutlierReport.Lof"/> 가 null 이고 앙상블은 나머지 알고리즘만으로 판정한다.
    /// 열이 그 이하면 k-d tree 가 이웃을 행 수에 가깝게 찾아 이 임계는 보지 않는다(행 수 임계만 적용). 이득은 LOF 가 12 열 부근에서, 나머지는 7 열 부근에서 끝난다 —
    /// 실측 LOF(10 만 행, u-insight 0.43.1): 7 열 7 초 · 8 열 7 초 · 10 열 16 초 · 12 열 26 초 · 14 열 36 초; 4 열 100 만 행은 113 초(메모리 628 MB — 0.42.0 측정).
    /// 기본값 2×10¹⁰ 은 2만 행 × 50 열(10 초 안팎)을 허용하고 2만 행 × 200 열을 건너뛴다. 0 이면 임계 없음.
    /// </summary>
    public ulong PairwiseWorkThreshold { get; set; } = 20_000_000_000;

    /// <summary>
    /// gap statistic(최적 군집 수 탐색)이 읽는 최대 행 수. 비용이 행 수 × 열 수 × 후보 K × 참조 표본에 비례한다(2만 행 × 200 열: 2 분).
    /// 행 수가 이 값을 넘으면 고르게 떨어진 이 행 수만큼의 행(결정적)만으로 K 를 고르고 <see cref="Models.AnalysisWarning"/>
    /// (<see cref="Models.WarningCategory.SkippedForSize"/>)으로 알린다 — 이후의 K-Means 자체는 모든 행을 쓴다. 0 이면 상한 없음.
    /// </summary>
    public uint GapStatisticMaxRows { get; set; } = 5_000;

    /// <summary>
    /// 계층적 군집을 자동 skip 하는 행 수 임계값. 계층적 군집은 압축 거리 행렬 n(n−1)/2 × 8 바이트가 필요하다
    /// (10,000 행 ≈ 400 MB, 50,000 행 ≈ 10 GB). 행 수가 이 값을 초과하면 건너뛰고
    /// <see cref="Models.ClusterReport.Hierarchical"/> 가 null 이며 <see cref="Models.WarningCategory.SkippedForSize"/> 경고로 알린다.
    /// 0 이면 임계 없음.
    /// </summary>
    public uint HierarchicalRowThreshold { get; set; } = 10_000;

    /// <summary>
    /// HDBSCAN 최소 클러스터 크기.
    /// </summary>
    public uint HdbscanMinClusterSize { get; set; } = 5;

    /// <summary>
    /// HDBSCAN 최소 샘플 수 (밀도 추정). 0이면 MinClusterSize와 동일하게 사용.
    /// </summary>
    public uint HdbscanMinSamples { get; set; } = 0;

    /// <summary>
    /// PELT 변화점 탐지 비용 함수. <see cref="DataLens.ChangepointCost.L2"/> = 평균 변화,
    /// <see cref="DataLens.ChangepointCost.Normal"/> = 평균+분산 변화.
    /// </summary>
    public ChangepointCost ChangepointCost { get; set; } = ChangepointCost.L2;

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
    /// <c>EnumerableSourceOptions&lt;T&gt;.IncludeProperties</c> 와 의미가 다르다 — 후자는 DataFrame
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
