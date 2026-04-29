namespace DataLens.Models;

/// <summary>
/// DataLens 자체 경고 카테고리. <see cref="AnalysisWarning"/> 의 분류 기준.
/// </summary>
/// <remarks>
/// UInsight 의 <c>InsightErrorCategory</c> 와 분리된 DataLens-owned enum. UInsight 가 throw 한 예외는
/// <see cref="UpstreamError"/> 로 분류하고 원본 카테고리는 <see cref="AnalysisWarning.UpstreamCategory"/> 에 보존한다.
/// 데이터 품질 / 입력 부적합 / 계산 정의 불가 등은 DataLens 가 자체 emit 하는 카테고리.
/// </remarks>
public enum WarningCategory
{
    /// <summary>UInsight 등 외부 라이브러리에서 throw 된 예외. <see cref="AnalysisWarning.UpstreamCategory"/> 참조.</summary>
    UpstreamError,

    /// <summary>분석에 필요한 컬럼 수가 부족 (예: 상관 분석에 numeric 컬럼 &lt; 2).</summary>
    InsufficientColumns,

    /// <summary>분석에 필요한 행 수가 부족 (예: 결측 제거 후 매트릭스 행 &lt; 3).</summary>
    InsufficientRows,

    /// <summary>공분산 행렬이 특이일 가능성 — Mahalanobis 등 inverse 가 필요한 분석 호출 전 사전 진단.</summary>
    SingularCovariance,

    /// <summary>분산 0 또는 거의 0 인 컬럼 (값이 모두 동일).</summary>
    ConstantColumns,

    /// <summary>다른 컬럼과 |r| ≈ 1 인 컬럼 페어 (정보 중복 / 다중공선성).</summary>
    DuplicateColumns,

    /// <summary>여러 컬럼이 동일한 행에서 함께 결측 — 구조적 결측 패턴 (MAR/MNAR 가능성).</summary>
    MissingnessPattern,

    /// <summary>완전히 동일한 행이 다중 존재 — 데이터 수집/조인 실수 가능성.</summary>
    DuplicateRows,

    /// <summary>모든 페어 / 모든 행에서 결과가 NaN 등 정의 불가.</summary>
    AllPairsUndefined,

    /// <summary>POCO/Dictionary → DataFrame 변환에서 지원하지 않는 타입 — ToString 으로 fallback.</summary>
    TypeMappingFailed,

    /// <summary>그 외 일반 예외 (UInsight 외부 원인). 디버깅 단서로만 사용.</summary>
    ComputationFailed,
}
