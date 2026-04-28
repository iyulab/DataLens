using UInsight;

namespace DataLens.Models;

/// <summary>
/// 분석 중 발생한 비치명적 경고. 해당 분석기는 부분/빈 결과를 반환하고 파이프라인은 계속된다.
/// </summary>
/// <param name="Analyzer">경고를 emit 한 분석기 또는 컴포넌트 이름 (예: "Correlation", "Mahalanobis", "EnumerableSource").</param>
/// <param name="Category">DataLens 자체 카테고리. UInsight 예외는 <see cref="WarningCategory.UpstreamError"/> 로 분류.</param>
/// <param name="Message">사람이 읽을 수 있는 설명. 가능 시 affected column / row 정보를 본문에 포함.</param>
/// <param name="AffectedColumns">사유에 직접 관련된 컬럼 이름들. UI 가 강조 표시하거나 사용자가 제외 결정에 활용.</param>
/// <param name="UpstreamCategory">UInsight 등 외부 origin 의 카테고리 (있는 경우). <see cref="WarningCategory.UpstreamError"/> 와 함께 사용.</param>
public record AnalysisWarning(
    string Analyzer,
    WarningCategory Category,
    string Message,
    IReadOnlyList<string>? AffectedColumns = null,
    InsightErrorCategory? UpstreamCategory = null
);
