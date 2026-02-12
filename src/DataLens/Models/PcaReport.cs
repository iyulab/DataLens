namespace DataLens.Models;

public class PcaReport
{
    public uint NComponents { get; init; }
    public double[] ExplainedVariance { get; init; } = [];
    public double[] CumulativeVariance { get; init; } = [];
    public double TotalExplainedVariance { get; init; }
}
