namespace DataLens.Models;

public class ClusterReport
{
    public KMeansReport? KMeans { get; init; }
    public DbscanReport? Dbscan { get; init; }
    public HierarchicalReport? Hierarchical { get; init; }
    public uint? OptimalK { get; init; }
    public double[]? GapValues { get; init; }
}

public class KMeansReport
{
    public uint K { get; init; }
    public double Wcss { get; init; }
    public uint Iterations { get; init; }
    public uint[] Labels { get; init; } = [];
    public List<ClusterSummary> ClusterSizes { get; init; } = [];
}

public class DbscanReport
{
    public uint NClusters { get; init; }
    public uint NoiseCount { get; init; }
    public int[] Labels { get; init; } = [];
}

public class HierarchicalReport
{
    public uint NClusters { get; init; }
    public uint[] Labels { get; init; } = [];
    public double[] MergeDistances { get; init; } = [];
}

public class ClusterSummary
{
    public uint ClusterId { get; init; }
    public int Size { get; init; }
    public double Percentage { get; init; }
}
