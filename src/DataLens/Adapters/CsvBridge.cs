using FilePrepper.Pipeline;

namespace DataLens.Adapters;

/// <summary>
/// 파일 경로 → FilePrepper DataPipeline → DataFrame 로드 브릿지.
/// CSV, Excel, JSON, XML 지원.
/// </summary>
public static class CsvBridge
{
    public static async Task<DataFrame> LoadAsync(string filePath)
    {
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

        // 기본 전처리: 중복 행 제거
        return RemoveDuplicateRows(df);
    }

    private static DataFrame RemoveDuplicateRows(DataFrame df)
    {
        var seen = new HashSet<string>();
        var unique = new List<Dictionary<string, string>>();

        foreach (var row in df.Rows)
        {
            var key = string.Join("\0", df.ColumnNames.Select(c =>
                row.TryGetValue(c, out var v) ? v ?? "" : ""));

            if (seen.Add(key))
                unique.Add(row);
        }

        if (unique.Count == df.RowCount)
            return df;

        return DataPipeline.FromData(unique).ToDataFrame();
    }

    private static async Task<IEnumerable<Dictionary<string, string>>> LoadJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var data = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json);
        return data ?? [];
    }
}
