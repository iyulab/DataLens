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

    /// <summary>
    /// CSV/TSV 파일 인코딩. 기본값 <c>"auto"</c> 는 BOM + heuristic 감지 (FilePrepper 0.7.0+).
    /// 명시 인코딩 예: <c>"utf-8"</c>, <c>"utf-8-bom"</c>, <c>"cp949"</c>, <c>"euc-kr"</c>.
    /// JSON 입력은 본 옵션의 영향을 받지 않는다 (RFC 8259 — UTF-8).
    /// </summary>
    public string Encoding { get; set; } = "auto";

    public static CsvLoadOptions Default => new();
}
