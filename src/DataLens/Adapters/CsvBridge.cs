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
