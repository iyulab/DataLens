using System.Dynamic;
using System.Text.Json.Serialization;
using DataLens.Adapters;

namespace DataLens.Tests;

public class EnumerableSourceTests
{
    private record Sample(string Name, int Count, double Value);

    private class WithIgnore
    {
        public int Public { get; set; }

        [JsonIgnore]
        public string Skipped { get; set; } = "";

        [DataLensIgnore]
        public string AlsoSkipped { get; set; } = "";
    }

    private class KoreanColumns
    {
        public DateTime 주문일자 { get; set; }
        public decimal 금액 { get; set; }
        public string? 고객명 { get; set; }
    }

    private class UnsupportedTypes
    {
        public Guid Id { get; set; }
        public DayOfWeek When { get; set; }
        public string Name { get; set; } = "";
    }

    private static AnalysisOptions MinimalOptions => new()
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
        IncludeChangepoints = false
    };

    [Fact]
    public async Task Analyze_PocoCollection_BuildsProfile()
    {
        var data = new[]
        {
            new Sample("a", 1, 1.0),
            new Sample("b", 2, 2.5),
            new Sample("c", 3, 3.7),
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        Assert.NotNull(result.Profile);
        Assert.Equal(3, result.Profile!.RowCount);
        Assert.Equal(3, result.Profile.ColumnCount);
        var names = result.Profile.Columns.Select(c => c.Name).ToHashSet();
        Assert.Contains("Name", names);
        Assert.Contains("Count", names);
        Assert.Contains("Value", names);
    }

    [Fact]
    public async Task Analyze_PocoWithJsonIgnoreAndDataLensIgnore_SkipsMarkedProperties()
    {
        var data = new[]
        {
            new WithIgnore { Public = 1, Skipped = "x", AlsoSkipped = "y" },
            new WithIgnore { Public = 2, Skipped = "x", AlsoSkipped = "y" },
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        Assert.NotNull(result.Profile);
        var names = result.Profile!.Columns.Select(c => c.Name).ToHashSet();
        Assert.Contains("Public", names);
        Assert.DoesNotContain("Skipped", names);
        Assert.DoesNotContain("AlsoSkipped", names);
    }

    [Fact]
    public async Task Analyze_KoreanColumnNames_ArePreservedInResult()
    {
        var data = new[]
        {
            new KoreanColumns { 주문일자 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 금액 = 1000m, 고객명 = "갑" },
            new KoreanColumns { 주문일자 = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 금액 = 2500m, 고객명 = "을" },
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        Assert.NotNull(result.Profile);
        var names = result.Profile!.Columns.Select(c => c.Name).ToHashSet();
        Assert.Contains("주문일자", names);
        Assert.Contains("금액", names);
        Assert.Contains("고객명", names);
    }

    [Fact]
    public async Task Analyze_UnsupportedTypes_FallBackToStringAndEmitWarnings()
    {
        var data = new[]
        {
            new UnsupportedTypes { Id = Guid.NewGuid(), When = DayOfWeek.Monday, Name = "x" },
            new UnsupportedTypes { Id = Guid.NewGuid(), When = DayOfWeek.Tuesday, Name = "y" },
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        // Build still succeeds.
        Assert.NotNull(result.Profile);
        // Guid 는 여전히 unsupported. enum (DayOfWeek) 은 0.8.0 부터 1급 지원이므로 warning 발산하지 않는다.
        var sourceWarnings = result.Warnings.Where(w => w.Analyzer == "EnumerableSource").ToList();
        Assert.Contains(sourceWarnings, w => w.Message.Contains("Id"));
        Assert.DoesNotContain(sourceWarnings, w => w.Message.Contains("When"));
    }

    [Fact]
    public async Task Analyze_AnonymousObjects_Work()
    {
        var data = new[]
        {
            new { x = 1.0, y = 2.0 },
            new { x = 3.0, y = 4.0 },
            new { x = 5.0, y = 6.0 },
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        Assert.NotNull(result.Profile);
        Assert.Equal(3, result.Profile!.RowCount);
        Assert.Equal(2, result.Profile.ColumnCount);
    }

    [Fact]
    public async Task Analyze_DictionaryRows_UsesKeysAsColumns()
    {
        var data = new List<Dictionary<string, object?>>
        {
            new() { ["a"] = 1, ["b"] = 2.5 },
            new() { ["a"] = 2, ["b"] = 3.5, ["c"] = "extra" },
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        Assert.NotNull(result.Profile);
        var names = result.Profile!.Columns.Select(c => c.Name).ToHashSet();
        Assert.Contains("a", names);
        Assert.Contains("b", names);
        Assert.Contains("c", names);
    }

    [Fact]
    public async Task Analyze_ExpandoObject_UsesKeysAsColumns()
    {
        dynamic row1 = new ExpandoObject();
        row1.x = 1.0;
        row1.y = 10.0;

        dynamic row2 = new ExpandoObject();
        row2.x = 2.0;
        row2.y = 20.0;

        var data = new List<ExpandoObject> { row1, row2 };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        Assert.NotNull(result.Profile);
        Assert.Equal(2, result.Profile!.RowCount);
        var names = result.Profile.Columns.Select(c => c.Name).ToHashSet();
        Assert.Contains("x", names);
        Assert.Contains("y", names);
    }

    [Fact]
    public async Task Analyze_HeaderAliases_RemapColumnNames()
    {
        var data = new[]
        {
            new Sample("a", 1, 1.0),
            new Sample("b", 2, 2.0),
        };
        var sourceOptions = new EnumerableSourceOptions<Sample>
        {
            HeaderAliases = new Dictionary<string, string> { ["Count"] = "수량", ["Value"] = "값" }
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions, sourceOptions);

        var names = result.Profile!.Columns.Select(c => c.Name).ToHashSet();
        Assert.Contains("Name", names);
        Assert.Contains("수량", names);
        Assert.Contains("값", names);
        Assert.DoesNotContain("Count", names);
        Assert.DoesNotContain("Value", names);
    }

    [Fact]
    public async Task Analyze_IncludeProperties_RestrictsColumns()
    {
        var data = new[]
        {
            new Sample("a", 1, 1.0),
            new Sample("b", 2, 2.0),
        };
        var sourceOptions = new EnumerableSourceOptions<Sample>
        {
            IncludeProperties = ["Value"]
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions, sourceOptions);

        Assert.NotNull(result.Profile);
        Assert.Equal(1, result.Profile!.ColumnCount);
        Assert.Equal("Value", result.Profile.Columns[0].Name);
    }

    [Fact]
    public async Task Analyze_CustomColumns_OverrideReflection()
    {
        var data = new[]
        {
            new Sample("a", 1, 1.0),
            new Sample("b", 2, 2.0),
        };
        var sourceOptions = new EnumerableSourceOptions<Sample>
        {
            Columns =
            [
                new NamedColumn<Sample>("doubled_value", s => s.Value * 2),
                new NamedColumn<Sample>("name_upper", s => s.Name.ToUpperInvariant())
            ]
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions, sourceOptions);

        Assert.NotNull(result.Profile);
        Assert.Equal(2, result.Profile!.ColumnCount);
        var names = result.Profile.Columns.Select(c => c.Name).ToHashSet();
        Assert.Contains("doubled_value", names);
        Assert.Contains("name_upper", names);
    }

    [Fact]
    public async Task Analyze_NullSource_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            DataLensEngine.Analyze<Sample>(null!));
    }

    [Fact]
    public async Task Analyze_SingleEnumeration_NoMultipleEnumerationOfDeferredQuery()
    {
        int enumerationCount = 0;
        IEnumerable<Sample> Source()
        {
            enumerationCount++;
            yield return new Sample("a", 1, 1.0);
            yield return new Sample("b", 2, 2.0);
        }

        await DataLensEngine.Analyze(Source(), MinimalOptions);

        Assert.Equal(1, enumerationCount);
    }
}
