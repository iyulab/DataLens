using DataLens;

namespace DataLens.Sample.Examples;

/// <summary>
/// README "Selecting analyses" 블록 잠금.
/// </summary>
internal static class SelectingAnalysesExample
{
    internal static async Task RunAsync(string filePath)
    {
        var options = new AnalysisOptions
        {
            IncludeProfiling   = true,
            IncludeDescriptive = true,
            IncludeCorrelation = true,
            IncludeClustering  = false,
            IncludeOutliers    = false,
            IncludeFeatures    = false,
            IncludePca         = false,
            IncludeChangepoints = false,
            CorrelationThreshold = 0.8
        };

        var analysis = await DataLensEngine.Analyze(filePath, options);
        var json = analysis.ToJson(Section.Correlation); // Single-section JSON
        _ = json;
    }
}
