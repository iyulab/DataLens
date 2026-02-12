namespace DataLens.Models;

public class OutlierReport
{
    public IsolationForestReport? IsolationForest { get; init; }
    public LofReport? Lof { get; init; }
    public MahalanobisReport? Mahalanobis { get; init; }
    public int TotalRows { get; init; }
    public int OutlierCount { get; init; }
    public double OutlierPercentage { get; init; }
}

public class IsolationForestReport
{
    public double[] Scores { get; init; } = [];
    public bool[] Anomalies { get; init; } = [];
    public uint AnomalyCount { get; init; }
    public double Threshold { get; init; }
}

public class LofReport
{
    public double[] Scores { get; init; } = [];
    public bool[] Anomalies { get; init; } = [];
    public uint AnomalyCount { get; init; }
    public double Threshold { get; init; }
}

public class MahalanobisReport
{
    public double[] Distances { get; init; } = [];
    public bool[] Anomalies { get; init; } = [];
    public uint OutlierCount { get; init; }
    public double Threshold { get; init; }
}
