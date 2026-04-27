using System.Text.Json;
using FilePrepper.Pipeline;

namespace DataLens.Adapters;

/// <summary>
/// 파일 경로 → FilePrepper DataPipeline → DataFrame 로드 브릿지.
/// CSV / TSV / JSON 입력을 받아 DataFrame 으로 변환한다.
/// </summary>
public static class CsvBridge
{
    public static async Task<DataFrame> LoadAsync(string filePath, CsvLoadOptions? options = null)
    {
        options ??= CsvLoadOptions.Default;

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var pipeline = ext switch
        {
            ".csv" or ".tsv" => await DataPipeline.FromCsvAsync(filePath),
            ".json" => DataPipeline.FromData(await LoadJsonAsync(filePath)),
            _ => await DataPipeline.FromCsvAsync(filePath) // CSV fallback for unknown extensions
        };

        var df = pipeline.ToDataFrame();

        if (options.RemoveDuplicateRows)
            df = RemoveDuplicateRows(df);

        return df;
    }

    private static DataFrame RemoveDuplicateRows(DataFrame df)
    {
        // Hash + per-column equality 로 비교. `string.Join("\0", ...)` 로 발생하던
        // 행당 임시 string 할당을 제거한다 (csvbridge-integrity #4).
        var columns = df.ColumnNames;
        var seen = new HashSet<RowKey>(new RowKeyComparer());
        var unique = new List<Dictionary<string, string>>();

        foreach (var row in df.Rows)
        {
            var values = new string[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                values[i] = row.TryGetValue(columns[i], out var v) ? v ?? string.Empty : string.Empty;
            }

            if (seen.Add(new RowKey(values)))
                unique.Add(row);
        }

        if (unique.Count == df.RowCount)
            return df;

        return DataPipeline.FromData(unique).ToDataFrame();
    }

    private readonly record struct RowKey(string[] Values);

    private sealed class RowKeyComparer : IEqualityComparer<RowKey>
    {
        public bool Equals(RowKey x, RowKey y)
        {
            if (x.Values.Length != y.Values.Length) return false;
            for (int i = 0; i < x.Values.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(x.Values[i], y.Values[i])) return false;
            }
            return true;
        }

        public int GetHashCode(RowKey obj)
        {
            var h = new HashCode();
            foreach (var v in obj.Values) h.Add(v, StringComparer.Ordinal);
            return h.ToHashCode();
        }
    }

    private static async Task<IEnumerable<Dictionary<string, string>>> LoadJsonAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        // 0.6.0 까지는 List<Dictionary<string, string>> 로 강제 역직렬화하여
        // number/bool/null 이 모두 string 으로 좌초되었다 (csvbridge-integrity #3).
        // 0.7.0 부터 JsonElement 단계에서 ValueKind 를 보존한 뒤 DefaultTypeMapper 로 직렬화한다.
        var typed = await JsonSerializer.DeserializeAsync<List<Dictionary<string, JsonElement>>>(stream);
        if (typed is null) return [];

        var rows = new List<Dictionary<string, string>>(typed.Count);
        foreach (var typedRow in typed)
        {
            var row = new Dictionary<string, string>(typedRow.Count);
            foreach (var (key, element) in typedRow)
            {
                row[key] = DefaultTypeMapper.ToCell(element);
            }
            rows.Add(row);
        }
        return rows;
    }
}
