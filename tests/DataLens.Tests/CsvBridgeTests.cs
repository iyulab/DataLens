using DataLens.Adapters;

namespace DataLens.Tests;

public class CsvBridgeTests : IDisposable
{
    private readonly string _tempDir;

    public CsvBridgeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"datalens-csvbridge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string WriteCsv(string name, string contents)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public async Task LoadAsync_DefaultOptions_PreservesDuplicateRows()
    {
        // Two identical rows + one unique row.
        var path = WriteCsv("dup.csv", "x,y\n1,2\n1,2\n3,4\n");

        var df = await CsvBridge.LoadAsync(path);

        Assert.Equal(3, df.RowCount);
    }

    [Fact]
    public async Task LoadAsync_OptInRemoveDuplicates_DropsExactDuplicateRows()
    {
        var path = WriteCsv("dup.csv", "x,y\n1,2\n1,2\n3,4\n");

        var df = await CsvBridge.LoadAsync(path, new CsvLoadOptions { RemoveDuplicateRows = true });

        Assert.Equal(2, df.RowCount);
    }

    [Fact]
    public async Task LoadAsync_DefaultOptions_ProfileReportsActualDuplicateRowCount()
    {
        var path = WriteCsv("profile-dup.csv", "x,y\n1,2\n1,2\n1,2\n3,4\n");

        var result = await DataLensEngine.Analyze(path, new AnalysisOptions
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
        });

        // Without silent dedup, ProfileReport must observe all 4 input rows.
        // (Pre-0.6.0 it would have reported 2 — the bug this PR fixes.)
        Assert.NotNull(result.Profile);
        Assert.Equal(4, result.Profile!.RowCount);
    }

    [Fact]
    public async Task LoadAsync_FileNotFound_Throws()
    {
        var path = Path.Combine(_tempDir, "missing.csv");

        await Assert.ThrowsAsync<FileNotFoundException>(() => CsvBridge.LoadAsync(path));
    }
}
