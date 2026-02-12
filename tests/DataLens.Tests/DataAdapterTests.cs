using DataLens.Adapters;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

public class DataAdapterTests
{
    private static DataFrame CreateTestDataFrame()
    {
        var data = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["Age"] = "30", ["Score"] = "85.5", ["Grade"] = "A" },
            new() { ["Name"] = "Bob", ["Age"] = "25", ["Score"] = "90.0", ["Grade"] = "A" },
            new() { ["Name"] = "Charlie", ["Age"] = "35", ["Score"] = "75.3", ["Grade"] = "B" },
            new() { ["Name"] = "Diana", ["Age"] = "28", ["Score"] = "88.7", ["Grade"] = "A" },
            new() { ["Name"] = "Eve", ["Age"] = "32", ["Score"] = "92.1", ["Grade"] = "A" },
        };
        return DataPipeline.FromData(data).ToDataFrame();
    }

    [Fact]
    public void ClassifiesColumns_Correctly()
    {
        var adapter = new DataAdapter(CreateTestDataFrame());

        Assert.Contains("Age", adapter.NumericColumns);
        Assert.Contains("Score", adapter.NumericColumns);
        Assert.Contains("Name", adapter.CategoricalColumns);
        Assert.Contains("Grade", adapter.CategoricalColumns);
    }

    [Fact]
    public void ToNumericMatrix_ReturnsCorrectDimensions()
    {
        var adapter = new DataAdapter(CreateTestDataFrame());
        var matrix = adapter.ToNumericMatrix();

        Assert.Equal(5, matrix.GetLength(0));
        Assert.Equal(2, matrix.GetLength(1)); // Age, Score
    }

    [Fact]
    public void ToNumericMatrix_SpecificColumns()
    {
        var adapter = new DataAdapter(CreateTestDataFrame());
        var matrix = adapter.ToNumericMatrix("Age");

        Assert.Equal(5, matrix.GetLength(0));
        Assert.Equal(1, matrix.GetLength(1));
        Assert.Equal(30.0, matrix[0, 0]);
    }

    [Fact]
    public void ToArray_ReturnsCorrectValues()
    {
        var adapter = new DataAdapter(CreateTestDataFrame());
        var ages = adapter.ToArray("Age");

        Assert.Equal(5, ages.Length);
        Assert.Equal(30.0, ages[0]);
        Assert.Equal(25.0, ages[1]);
    }

    [Fact]
    public void ToLabels_EncodesCorrectly()
    {
        var adapter = new DataAdapter(CreateTestDataFrame());
        var labels = adapter.ToLabels("Grade");

        // A=0, B=1
        Assert.Equal(0u, labels[0]); // Alice: A
        Assert.Equal(0u, labels[1]); // Bob: A
        Assert.Equal(1u, labels[2]); // Charlie: B
    }

    [Fact]
    public void ToCleanMatrix_RemovesNaN()
    {
        var data = new List<Dictionary<string, string>>
        {
            new() { ["X"] = "1.0", ["Y"] = "2.0" },
            new() { ["X"] = "3.0", ["Y"] = "" },     // Y is missing
            new() { ["X"] = "5.0", ["Y"] = "6.0" },
        };
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        var clean = adapter.ToCleanMatrix("X", "Y");

        Assert.Equal(2, clean.GetLength(0)); // row with missing Y is removed
        Assert.Equal(2, clean.GetLength(1));
    }

    [Fact]
    public void ToCsvString_ProducesCsv()
    {
        var adapter = new DataAdapter(CreateTestDataFrame());
        var csv = adapter.ToCsvString();

        Assert.Contains("Name", csv);
        Assert.Contains("Alice", csv);
    }

    [Fact]
    public void RowCount_And_ColumnCount()
    {
        var adapter = new DataAdapter(CreateTestDataFrame());

        Assert.Equal(5, adapter.RowCount);
        Assert.Equal(4, adapter.ColumnCount);
    }

    [Fact]
    public void ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DataAdapter(null!));
    }

    [Fact]
    public void EmptyDataFrame_HasNoColumns()
    {
        var df = DataPipeline.FromData([]).ToDataFrame();
        var adapter = new DataAdapter(df);

        Assert.Empty(adapter.NumericColumns);
        Assert.Empty(adapter.CategoricalColumns);
        Assert.Equal(0, adapter.RowCount);
    }

    [Fact]
    public void ToNumericMatrix_ThrowsWhenNoNumericColumns()
    {
        var data = new List<Dictionary<string, string>>
        {
            new() { ["Name"] = "Alice", ["City"] = "Seoul" },
        };
        var df = DataPipeline.FromData(data).ToDataFrame();
        var adapter = new DataAdapter(df);

        Assert.Throws<InvalidOperationException>(() => adapter.ToNumericMatrix());
    }
}
