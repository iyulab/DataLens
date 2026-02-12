namespace DataLens.Models;

public class FeatureReport
{
    public string? TargetColumn { get; init; }
    public FeatureImportanceSummary? Importance { get; init; }
    public AnovaSummary? Anova { get; init; }
    public MutualInfoSummary? MutualInfo { get; init; }
    public PermutationSummary? Permutation { get; init; }
}

public class FeatureImportanceSummary
{
    public List<FeatureScore> Scores { get; init; } = [];
    public double ConditionNumber { get; init; }
    public uint LowVarianceCount { get; init; }
    public uint HighCorrPairsCount { get; init; }
}

public class FeatureScore
{
    public string Name { get; init; } = "";
    public int Index { get; init; }
    public double Score { get; init; }
}

public class AnovaSummary
{
    public List<AnovaFeatureResult> Features { get; init; } = [];
    public uint SelectedCount { get; init; }
}

public class AnovaFeatureResult
{
    public string Name { get; init; } = "";
    public int Index { get; init; }
    public double FStatistic { get; init; }
    public double PValue { get; init; }
}

public class MutualInfoSummary
{
    public List<MutualInfoFeatureResult> Features { get; init; } = [];
}

public class MutualInfoFeatureResult
{
    public string Name { get; init; } = "";
    public int Index { get; init; }
    public double Mi { get; init; }
}

public class PermutationSummary
{
    public double BaselineScore { get; init; }
    public List<PermutationFeatureResult> Features { get; init; } = [];
}

public class PermutationFeatureResult
{
    public string Name { get; init; } = "";
    public int Index { get; init; }
    public double Importance { get; init; }
    public double StdDev { get; init; }
}
