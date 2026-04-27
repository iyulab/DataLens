using DataLens;
using FilePrepper.Pipeline;

namespace DataLens.Sample.Examples;

/// <summary>
/// README "Integration with iyulab Tools" 섹션의 코드 블록 잠금.
/// </summary>
internal static class IntegrationExamples
{
    internal static async Task FilePrepperToDataLensAsync(string rawDataCsv)
    {
        // README — FilePrepper → DataLens
        var pipeline = await DataPipeline.FromCsvAsync(rawDataCsv);
        // ... apply FilePrepper transforms ...
        var df = pipeline.ToDataFrame();

        var analysis = await DataLensEngine.Analyze(df);
        _ = analysis;
    }

    internal static async Task DataLensToMLoopAsync(string trainCsv)
    {
        // README — DataLens → MLoop
        var options = new AnalysisOptions { TargetColumn = "target_column", IncludeFeatures = true };
        var analysis = await DataLensEngine.Analyze(trainCsv, options);

        // Top features by ANOVA F-score
        var topByAnova = analysis.Features!.Anova!.Features
            .OrderByDescending(f => f.FStatistic)
            .Take(15);

        // High-correlation pairs (multicollinearity hints)
        foreach (var pair in analysis.Correlation!.HighCorrelationPairs)
        {
            Console.WriteLine($"{pair.Column1} ~ {pair.Column2}: r={pair.Value:F3}");
        }

        _ = topByAnova;
    }
}
