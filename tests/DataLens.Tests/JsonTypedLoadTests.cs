using DataLens.Adapters;

namespace DataLens.Tests;

public class JsonTypedLoadTests : IDisposable
{
    private readonly string _tempDir;

    public JsonTypedLoadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"datalens-jsontyped-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string WriteJson(string name, string contents)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public async Task LoadAsync_JsonNumberValues_PreservedAsNumericStrings()
    {
        var path = WriteJson("typed.json",
            """
            [
              { "x": 1, "y": 2.5, "name": "a" },
              { "x": 3, "y": 4.75, "name": "b" }
            ]
            """);

        var df = await CsvBridge.LoadAsync(path);

        Assert.Equal(2, df.RowCount);
        var xColumn = df.GetColumn("x").ToList();
        var yColumn = df.GetColumn("y").ToList();
        Assert.Equal("1", xColumn[0]);
        Assert.Equal("2.5", yColumn[0]);
        Assert.Equal("4.75", yColumn[1]);

        // Critical: an analyzer that runs ToNumericMatrix should now classify x/y as numeric
        // (pre-0.7.0 the strings would have failed to deserialize entirely).
        var adapter = new DataAdapter(df);
        Assert.Contains("x", adapter.NumericColumns);
        Assert.Contains("y", adapter.NumericColumns);
        Assert.Contains("name", adapter.CategoricalColumns);
    }

    [Fact]
    public async Task LoadAsync_JsonBooleanValues_AreNormalized()
    {
        var path = WriteJson("bools.json",
            """
            [
              { "flag": true,  "label": "a" },
              { "flag": false, "label": "b" }
            ]
            """);

        var df = await CsvBridge.LoadAsync(path);

        var flag = df.GetColumn("flag").ToList();
        Assert.Equal("true", flag[0]);
        Assert.Equal("false", flag[1]);
    }

    [Fact]
    public async Task LoadAsync_JsonNullValues_BecomeMissing()
    {
        var path = WriteJson("nulls.json",
            """
            [
              { "x": 1,    "name": "a"  },
              { "x": null, "name": null }
            ]
            """);

        var df = await CsvBridge.LoadAsync(path);

        var xColumn = df.GetColumn("x").ToList();
        var nameColumn = df.GetColumn("name").ToList();
        Assert.Equal("1", xColumn[0]);
        Assert.Equal("", xColumn[1]);
        Assert.Equal("a", nameColumn[0]);
        Assert.Equal("", nameColumn[1]);
    }
}
