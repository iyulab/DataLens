namespace DataLens.Models;

public class ProfileReport
{
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public List<ColumnProfile> Columns { get; init; } = [];
}

public class ColumnProfile
{
    public string Name { get; init; } = "";
    public int Index { get; init; }
    public string DataType { get; init; } = "unknown";
    public long ValidCount { get; init; }
    public long NullCount { get; init; }
    public double NullPercentage { get; init; }
    public double? Mean { get; init; }
    public double? StdDev { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
}
