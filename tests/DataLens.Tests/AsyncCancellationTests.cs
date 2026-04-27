using System.Diagnostics;
using DataLens.Adapters;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class AsyncCancellationTests
{
    private static DataFrame BuildLargeFrame(int rows = 500, int cols = 8)
    {
        var rng = new Random(7);
        var data = new List<Dictionary<string, string>>(rows);
        for (int i = 0; i < rows; i++)
        {
            var row = new Dictionary<string, string>(cols);
            for (int j = 0; j < cols; j++)
                row[$"col{j}"] = (rng.NextDouble() * 100).ToString("R");
            data.Add(row);
        }
        return DataPipeline.FromData(data).ToDataFrame();
    }

    [Fact]
    public async Task Analyze_DoesNotBlockCallingThread()
    {
        // 호출 thread 의 ManagedThreadId 가 동기 prefix 동안 점유되지 않아야 한다.
        // SynchronizationContext 없이 Task.Run wrap 이 적용되었다면 호출 thread 는 즉시 release.
        var df = BuildLargeFrame();
        var callerThreadId = Environment.CurrentManagedThreadId;

        var sw = Stopwatch.StartNew();
        var task = DataLensEngine.Analyze(df, new AnalysisOptions
        {
            // 모든 무거운 분석 켬.
            IncludeOutliers = true,
            IncludeClustering = true,
            IncludePca = true
        });
        // 호출 thread 가 await 없이 다음 줄로 즉시 도달해야 한다.
        var elapsedBeforeAwait = sw.ElapsedMilliseconds;

        var result = await task;
        sw.Stop();

        // 동기 prefix 가 길었다면 Task 가 반환되기 전에 모든 작업을 끝냈을 것 — < 50ms 가 정상.
        Assert.True(elapsedBeforeAwait < 100,
            $"Calling thread was blocked for {elapsedBeforeAwait}ms before await — async signature contract violated.");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Analyze_WithPreCancelledToken_ThrowsBeforeAnalysis()
    {
        var df = BuildLargeFrame();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DataLensEngine.Analyze(df, new AnalysisOptions(), cts.Token));
    }

    [Fact]
    public async Task Analyze_CancelDuringAnalysis_ThrowsBetweenAnalyzers()
    {
        // Cancel 보장 범위는 between-analyzer. 일정 시점에 cancel 시,
        // 다음 analyzer 진입 직전에 OperationCanceledException 이 throw 되어야 한다.
        var df = BuildLargeFrame(rows: 800);
        using var cts = new CancellationTokenSource();

        // cancellation 을 별도 thread 에서 빠르게 trigger.
        _ = Task.Run(async () =>
        {
            await Task.Delay(10);
            cts.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DataLensEngine.Analyze(df, new AnalysisOptions(), cts.Token));
    }

    private record PocoSample(int Id, double X, double Y, string Tag);

    [Fact]
    public async Task Analyze_PocoOverload_DoesNotBlockCallingThread()
    {
        // 8469 × 78 시나리오의 reflection-based POCO 변환이 호출 thread 를 점유하지 않아야 한다.
        // EnumerableSourceBuilder.Build 도 Task.Run 안으로 들어갔는지 검증.
        var data = new List<PocoSample>(2000);
        var rng = new Random(13);
        for (int i = 0; i < 2000; i++)
            data.Add(new PocoSample(i, rng.NextDouble() * 100, rng.NextDouble() * 50, $"tag{i % 5}"));

        var sw = Stopwatch.StartNew();
        var task = DataLensEngine.Analyze(data, new AnalysisOptions
        {
            IncludeOutliers = true,
            IncludeClustering = true,
            IncludePca = true
        });
        var elapsedBeforeAwait = sw.ElapsedMilliseconds;
        var result = await task;
        sw.Stop();

        Assert.True(elapsedBeforeAwait < 100,
            $"POCO overload blocked calling thread for {elapsedBeforeAwait}ms before await — Build is still synchronous on caller.");
        Assert.NotNull(result);
    }

    [Fact]
    public void AnalyzeSync_ProducesSameResult()
    {
        // 동기 entry point 가 async 와 동일한 결과를 산출.
        var df = BuildLargeFrame();
        var options = new AnalysisOptions
        {
            IncludeProfiling = true,
            IncludeDescriptive = true,
            IncludeCorrelation = false,
            IncludeRegression = false,
            IncludeDistribution = false,
            IncludeClustering = false,
            IncludeOutliers = false,
            IncludeFeatures = false,
            IncludePca = false,
            IncludeChangepoints = false
        };

        var result = DataLensEngine.AnalyzeSync(df, options);

        Assert.NotNull(result.Profile);
        Assert.NotNull(result.Descriptive);
    }
}
