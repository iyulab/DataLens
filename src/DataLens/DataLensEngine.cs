using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;
using UInsight;

namespace DataLens;

/// <summary>
/// DataLens 메인 진입점. 정적 API로 데이터 분석 파이프라인을 실행한다.
/// </summary>
/// <remarks>
/// <para>
/// 모든 <c>Analyze*</c> 메서드는 호출 thread 를 즉시 release 한다 (내부적으로 <see cref="Task.Run(Action)"/> wrap).
/// UI thread 에서 <c>await</c> 해도 freeze 되지 않는다.
/// </para>
/// <para>
/// <b>Cancellation 보장 범위</b>: <see cref="CancellationToken"/> 은 *between-analyzer* 경계에서만 동작한다.
/// 개별 analyzer (UInsight FFI 호출) 진입 후에는 native 코드에서 차단되며 .NET 측에서 중단할 수 없다.
/// 즉, 큰 LOF/IsolationForest 계산이 시작되면 그 모듈이 끝날 때까지 기다린 뒤 다음 analyzer 진입 직전에 throw 된다.
/// </para>
/// </remarks>
public static class DataLensEngine
{
    public static async Task<AnalysisResult> Analyze(
        string filePath,
        AnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var dataFrame = await CsvBridge.LoadAsync(filePath).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await Analyze(dataFrame, options, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<AnalysisResult> Analyze(
        DataFrame dataFrame,
        AnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await RunCoreAsync(dataFrame, options, sourceWarnings: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POCO / dynamic / <see cref="IDictionary{TKey, TValue}"/> / <see cref="System.Dynamic.ExpandoObject"/>
    /// 컬렉션을 직접 분석한다. 입력은 한 번만 enumerate 된다.
    /// </summary>
    /// <remarks>
    /// <see cref="EnumerableSourceBuilder.Build{T}"/> 의 reflection-based POCO 변환은 행 수 × 컬럼 수에
    /// 비례한 비용 (8469행 × 78 컬럼 = ~660K reflection 호출) 이 든다. async 계약 보장을 위해
    /// Build 도 <see cref="Task.Run(Action)"/> 안으로 들어간다 — 호출 thread 는 즉시 release.
    /// 단, deferred IEnumerable 의 enumeration (<c>source.ToList()</c>) 은 호출 thread 에서 수행된다.
    /// 호출자 thread state 에 의존하는 query 의 의미를 보존하기 위함.
    /// </remarks>
    public static Task<AnalysisResult> Analyze<T>(
        IEnumerable<T> source,
        AnalysisOptions? options = null,
        EnumerableSourceOptions<T>? sourceOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var items = source as IReadOnlyList<T> ?? source.ToList();
        return Task.Run(() =>
        {
            var (dataFrame, warnings) = EnumerableSourceBuilder.Build(items, sourceOptions, cancellationToken);
            return AnalyzeCore(dataFrame, options, warnings, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// 동기 분석 entry point. CLI / 배치 / 콘솔 등 thread pool 오버헤드가 불필요한 호출자용.
    /// async 오버로드는 내부적으로 이 메서드를 <see cref="Task.Run(Func{TResult})"/> wrap 한다.
    /// </summary>
    public static AnalysisResult AnalyzeSync(
        DataFrame dataFrame,
        AnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return AnalyzeCore(dataFrame, options, sourceWarnings: null, cancellationToken);
    }

    private static Task<AnalysisResult> RunCoreAsync(
        DataFrame dataFrame,
        AnalysisOptions? options,
        IReadOnlyList<AnalysisWarning>? sourceWarnings,
        CancellationToken cancellationToken)
    {
        // async 시그니처의 계약을 지키기 위해 Task.Run 으로 호출 thread 를 즉시 release.
        // analyzer 본체는 모두 동기 + FFI 이므로 Task.Yield 만으로는 호출 thread 가 다시 잡힌다.
        return Task.Run(
            () => AnalyzeCore(dataFrame, options, sourceWarnings, cancellationToken),
            cancellationToken);
    }

    private static AnalysisResult AnalyzeCore(
        DataFrame dataFrame,
        AnalysisOptions? options,
        IReadOnlyList<AnalysisWarning>? sourceWarnings,
        CancellationToken cancellationToken)
    {
        options ??= AnalysisOptions.Default;
        var adapter = new DataAdapter(dataFrame, options.IncludeColumns, options.ExcludeColumns);
        var warnings = new List<AnalysisWarning>();
        if (sourceWarnings is { Count: > 0 }) warnings.AddRange(sourceWarnings);

        cancellationToken.ThrowIfCancellationRequested();
        var profile = options.IncludeProfiling
            ? SafeAnalyze("Profiling", () => new ProfilingAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var descriptive = options.IncludeDescriptive
            ? SafeAnalyze("Descriptive", () => new DescriptiveAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var correlation = options.IncludeCorrelation
            ? SafeAnalyze("Correlation", () => new CorrelationAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var regression = options.IncludeRegression
            ? SafeAnalyze("Regression", () => new RegressionAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var distribution = options.IncludeDistribution
            ? SafeAnalyze("Distribution", () => new DistributionAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var clusters = options.IncludeClustering
            ? SafeAnalyze("Clustering", () => new ClusterAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var outliers = options.IncludeOutliers
            ? SafeAnalyze("Outliers", () => new OutlierAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var features = options.IncludeFeatures
            ? SafeAnalyze("Features", () => new FeatureAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var pca = options.IncludePca
            ? SafeAnalyze("Pca", () => new PcaAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        var changepoints = options.IncludeChangepoints
            ? SafeAnalyze("Changepoints", () => new ChangepointAnalyzer().AnalyzeAsync(adapter, options, warnings).GetAwaiter().GetResult(), warnings)
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

    private static T? SafeAnalyze<T>(
        string analyzerName,
        Func<T> analyzer,
        List<AnalysisWarning> warnings) where T : class
    {
        try
        {
            return analyzer();
        }
        catch (OperationCanceledException)
        {
            // Cancellation 은 호출자에게 그대로 전달 — warning 으로 둔갑시키면 안 된다.
            throw;
        }
        catch (InsightException ex)
        {
            warnings.Add(AnalysisWarning.FromInsightException(analyzerName, ex));
            return null;
        }
        catch (Exception ex)
        {
            warnings.Add(new AnalysisWarning(
                Analyzer: analyzerName,
                Category: WarningCategory.ComputationFailed,
                Message: ex.Message));
            return null;
        }
    }

    public static async Task<ProfileReport> Profile(string filePath, CancellationToken cancellationToken = default)
    {
        var dataFrame = await CsvBridge.LoadAsync(filePath).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var adapter = new DataAdapter(dataFrame);
            return new ProfilingAnalyzer().AnalyzeAsync(adapter, AnalysisOptions.Default).GetAwaiter().GetResult();
        }, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<FeatureReport> FeatureImportance(
        string filePath,
        string target,
        CancellationToken cancellationToken = default)
    {
        var dataFrame = await CsvBridge.LoadAsync(filePath).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var adapter = new DataAdapter(dataFrame);
            var options = new AnalysisOptions { TargetColumn = target };
            return new FeatureAnalyzer().AnalyzeAsync(adapter, options).GetAwaiter().GetResult();
        }, cancellationToken).ConfigureAwait(false);
    }
}
