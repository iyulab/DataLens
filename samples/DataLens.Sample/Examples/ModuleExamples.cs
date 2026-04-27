using DataLens;

namespace DataLens.Sample.Examples;

/// <summary>
/// README "Analysis Modules" 1~9 의 코드 블록 잠금. 각 메서드는 README 본문과 1:1 대응.
/// </summary>
internal static class ModuleExamples
{
    internal static async Task ProfilingAsync(string filePath)
    {
        var profile = await DataLensEngine.Profile(filePath);
        Console.WriteLine($"Rows: {profile.RowCount}, Columns: {profile.ColumnCount}");
        foreach (var col in profile.Columns)
        {
            Console.WriteLine($"{col.Name}: type={col.DataType}, null={col.NullPercentage:F1}%");
        }
    }

    internal static async Task DescriptiveAsync(string filePath)
    {
        var analysis = await DataLensEngine.Analyze(filePath);
        foreach (var col in analysis.Descriptive!.Columns)
        {
            Console.WriteLine($"{col.Name}: mean={col.Mean:F3}, std={col.Std:F3}, skew={col.Skewness:F3}");
        }
    }

    internal static async Task CorrelationAsync(string filePath)
    {
        var analysis = await DataLensEngine.Analyze(filePath);
        var corr = analysis.Correlation!;
        foreach (var pair in corr.HighCorrelationPairs)
        {
            Console.WriteLine($"{pair.Column1} ~ {pair.Column2}: r={pair.Value:F3}");
        }
    }

    internal static async Task RegressionAsync(string filePath)
    {
        var options = new AnalysisOptions { TargetColumn = "S_OutputPower", IncludeRegression = true };
        var analysis = await DataLensEngine.Analyze(filePath, options);
        var regression = analysis.Regression!;
        foreach (var entry in regression.Entries)
        {
            Console.WriteLine($"{entry.FeatureColumn}: slope={entry.Slope:F4}, R²={entry.RSquared:F4}");
        }
    }

    internal static async Task ClusterAsync(string filePath)
    {
        var analysis = await DataLensEngine.Analyze(filePath);
        var clusters = analysis.Clusters!;
        Console.WriteLine($"Optimal K={clusters.OptimalK}");
        if (clusters.KMeans is { } km)
        {
            foreach (var cluster in km.ClusterSizes)
            {
                Console.WriteLine($"Cluster {cluster.ClusterId}: {cluster.Size} rows");
            }
        }
    }

    internal static async Task OutliersAsync(string filePath)
    {
        var analysis = await DataLensEngine.Analyze(filePath);
        var outliers = analysis.Outliers!;
        Console.WriteLine($"Outliers: {outliers.OutlierCount} rows ({outliers.OutlierPercentage:F1}%)");
        if (outliers.IsolationForest is { } iso)
        {
            Console.WriteLine($"  IsolationForest: {iso.AnomalyCount} anomalies (threshold={iso.Threshold:F3})");
        }
    }

    internal static async Task FeatureImportanceAsync(string filePath)
    {
        var report = await DataLensEngine.FeatureImportance(filePath, target: "Machining_Process");
        foreach (var feat in report.Importance!.Scores)
        {
            Console.WriteLine($"  {feat.Name}: {feat.Score:F4}");
        }
    }

    internal static async Task PcaAsync(string filePath)
    {
        var analysis = await DataLensEngine.Analyze(filePath);
        var pca = analysis.Pca!;
        Console.WriteLine($"Components: {pca.NComponents}, total variance explained: {pca.TotalExplainedVariance:P1}");
        for (int i = 0; i < pca.ExplainedVariance.Length; i++)
        {
            Console.WriteLine($"  PC{i + 1}: {pca.ExplainedVariance[i]:P1}");
        }
    }

    internal static async Task ChangepointAsync(string filePath)
    {
        var options = new AnalysisOptions
        {
            IncludeChangepoints = true,
            ChangepointCost = 1, // 0=L2 mean, 1=Normal mean+variance
            ChangepointMinSegmentLength = 10
        };
        var analysis = await DataLensEngine.Analyze(filePath, options);
        _ = analysis;
    }
}
