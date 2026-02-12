using DataLens.Adapters;

namespace DataLens.Analyzers;

/// <summary>
/// 분석기 공통 인터페이스. 각 분석 모듈은 이 인터페이스를 구현한다.
/// </summary>
public interface IAnalyzer<TReport>
{
    Task<TReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options);
}
