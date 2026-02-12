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
            _ => throw new NotSupportedException($"Unsupported file format: {ext}")
        };

        return pipeline.ToDataFrame();
    }

    private static async Task<IEnumerable<Dictionary<string, string>>> LoadJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var data = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json);
        return data ?? [];
    }
}
