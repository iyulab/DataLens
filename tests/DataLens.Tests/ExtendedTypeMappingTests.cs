using DataLens.Adapters;

namespace DataLens.Tests;

public class ExtendedTypeMappingTests
{
    private enum Status
    {
        Pending = 0,
        Active = 1,
        Completed = 2,
        Cancelled = 3
    }

    private class WithEnum
    {
        public string Name { get; set; } = "";
        public Status Status { get; set; }
        public int Count { get; set; }
    }

    private class WithTimeSpan
    {
        public string Task { get; set; } = "";
        public TimeSpan Elapsed { get; set; }
    }

    private class WithDateTimeOffset
    {
        public string Tag { get; set; } = "";
        public DateTimeOffset At { get; set; }
    }

    private static AnalysisOptions MinimalOptions => new()
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

    [Fact]
    public async Task Enum_IsClassifiedAsNumeric_ViaUnderlyingValue()
    {
        var data = new[]
        {
            new WithEnum { Name = "a", Status = Status.Pending,   Count = 1 },
            new WithEnum { Name = "b", Status = Status.Active,    Count = 2 },
            new WithEnum { Name = "c", Status = Status.Completed, Count = 3 },
            new WithEnum { Name = "d", Status = Status.Cancelled, Count = 4 },
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        // enum 은 1급 — warning 발산 안 함.
        var enumWarnings = result.Warnings
            .Where(w => w.Analyzer == "EnumerableSource" && w.Message.Contains("Status"))
            .ToList();
        Assert.Empty(enumWarnings);

        // Status 컬럼이 Profile 에 numeric 으로 등장.
        var statusCol = result.Profile!.Columns.FirstOrDefault(c => c.Name == "Status");
        Assert.NotNull(statusCol);
    }

    [Fact]
    public async Task TimeSpan_ConvertsToTotalSeconds_ForNumericAnalysis()
    {
        var data = new[]
        {
            new WithTimeSpan { Task = "a", Elapsed = TimeSpan.FromSeconds(30) },
            new WithTimeSpan { Task = "b", Elapsed = TimeSpan.FromMinutes(2) },
            new WithTimeSpan { Task = "c", Elapsed = TimeSpan.FromHours(1) },
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        var tsWarnings = result.Warnings
            .Where(w => w.Analyzer == "EnumerableSource" && w.Message.Contains("Elapsed"))
            .ToList();
        Assert.Empty(tsWarnings);

        // TotalSeconds 평균: (30 + 120 + 3600) / 3 = 1250
        var elapsedDescriptive = result.Descriptive!.Columns
            .FirstOrDefault(c => c.Name == "Elapsed");
        Assert.NotNull(elapsedDescriptive);
        Assert.InRange(elapsedDescriptive!.Mean, 1249.5, 1250.5);
    }

    [Fact]
    public async Task DateTimeOffset_IsHandledLikeDateTime()
    {
        var data = new[]
        {
            new WithDateTimeOffset { Tag = "a", At = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new WithDateTimeOffset { Tag = "b", At = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.FromHours(9)) },
        };

        var result = await DataLensEngine.Analyze(data, MinimalOptions);

        var dtoWarnings = result.Warnings
            .Where(w => w.Analyzer == "EnumerableSource" && w.Message.Contains("At"))
            .ToList();
        Assert.Empty(dtoWarnings);
    }

    [Fact]
    public void DefaultTypeMapper_Enum_RoundsToUnderlyingNumeric()
    {
        var cell = DefaultTypeMapper.ToCell(Status.Completed, out var unsupported);
        Assert.False(unsupported);
        Assert.Equal("2", cell);
    }

    [Fact]
    public void DefaultTypeMapper_TimeSpan_RoundsToTotalSeconds()
    {
        var cell = DefaultTypeMapper.ToCell(TimeSpan.FromMinutes(1.5), out var unsupported);
        Assert.False(unsupported);
        Assert.Equal("90", cell);
    }

    [Fact]
    public void DefaultTypeMapper_IsSupported_CoversNewTypes()
    {
        Assert.True(DefaultTypeMapper.IsSupported(typeof(Status)));
        Assert.True(DefaultTypeMapper.IsSupported(typeof(Status?)));
        Assert.True(DefaultTypeMapper.IsSupported(typeof(TimeSpan)));
        Assert.True(DefaultTypeMapper.IsSupported(typeof(TimeSpan?)));
        Assert.True(DefaultTypeMapper.IsSupported(typeof(DateTimeOffset)));
        Assert.False(DefaultTypeMapper.IsSupported(typeof(Guid)));
    }
}
