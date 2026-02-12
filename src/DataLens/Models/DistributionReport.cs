namespace DataLens.Models;

public class DistributionReport
{
    public List<ColumnDistribution> Columns { get; init; } = [];
}

public class ColumnDistribution
{
    public string Name { get; init; } = "";
    public uint SampleSize { get; init; }
    public double KsStatistic { get; init; }
    public double KsPValue { get; init; }
    public double JbStatistic { get; init; }
    public double JbPValue { get; init; }
    public double SwStatistic { get; init; }
    public double SwPValue { get; init; }
    public double AdStatistic { get; init; }
    public double AdPValue { get; init; }
    public bool IsNormal { get; init; }
}
