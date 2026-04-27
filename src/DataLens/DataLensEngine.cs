using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;
using UInsight;

namespace DataLens;

/// <summary>
/// DataLens 메인 진입점. 정적 API로 데이터 분석 파이프라인을 실행한다.
/// </summary>
public static class DataLensEngine
{
    public static async Task<AnalysisResult> Analyze(string filePath, AnalysisOptions? options = null)
    {
        var dataFrame = await CsvBridge.LoadAsync(filePath);
        return await Analyze(dataFrame, options);
    }

    public static async Task<AnalysisResult> Analyze(DataFrame dataFrame, AnalysisOptions? options = null)
    {
        return await AnalyzeCore(dataFrame, options, sourceWarnings: null);
    }

    /// <summary>
    /// POCO / dynamic / <see cref="IDictionary{TKey, TValue}"/> / <see cref="System.Dynamic.ExpandoObject"/>
    /// 컬렉션을 직접 분석한다. 입력은 한 번만 enumerate 된다.
    /// </summary>
    public static async Task<AnalysisResult> Analyze<T>(
        IEnumerable<T> source,
        AnalysisOptions? options = null,
        EnumerableSourceOptions<T>? sourceOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var items = source as IReadOnlyList<T> ?? source.ToList();
        var (dataFrame, warnings) = EnumerableSourceBuilder.Build(items, sourceOptions, cancellationToken);
        return await AnalyzeCore(dataFrame, options, warnings);
    }

    private static async Task<AnalysisResult> AnalyzeCore(
        DataFrame dataFrame,
        AnalysisOptions? options,
        IReadOnlyList<AnalysisWarning>? sourceWarnings)
    {
        options ??= AnalysisOptions.Default;
        var adapter = new DataAdapter(dataFrame);
        var warnings = new List<AnalysisWarning>();
        if (sourceWarnings is { Count: > 0 }) warnings.AddRange(sourceWarnings);

        var profile = options.IncludeProfiling
            ? await SafeAnalyze("Profiling", () => new ProfilingAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var descriptive = options.IncludeDescriptive
            ? await SafeAnalyze("Descriptive", () => new DescriptiveAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var correlation = options.IncludeCorrelation
            ? await SafeAnalyze("Correlation", () => new CorrelationAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var regression = options.IncludeRegression
            ? await SafeAnalyze("Regression", () => new RegressionAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var distribution = options.IncludeDistribution
            ? await SafeAnalyze("Distribution", () => new DistributionAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var clusters = options.IncludeClustering
            ? await SafeAnalyze("Clustering", () => new ClusterAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var outliers = options.IncludeOutliers
            ? await SafeAnalyze("Outliers", () => new OutlierAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var features = options.IncludeFeatures
            ? await SafeAnalyze("Features", () => new FeatureAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var pca = options.IncludePca
            ? await SafeAnalyze("Pca", () => new PcaAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        var changepoints = options.IncludeChangepoints
            ? await SafeAnalyze("Changepoints", () => new ChangepointAnalyzer().AnalyzeAsync(adapter, options), warnings)
            : null;

        return new AnalysisResult
        {
            Profile = profile,
            Descriptive = descriptive,
            Correlation = correlation,
            Regression = regression,
            Clusters = clusters,
            Outliers = outliers,
            Distribution = distribution,
            Features = features,
            Pca = pca,
            Changepoints = changepoints,
            Warnings = warnings
        };
    }

    private static async Task<T?> SafeAnalyze<T>(
        string analyzerName,
        Func<Task<T>> analyzer,
        List<AnalysisWarning> warnings) where T : class
    {
        try
        {
            return await analyzer();
        }
        catch (InsightException ex)
        {
            warnings.Add(new AnalysisWarning(analyzerName, ex.Category, ex.Message));
            return null;
        }
        catch (Exception ex)
        {
            warnings.Add(new AnalysisWarning(analyzerName, InsightErrorCategory.Unknown, ex.Message));
            return null;
        }
    }

    public static async Task<ProfileReport> Profile(string filePath)
    {
        var dataFrame = await CsvBridge.LoadAsync(filePath);
        var adapter = new DataAdapter(dataFrame);
        return await new ProfilingAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default);
    }

    public static async Task<FeatureReport> FeatureImportance(string filePath, string target)
    {
        var dataFrame = await CsvBridge.LoadAsync(filePath);
        var adapter = new DataAdapter(dataFrame);
        var options = new AnalysisOptions { TargetColumn = target };
        return await new FeatureAnalyzer().AnalyzeAsync(adapter, options);
    }
}
