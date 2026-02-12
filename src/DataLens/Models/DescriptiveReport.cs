namespace DataLens.Models;

public class DescriptiveReport
{
    public List<ColumnDescriptive> Columns { get; init; } = [];
}

public class ColumnDescriptive
{
    public string Name { get; init; } = "";
    public int Count { get; init; }
    public int NullCount { get; init; }
    public double Mean { get; init; }
    public double Median { get; init; }
    public double Std { get; init; }
    public double Variance { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Q1 { get; init; }
    public double Q3 { get; init; }
    public double Iqr { get; init; }
    public double? Skewness { get; init; }
    public double? Kurtosis { get; init; }
}
