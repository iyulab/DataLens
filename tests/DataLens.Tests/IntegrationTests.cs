using System.Text.Json;
using DataLens.Adapters;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class IntegrationTests
{
    private static DataFrame CreateSampleDataFrame()
    {
        var rng = new Random(42);
        var data = new List<Dictionary<string, string>>();

        for (int i = 0; i < 100; i++)
        {
            var x = rng.NextDouble() * 10;
            var y = x * 2.5 + rng.NextDouble() * 2;
            var z = rng.NextDouble() * 100;
            var category = i % 3 == 0 ? "A" : (i % 3 == 1 ? "B" : "C");

            data.Add(new Dictionary<string, string>
            {
                ["X"] = x.ToString("F4"),
                ["Y"] = y.ToString("F4"),
                ["Z"] = z.ToString("F4"),
                ["Category"] = category
            });
        }

        return DataPipeline.FromData(data).ToDataFrame();
    }

    [Fact]
    public async Task FullPipeline_ProducesValidJson()
    {
        var df = CreateSampleDataFrame();
        var options = new AnalysisOptions
        {
            IncludeProfiling = true,
            IncludeDescriptive = true,
            IncludeCorrelation = true,
            IncludeRegression = true,
            IncludeDistribution = true,
            IncludeClustering = true,
            IncludeOutliers = true,
            IncludeFeatures = true,
            IncludePca = true,
            CorrelationThreshold = 0.7,
        };

        var result = await DataLensEngine.Analyze(df, options);
        var json = result.ToJson();

        Assert.NotNull(json);
        Assert.True(json.Length > 100);

        // JSON이 유효한지 확인
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task Profile_ReturnsExpectedStructure()
    {
        var df = CreateSampleDataFrame();
        var result = await DataLensEngine.Analyze(df, new AnalysisOptions
        {
            IncludeProfiling = true,
            IncludeDescriptive = false,
            IncludeCorrelation = false,
            IncludeRegression = false,
            IncludeDistribution = false,
            IncludeClustering = false,
            IncludeOutliers = false,
            IncludeFeatures = false,
            IncludePca = false,
        });

        Assert.NotNull(result.Profile);
        Assert.Equal(100, result.Profile.RowCount);
        Assert.Equal(4, result.Profile.ColumnCount);
        Assert.Null(result.Correlation);
        Assert.Null(result.Clusters);
    }

    [Fact]
    public async Task SelectiveAnalysis_OnlyRunsRequestedModules()
    {
        var df = CreateSampleDataFrame();
        var result = await DataLensEngine.Analyze(df, new AnalysisOptions
        {
            IncludeProfiling = false,
            IncludeDescriptive = true,
            IncludeCorrelation = false,
            IncludeRegression = false,
            IncludeDistribution = false,
            IncludeClustering = false,
            IncludeOutliers = false,
            IncludeFeatures = false,
            IncludePca = false,
        });

        Assert.Null(result.Profile);
        Assert.NotNull(result.Descriptive);
        Assert.Null(result.Correlation);
    }

    [Fact]
    public async Task SectionJson_ProducesPartialResult()
    {
        var df = CreateSampleDataFrame();
        var result = await DataLensEngine.Analyze(df, new AnalysisOptions
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
        });

        var profileJson = result.ToJson(Section.Profile);
        Assert.Contains("rowCount", profileJson);
        Assert.DoesNotContain("correlation", profileJson, StringComparison.OrdinalIgnoreCase);

        var descJson = result.ToJson(Section.Descriptive);
        Assert.Contains("columns", descJson);
    }
}
