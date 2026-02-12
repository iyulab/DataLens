using DataLens;

if (args.Length == 0)
{
    Console.WriteLine("Usage: DataLens.Sample <csv-file-path> [target-column]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  DataLens.Sample data.csv");
    Console.WriteLine("  DataLens.Sample data.csv OutputPower");
    return;
}

var filePath = args[0];
var targetColumn = args.Length > 1 ? args[1] : null;

Console.WriteLine($"Analyzing: {filePath}");
Console.WriteLine();

var options = new AnalysisOptions
{
    TargetColumn = targetColumn,
    CorrelationThreshold = 0.7,
    OutlierContamination = 0.1,
};

var result = await DataLensEngine.Analyze(filePath, options);

// 프로파일 요약
if (result.Profile != null)
{
    Console.WriteLine($"== Profile ==");
    Console.WriteLine($"  Rows: {result.Profile.RowCount}, Columns: {result.Profile.ColumnCount}");
    foreach (var col in result.Profile.Columns)
    {
        Console.WriteLine($"  {col.Name}: {col.DataType}, valid={col.ValidCount}, null={col.NullCount} ({col.NullPercentage:F1}%)");
    }
    Console.WriteLine();
}

// 기술통계 요약
if (result.Descriptive != null)
{
    Console.WriteLine($"== Descriptive Statistics ==");
    foreach (var col in result.Descriptive.Columns)
    {
        Console.WriteLine($"  {col.Name}: mean={col.Mean:F3}, std={col.Std:F3}, min={col.Min:F3}, max={col.Max:F3}, skew={col.Skewness:F3}");
    }
    Console.WriteLine();
}

// 상관분석 요약
if (result.Correlation?.HighCorrelationPairs?.Count > 0)
{
    Console.WriteLine($"== High Correlation Pairs ==");
    foreach (var pair in result.Correlation.HighCorrelationPairs.Take(10))
    {
        Console.WriteLine($"  {pair.Column1} <-> {pair.Column2}: r={pair.Value:F4}");
    }
    Console.WriteLine();
}

// 클러스터 요약
if (result.Clusters?.KMeans != null)
{
    Console.WriteLine($"== Clustering ==");
    Console.WriteLine($"  Optimal K: {result.Clusters.OptimalK}");
    Console.WriteLine($"  K-Means WCSS: {result.Clusters.KMeans.Wcss:F2}");
    foreach (var cs in result.Clusters.KMeans.ClusterSizes)
    {
        Console.WriteLine($"  Cluster {cs.ClusterId}: {cs.Size} rows ({cs.Percentage:F1}%)");
    }
    Console.WriteLine();
}

// 이상치 요약
if (result.Outliers != null)
{
    Console.WriteLine($"== Outliers ==");
    Console.WriteLine($"  Total: {result.Outliers.OutlierCount}/{result.Outliers.TotalRows} ({result.Outliers.OutlierPercentage:F1}%)");
    Console.WriteLine();
}

// JSON 출력
var jsonPath = Path.ChangeExtension(filePath, ".analysis.json");
await result.ToJsonAsync(jsonPath);
Console.WriteLine($"Full analysis saved to: {jsonPath}");
