using DataLens;

namespace DataLens.Sample.Examples;

/// <summary>
/// README "One-Line Analysis" + "POCO Collections" 코드 블록을 그대로 컴파일에 노출한다.
/// API drift 발생 시 빌드가 실패하여 README ↔ 코드 정합성을 잠근다.
/// </summary>
internal static class QuickStartExample
{
    internal static async Task RunOneLineAsync(string filePath)
    {
        // README — Quick Start / One-Line Analysis
        var analysis = await DataLensEngine.Analyze(filePath);
        await analysis.ToJsonAsync("results.json");
    }

    internal record Sale(DateTime 주문일자, decimal 금액, string 고객명);

    internal static async Task RunPocoCollectionsAsync()
    {
        // README — POCO Collections (no file required)
        var sales = new List<Sale>
        {
            new(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1000m, "갑"),
            new(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 2500m, "을"),
        };

        var analysis = await DataLensEngine.Analyze(sales);
        _ = analysis;
    }
}
