using DataLens;

namespace DataLens.Sample.Examples;

/// <summary>
/// README "Programmatic Access" 코드 블록 잠금.
/// </summary>
internal static class ProgrammaticAccessExample
{
    internal static async Task RunAsync(string filePath)
    {
        var analysis = await DataLensEngine.Analyze(filePath);

        // Profile (row/column counts, per-column null %, type, basic stats)
        Console.WriteLine($"Rows: {analysis.Profile!.RowCount}, Cols: {analysis.Profile.ColumnCount}");
        foreach (var col in analysis.Profile.Columns)
        {
            Console.WriteLine($"{col.Name}: type={col.DataType}, null={col.NullPercentage:F1}%");
        }

        // Descriptive statistics (mean, std, skew, kurtosis, ...)
        foreach (var col in analysis.Descriptive!.Columns)
        {
            Console.WriteLine($"{col.Name}: mean={col.Mean:F3}, skew={col.Skewness:F3}");
        }

        // Correlation — high pairs already filtered by AnalysisOptions.CorrelationThreshold
        foreach (var pair in analysis.Correlation!.HighCorrelationPairs)
        {
            Console.WriteLine($"{pair.Column1} ~ {pair.Column2}: r={pair.Value:F3}");
        }

        // Clusters
        var kmeans = analysis.Clusters!.KMeans;
        if (kmeans is not null)
        {
            Console.WriteLine($"K={kmeans.K}, WCSS={kmeans.Wcss:F3}");
            foreach (var cluster in kmeans.ClusterSizes)
            {
                Console.WriteLine($"  Cluster {cluster.ClusterId}: {cluster.Size} rows ({cluster.Percentage:F1}%)");
            }
        }

        // Outliers
        Console.WriteLine($"Outliers: {analysis.Outliers!.OutlierCount} rows ({analysis.Outliers.OutlierPercentage:F1}%)");
    }
}
