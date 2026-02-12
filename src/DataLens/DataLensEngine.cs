using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;

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
        options ??= AnalysisOptions.Default;
        var adapter = new DataAdapter(dataFrame);

        var profile = options.IncludeProfiling
            ? await new ProfilingAnalyzer().AnalyzeAsync(adapter, options)
            : null;

        var descriptive = options.IncludeDescriptive
            ? await new DescriptiveAnalyzer().AnalyzeAsync(adapter, options)
            : null;

        var correlation = options.IncludeCorrelation
            ? await new CorrelationAnalyzer().AnalyzeAsync(adapter, options)
            : null;

        var regression = options.IncludeRegression
            ? await new RegressionAnalyzer().AnalyzeAsync(adapter, options)
            : null;

        var distribution = options.IncludeDistribution
            ? await new DistributionAnalyzer().AnalyzeAsync(adapter, options)
            : null;

        var clusters = options.IncludeClustering
            ? await new ClusterAnalyzer().AnalyzeAsync(adapter, options)
            : null;

        var outliers = options.IncludeOutliers
            ? await new OutlierAnalyzer().AnalyzeAsync(adapter, options)
            : null;

        var features = options.IncludeFeatures
            ? await new FeatureAnalyzer().AnalyzeAsync(adapter, options)
            : null;

        var pca = options.IncludePca
            ? await new PcaAnalyzer().AnalyzeAsync(adapter, options)
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
            Pca = pca
        };
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
