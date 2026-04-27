using DataLens.Adapters;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class ColumnFilterTests
{
    private record Record(string Id, string Name, double Score, double Value);

    private static DataFrame BuildFrame()
    {
        var rng = new Random(11);
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 30; i++)
        {
            data.Add(new Dictionary<string, string>
            {
                ["_id"] = i.ToString(),
                ["customer_id"] = (i * 7).ToString(),
                ["amount"] = (rng.NextDouble() * 100).ToString("R"),
                ["quantity"] = rng.Next(1, 50).ToString()
            });
        }
        return DataPipeline.FromData(data).ToDataFrame();
    }

    [Fact]
    public void Adapter_ExcludeColumns_RemovesFromAllProjections()
    {
        var df = BuildFrame();
        var adapter = new DataAdapter(df,
            includeColumns: null,
            excludeColumns: new[] { "_id", "customer_id" });

        Assert.DoesNotContain("_id", adapter.AllColumns);
        Assert.DoesNotContain("customer_id", adapter.AllColumns);
        Assert.DoesNotContain("_id", adapter.NumericColumns);
        Assert.DoesNotContain("customer_id", adapter.NumericColumns);
        Assert.Contains("amount", adapter.AllColumns);
        Assert.Contains("quantity", adapter.AllColumns);
        Assert.Equal(2, adapter.ColumnCount);
    }

    [Fact]
    public void Adapter_IncludeColumns_RestrictsToAllowlist()
    {
        var df = BuildFrame();
        var adapter = new DataAdapter(df,
            includeColumns: new[] { "amount" },
            excludeColumns: null);

        Assert.Single(adapter.AllColumns);
        Assert.Equal("amount", adapter.AllColumns[0]);
    }

    [Fact]
    public void Adapter_NonExistentColumns_AreSilentlyIgnored()
    {
        var df = BuildFrame();
        var adapter = new DataAdapter(df,
            includeColumns: null,
            excludeColumns: new[] { "nonexistent", "_id" });

        Assert.DoesNotContain("_id", adapter.AllColumns);
        Assert.Equal(3, adapter.ColumnCount);
    }

    [Fact]
    public async Task Engine_AnalysisOptions_ExcludeColumns_PropagatesToAdapter()
    {
        var df = BuildFrame();
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
            IncludeChangepoints = false,
            ExcludeColumns = new[] { "_id", "customer_id" }
        };

        var result = await DataLensEngine.Analyze(df, options);

        Assert.NotNull(result.Profile);
        var names = result.Profile!.Columns.Select(c => c.Name).ToHashSet();
        Assert.DoesNotContain("_id", names);
        Assert.DoesNotContain("customer_id", names);
        Assert.Contains("amount", names);
    }
}
