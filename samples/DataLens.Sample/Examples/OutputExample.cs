using DataLens;

namespace DataLens.Sample.Examples;

/// <summary>
/// README "Output" 섹션의 JSON 출력 코드 블록 잠금.
/// </summary>
internal static class OutputExample
{
    internal static async Task RunAsync(string filePath)
    {
        var analysis = await DataLensEngine.Analyze(filePath);

        // Full result
        var json = analysis.ToJson();
        await analysis.ToJsonAsync("results.json");

        // Section-scoped JSON
        var corrJson = analysis.ToJson(Section.Correlation);

        _ = (json, corrJson);
    }
}
