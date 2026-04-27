using System.Globalization;
using System.Text.Json;

namespace DataLens.Adapters;

/// <summary>
/// CLR 값 / <see cref="JsonElement"/> 를 FilePrepper DataFrame 이 소비하는 문자열 표현으로 변환하는 단일 출처 매퍼.
/// 구현은 demand-driven minimal subset 에 한정한다 — numeric / decimal / bool / string / DateTime / Nullable&lt;T&gt;.
/// 그 외 타입(Guid / Enum / DateOnly / TimeOnly / DateTimeOffset / TimeSpan / Uri / IPAddress 등)은 string fallback + warning.
/// </summary>
internal static class DefaultTypeMapper
{
    /// <summary>
    /// CLR 값 → DataFrame 셀 문자열 표현. null 은 빈 문자열로 매핑(missing).
    /// </summary>
    public static string ToCell(object? value, out bool unsupportedType)
    {
        unsupportedType = false;

        if (value is null) return string.Empty;

        // Nullable<T> 는 boxing 시점에 언래핑되므로 별도 처리 불필요.
        var t = value.GetType();

        // 정확한 타입부터 분기 (JIT 가 jump-table 로 최적화).
        if (t == typeof(string)) return (string)value;
        if (t == typeof(bool)) return ((bool)value) ? "true" : "false";

        if (t == typeof(double)) return ((double)value).ToString("R", CultureInfo.InvariantCulture);
        if (t == typeof(float)) return ((float)value).ToString("R", CultureInfo.InvariantCulture);
        if (t == typeof(decimal)) return ((decimal)value).ToString(CultureInfo.InvariantCulture);

        if (t == typeof(int)) return ((int)value).ToString(CultureInfo.InvariantCulture);
        if (t == typeof(long)) return ((long)value).ToString(CultureInfo.InvariantCulture);
        if (t == typeof(short)) return ((short)value).ToString(CultureInfo.InvariantCulture);
        if (t == typeof(byte)) return ((byte)value).ToString(CultureInfo.InvariantCulture);
        if (t == typeof(sbyte)) return ((sbyte)value).ToString(CultureInfo.InvariantCulture);
        if (t == typeof(uint)) return ((uint)value).ToString(CultureInfo.InvariantCulture);
        if (t == typeof(ulong)) return ((ulong)value).ToString(CultureInfo.InvariantCulture);
        if (t == typeof(ushort)) return ((ushort)value).ToString(CultureInfo.InvariantCulture);

        if (t == typeof(DateTime))
        {
            var dt = (DateTime)value;
            // Unspecified → UTC 가정 (cross-consumer 합의 후 옵션화).
            var utc = dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            };
            return utc.ToString("o", CultureInfo.InvariantCulture);
        }

        // Demand 누적 후 승격 대상 — 현재는 ToString fallback + warning flag.
        unsupportedType = true;
        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// <see cref="JsonElement"/> → DataFrame 셀 문자열. <see cref="JsonValueKind"/> 를 보존하여
    /// number/bool/null 이 모두 동일하게 string 으로 강제되던 0.6.0 동작을 교정한다.
    /// </summary>
    public static string ToCell(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            // GetRawText 는 JSON 표기 그대로 — int/decimal 정밀도 보존.
            JsonValueKind.Number => element.GetRawText(),
            // 객체/배열은 JSON 직렬화로 fallback. 분석에는 의미 없으나 round-trip 유지.
            JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// CLR 타입이 미니멀 정책에 포함되는지 검사. <c>false</c> 면 호출자가 warning 을 발생시킬 수 있다.
    /// </summary>
    public static bool IsSupported(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return t == typeof(string)
            || t == typeof(bool)
            || t == typeof(double) || t == typeof(float) || t == typeof(decimal)
            || t == typeof(int) || t == typeof(long) || t == typeof(short)
            || t == typeof(byte) || t == typeof(sbyte)
            || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort)
            || t == typeof(DateTime);
    }
}
