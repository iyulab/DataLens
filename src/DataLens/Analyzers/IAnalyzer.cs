using DataLens.Adapters;
using DataLens.Models;

namespace DataLens.Analyzers;

/// <summary>
/// 분석기 공통 인터페이스. 각 분석 모듈은 이 인터페이스를 구현한다.
/// </summary>
/// <typeparam name="TReport">분석 결과 모델 타입.</typeparam>
/// <remarks>
/// <paramref name="warnings"/> 컬렉션이 전달되면 분석기는 사전/사후 진단을 emit 할 수 있다.
/// null 이면 emit 채널이 없는 standalone 호출 (테스트/직접 호출용).
/// 분석기는 InsightException 을 swallow 하지 말고 호출자(<see cref="DataLensEngine"/>)에 전파해야 한다 — SafeAnalyze 가
/// <see cref="WarningCategory.UpstreamError"/> 로 변환한다. 단 <see cref="OutlierAnalyzer"/> 처럼 알고리즘별 부분 실패가
/// emit-and-continue 가 자연스러운 경우는 분석기 내부에서 직접 emit 하고 다른 알고리즘을 계속 시도한다.
/// </remarks>
public interface IAnalyzer<TReport>
{
    Task<TReport> AnalyzeAsync(
        DataAdapter adapter,
        AnalysisOptions options,
        ICollection<AnalysisWarning>? warnings = null);
}
