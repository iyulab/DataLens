using UInsight;

namespace DataLens.Models;

/// <summary>
/// 분석 중 발생한 비치명적 경고. 해당 분석기는 null을 반환하고 파이프라인은 계속된다.
/// </summary>
public record AnalysisWarning(
    /// <summary>실패한 분석기 이름 (예: "Correlation", "Pca").</summary>
    string Analyzer,
    /// <summary>UInsight 에러 카테고리.</summary>
    InsightErrorCategory Category,
    /// <summary>원본 에러 메시지.</summary>
    string Message
);
