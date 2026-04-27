namespace DataLens;

/// <summary>
/// <see cref="DataLensEngine.Analyze{T}(System.Collections.Generic.IEnumerable{T}, AnalysisOptions?, Adapters.EnumerableSourceOptions{T}?, System.Threading.CancellationToken)"/>
/// 가 reflection 으로 컬럼을 추출할 때 무시할 속성 표식.
/// <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/> 도 동일하게 존중된다.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public sealed class DataLensIgnoreAttribute : Attribute
{
}
