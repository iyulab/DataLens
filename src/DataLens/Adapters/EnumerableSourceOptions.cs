namespace DataLens.Adapters;

/// <summary>
/// <see cref="DataLensEngine.Analyze{T}(System.Collections.Generic.IEnumerable{T}, AnalysisOptions?, EnumerableSourceOptions{T}?, System.Threading.CancellationToken)"/>
/// 의 컬럼 추출 정책을 제어한다.
/// </summary>
/// <typeparam name="T">소스 컬렉션의 항목 타입.</typeparam>
public class EnumerableSourceOptions<T>
{
    /// <summary>
    /// 포함할 속성 이름 화이트리스트. null 이면 reflection 기본값 + ExcludeProperties 적용.
    /// </summary>
    public IReadOnlyList<string>? IncludeProperties { get; set; }

    /// <summary>
    /// 제외할 속성 이름 블랙리스트.
    /// </summary>
    public IReadOnlyList<string>? ExcludeProperties { get; set; }

    /// <summary>
    /// 사용자 정의 컬럼 (속성 이름 대신 selector 로 추출). 지정 시 reflection 결과를 대체한다.
    /// </summary>
    public IReadOnlyList<NamedColumn<T>>? Columns { get; set; }

    /// <summary>
    /// 속성 이름 → 외부 노출 컬럼 이름 매핑. 결과 라벨링 / 한국어 헤더 등에 사용.
    /// </summary>
    public IReadOnlyDictionary<string, string>? HeaderAliases { get; set; }

    public static EnumerableSourceOptions<T> Default => new();
}

/// <summary>
/// 사용자 정의 컬럼: 외부 컬럼 이름 + <typeparamref name="T"/> 인스턴스에서 값을 추출하는 selector.
/// </summary>
public sealed class NamedColumn<T>
{
    public string Name { get; }
    public Func<T, object?> Selector { get; }

    public NamedColumn(string name, Func<T, object?> selector)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name must be non-empty.", nameof(name));
        Name = name;
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }
}
