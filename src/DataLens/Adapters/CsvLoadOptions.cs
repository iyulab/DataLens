namespace DataLens.Adapters;

/// <summary>
/// <see cref="CsvBridge.LoadAsync"/> 동작 옵션.
/// </summary>
public class CsvLoadOptions
{
    /// <summary>
    /// 로드 시점에 중복 행을 제거할지 여부.
    /// 기본값은 <c>false</c> — 분석기(예: <c>ProfilingAnalyzer</c>)가 실제 row 수를 그대로 관찰하도록 한다.
    /// 호출자가 정합성을 의도적으로 포기하고 싶을 때만 <c>true</c> 로 켠다.
    /// </summary>
    public bool RemoveDuplicateRows { get; set; } = false;

    public static CsvLoadOptions Default => new();
}
