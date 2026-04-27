using DataLens;
using DataLens.Sample.Examples;

// CLI dispatcher.
// Default mode: run the full pipeline against a CSV file (matches the
// historical CLI demo). `--example=<name>` runs a specific README snippet
// for ad-hoc verification — the snippets themselves compile unconditionally,
// which is the actual README ↔ code drift safety net.

if (args.Length == 0)
{
    PrintUsage();
    return;
}

if (args[0].StartsWith("--example=", StringComparison.Ordinal))
{
    var name = args[0]["--example=".Length..];
    var inputPath = args.Length > 1 ? args[1] : null;
    if (inputPath is null && name is not "poco")
    {
        Console.Error.WriteLine($"--example={name} requires a CSV path as the second argument.");
        return;
    }

    Func<Task> run = name switch
    {
        "quick-start"   => () => QuickStartExample.RunOneLineAsync(inputPath!),
        "poco"          => () => QuickStartExample.RunPocoCollectionsAsync(),
        "programmatic"  => () => ProgrammaticAccessExample.RunAsync(inputPath!),
        "selecting"     => () => SelectingAnalysesExample.RunAsync(inputPath!),
        "profiling"     => () => ModuleExamples.ProfilingAsync(inputPath!),
        "descriptive"   => () => ModuleExamples.DescriptiveAsync(inputPath!),
        "correlation"   => () => ModuleExamples.CorrelationAsync(inputPath!),
        "regression"    => () => ModuleExamples.RegressionAsync(inputPath!),
        "cluster"       => () => ModuleExamples.ClusterAsync(inputPath!),
        "outliers"      => () => ModuleExamples.OutliersAsync(inputPath!),
        "features"      => () => ModuleExamples.FeatureImportanceAsync(inputPath!),
        "pca"           => () => ModuleExamples.PcaAsync(inputPath!),
        "changepoint"   => () => ModuleExamples.ChangepointAsync(inputPath!),
        "output"        => () => OutputExample.RunAsync(inputPath!),
        "fileprepper"   => () => IntegrationExamples.FilePrepperToDataLensAsync(inputPath!),
        "mloop"         => () => IntegrationExamples.DataLensToMLoopAsync(inputPath!),
        _ => () =>
        {
            Console.Error.WriteLine($"Unknown example: {name}");
            PrintUsage();
            return Task.CompletedTask;
        }
    };

    await run();
    return;
}

// Default mode — full pipeline, kept identical to the historical CLI demo.
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

if (result.Descriptive != null)
{
    Console.WriteLine($"== Descriptive Statistics ==");
    foreach (var col in result.Descriptive.Columns)
    {
        Console.WriteLine($"  {col.Name}: mean={col.Mean:F3}, std={col.Std:F3}, min={col.Min:F3}, max={col.Max:F3}, skew={col.Skewness:F3}");
    }
    Console.WriteLine();
}

if (result.Correlation?.HighCorrelationPairs?.Count > 0)
{
    Console.WriteLine($"== High Correlation Pairs ==");
    foreach (var pair in result.Correlation.HighCorrelationPairs.Take(10))
    {
        Console.WriteLine($"  {pair.Column1} <-> {pair.Column2}: r={pair.Value:F4}");
    }
    Console.WriteLine();
}

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

if (result.Outliers != null)
{
    Console.WriteLine($"== Outliers ==");
    Console.WriteLine($"  Total: {result.Outliers.OutlierCount}/{result.Outliers.TotalRows} ({result.Outliers.OutlierPercentage:F1}%)");
    Console.WriteLine();
}

var jsonPath = Path.ChangeExtension(filePath, ".analysis.json");
await result.ToJsonAsync(jsonPath);
Console.WriteLine($"Full analysis saved to: {jsonPath}");

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  DataLens.Sample <csv-file-path> [target-column]");
    Console.WriteLine("  DataLens.Sample --example=<name> [csv-file-path]");
    Console.WriteLine();
    Console.WriteLine("Examples (mirrors README code blocks):");
    Console.WriteLine("  --example=quick-start    --example=poco         --example=programmatic");
    Console.WriteLine("  --example=selecting      --example=output");
    Console.WriteLine("  --example=profiling      --example=descriptive  --example=correlation");
    Console.WriteLine("  --example=regression     --example=cluster      --example=outliers");
    Console.WriteLine("  --example=features       --example=pca          --example=changepoint");
    Console.WriteLine("  --example=fileprepper    --example=mloop");
}
