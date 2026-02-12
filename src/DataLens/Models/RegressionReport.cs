namespace DataLens.Models;

public class RegressionReport
{
    public string? TargetColumn { get; init; }
    public List<RegressionEntry> Entries { get; init; } = [];
}

public class RegressionEntry
{
    public string FeatureColumn { get; init; } = "";
    public double Slope { get; init; }
    public double Intercept { get; init; }
    public double RSquared { get; init; }
    public double AdjRSquared { get; init; }
    public double FPValue { get; init; }
}
