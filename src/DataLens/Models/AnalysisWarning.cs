namespace DataLens.Models;

/// <summary>
/// 분석 중 발생한 비치명적 경고. 해당 분석기는 null을 반환하고 파이프라인은 계속된다.
/// </summary>
public record AnalysisWarning(
    /// <summary>실패한 분석기 이름 (예: "Correlation", "Pca").</summary>
    string Analyzer,
    /// <summary>
    /// 에러 카테고리. UInsight &gt;= 0.3.2 배포 시 세분화됨.
    /// 현재 가능한 값: AnalysisFailed, InsufficientData, InvalidParameter,
    /// DegenerateData, ComputationFailed, Unexpected.
    /// </summary>
    string Category,
    /// <summary>원본 에러 메시지.</summary>
    string Message
);
