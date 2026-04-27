using System.Dynamic;
using System.Reflection;
using System.Text.Json.Serialization;
using DataLens.Models;
using FilePrepper.Pipeline;
using UInsight;

namespace DataLens.Adapters;

/// <summary>
/// <see cref="IEnumerable{T}"/> → FilePrepper <see cref="DataFrame"/> 변환기.
/// POCO / dynamic / <see cref="IDictionary{TKey, TValue}"/> / <see cref="ExpandoObject"/> 를 모두 처리한다.
/// </summary>
internal static class EnumerableSourceBuilder
{
    public static (DataFrame DataFrame, IReadOnlyList<AnalysisWarning> Warnings) Build<T>(
        IReadOnlyList<T> items,
        EnumerableSourceOptions<T>? options,
        CancellationToken cancellationToken)
    {
        options ??= EnumerableSourceOptions<T>.Default;
        cancellationToken.ThrowIfCancellationRequested();

        var warnings = new List<AnalysisWarning>();

        // 1. 사용자 정의 컬럼이 있으면 그대로 사용.
        if (options.Columns is { Count: > 0 } customColumns)
        {
            var rows = BuildRowsWithSelectors(items, customColumns, warnings, cancellationToken);
            return (DataPipeline.FromData(rows).ToDataFrame(), warnings);
        }

        // 2. 비어 있는 입력은 빈 DataFrame.
        if (items.Count == 0)
            return (DataPipeline.FromData(Array.Empty<Dictionary<string, string>>()).ToDataFrame(), warnings);

        // 3. dictionary-like 입력은 키 union 으로 컬럼 결정.
        if (TryBuildFromDictionary(items, options, warnings, cancellationToken, out var dictDf))
            return (dictDf!, warnings);

        // 4. POCO 입력은 reflection.
        var pocoDf = BuildFromPoco(items, options, warnings, cancellationToken);
        return (pocoDf, warnings);
    }

    private static List<Dictionary<string, string>> BuildRowsWithSelectors<T>(
        IReadOnlyList<T> items,
        IReadOnlyList<NamedColumn<T>> columns,
        List<AnalysisWarning> warnings,
        CancellationToken cancellationToken)
    {
        var seenUnsupported = new HashSet<string>();
        var rows = new List<Dictionary<string, string>>(items.Count);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string>(columns.Count);
            foreach (var column in columns)
            {
                var raw = item is null ? null : column.Selector(item);
                row[column.Name] = ConvertCell(raw, column.Name, seenUnsupported, warnings);
            }
            rows.Add(row);
        }
        return rows;
    }

    private static bool TryBuildFromDictionary<T>(
        IReadOnlyList<T> items,
        EnumerableSourceOptions<T> options,
        List<AnalysisWarning> warnings,
        CancellationToken cancellationToken,
        out DataFrame? dataFrame)
    {
        dataFrame = null;

        // 첫 non-null item 의 runtime 타입으로 dictionary 여부 판정.
        // (T = object/dynamic 인 경우에도 runtime 타입을 우선시한다.)
        IDictionary<string, object?>? firstDict = null;
        foreach (var item in items)
        {
            if (item is null) continue;
            firstDict = AsDictionary(item);
            if (firstDict is not null) break;
        }
        if (firstDict is null) return false;

        // 모든 키의 union 을 컬럼으로 사용 (insertion-ordered).
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null) continue;
            var dict = AsDictionary(item);
            if (dict is null) continue;
            foreach (var key in dict.Keys)
            {
                if (seen.Add(key)) columns.Add(key);
            }
        }

        var filteredColumns = ApplyIncludeExclude(columns, options);
        var aliasMap = options.HeaderAliases;

        var rows = new List<Dictionary<string, string>>(items.Count);
        var seenUnsupported = new HashSet<string>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dict = item is null ? null : AsDictionary(item);
            var row = new Dictionary<string, string>(filteredColumns.Count);
            foreach (var col in filteredColumns)
            {
                object? raw = null;
                if (dict is not null && dict.TryGetValue(col, out var v)) raw = v;

                var label = aliasMap is not null && aliasMap.TryGetValue(col, out var alias) ? alias : col;
                row[label] = ConvertCell(raw, label, seenUnsupported, warnings);
            }
            rows.Add(row);
        }

        dataFrame = DataPipeline.FromData(rows).ToDataFrame();
        return true;
    }

    private static DataFrame BuildFromPoco<T>(
        IReadOnlyList<T> items,
        EnumerableSourceOptions<T> options,
        List<AnalysisWarning> warnings,
        CancellationToken cancellationToken)
    {
        // T = object 인 경우 runtime 타입을 사용해야 reflection 이 의미를 가진다.
        var pocoType = typeof(T) == typeof(object)
            ? items.FirstOrDefault(i => i is not null)?.GetType() ?? typeof(T)
            : typeof(T);

        var properties = pocoType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.GetCustomAttribute<DataLensIgnoreAttribute>(inherit: true) is null)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>(inherit: true) is null)
            .ToArray();

        var allNames = properties.Select(p => p.Name).ToList();
        var filtered = ApplyIncludeExclude(allNames, options);
        var nameToProperty = properties.ToDictionary(p => p.Name, StringComparer.Ordinal);

        var aliasMap = options.HeaderAliases;
        var seenUnsupported = new HashSet<string>();

        var rows = new List<Dictionary<string, string>>(items.Count);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string>(filtered.Count);
            foreach (var propName in filtered)
            {
                if (!nameToProperty.TryGetValue(propName, out var prop)) continue;
                var label = aliasMap is not null && aliasMap.TryGetValue(propName, out var alias) ? alias : propName;
                var raw = item is null ? null : prop.GetValue(item);
                row[label] = ConvertCell(raw, label, seenUnsupported, warnings);
            }
            rows.Add(row);
        }

        return DataPipeline.FromData(rows).ToDataFrame();
    }

    private static IDictionary<string, object?>? AsDictionary(object item)
    {
        if (item is IDictionary<string, object?> generic) return generic;
        // ExpandoObject 는 IDictionary<string, object?> 를 직접 구현한다.
        if (item is IDictionary<string, object> nullableValueless)
        {
            // 어댑팅: object → object?
            return new DictionaryAdapter(nullableValueless);
        }
        return null;
    }

    private sealed class DictionaryAdapter : IDictionary<string, object?>
    {
        private readonly IDictionary<string, object> _inner;
        public DictionaryAdapter(IDictionary<string, object> inner) => _inner = inner;
        public object? this[string key]
        {
            get => _inner[key];
            set => _inner[key] = value!;
        }
        public ICollection<string> Keys => _inner.Keys;
        public ICollection<object?> Values => _inner.Values.Select(v => (object?)v).ToList();
        public int Count => _inner.Count;
        public bool IsReadOnly => _inner.IsReadOnly;
        public void Add(string key, object? value) => _inner.Add(key, value!);
        public void Add(KeyValuePair<string, object?> item) => _inner.Add(item.Key, item.Value!);
        public void Clear() => _inner.Clear();
        public bool Contains(KeyValuePair<string, object?> item) => _inner.ContainsKey(item.Key);
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
        {
            foreach (var kv in _inner) array[arrayIndex++] = new(kv.Key, kv.Value);
        }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            foreach (var kv in _inner) yield return new(kv.Key, kv.Value);
        }
        public bool Remove(string key) => _inner.Remove(key);
        public bool Remove(KeyValuePair<string, object?> item) => _inner.Remove(item.Key);
        public bool TryGetValue(string key, out object? value)
        {
            if (_inner.TryGetValue(key, out var v)) { value = v; return true; }
            value = null; return false;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static List<string> ApplyIncludeExclude<T>(IReadOnlyList<string> names, EnumerableSourceOptions<T> options)
    {
        IEnumerable<string> result = names;
        if (options.IncludeProperties is { Count: > 0 } include)
        {
            var inc = new HashSet<string>(include, StringComparer.Ordinal);
            result = result.Where(inc.Contains);
        }
        if (options.ExcludeProperties is { Count: > 0 } exclude)
        {
            var exc = new HashSet<string>(exclude, StringComparer.Ordinal);
            result = result.Where(n => !exc.Contains(n));
        }
        return result.ToList();
    }

    private static string ConvertCell(
        object? raw,
        string columnLabel,
        HashSet<string> seenUnsupported,
        List<AnalysisWarning> warnings)
    {
        var cell = DefaultTypeMapper.ToCell(raw, out var unsupported);
        if (unsupported && seenUnsupported.Add(columnLabel))
        {
            warnings.Add(new AnalysisWarning(
                "EnumerableSource",
                InsightErrorCategory.Unknown,
                $"Column '{columnLabel}' has type '{raw!.GetType().Name}' outside the minimal type-mapping policy; values are passed through ToString()."));
        }
        return cell;
    }
}
