namespace DataLens.Models;

public class ClusterReport
{
    public KMeansReport? KMeans { get; init; }
    public DbscanReport? Dbscan { get; init; }
    public HierarchicalReport? Hierarchical { get; init; }
    public HdbscanReport? Hdbscan { get; init; }

    /// <summary>
    /// Gap statistic 으로 추정한 최적 K. <c>null</c> 이면 GapStatistic 호출 실패 (기본 K=3 으로 fallback).
    /// </summary>
    public uint? OptimalK { get; init; }

    /// <summary>
    /// Gap statistic 값 (Tibshirani 2001) — K=2..<see cref="AnalysisOptions.MaxClusters"/> 에 대응.
    /// 인덱스 매핑: <c>GapValues[k - 2]</c> = K=k 의 gap. 길이 = <c>MaxClusters - 1</c>.
    /// 값이 클수록 해당 K 에서 클러스터 분리가 균일 분포 대비 양호함을 의미. <c>null</c> 이면 미산출.
    /// </summary>
    public double[]? GapValues { get; init; }
}

public class KMeansReport
{
    public uint K { get; init; }
    public double Wcss { get; init; }
    public uint Iterations { get; init; }
    public uint[] Labels { get; init; } = [];
    public List<ClusterSummary> ClusterSizes { get; init; } = [];

    /// <summary>true이면 MiniBatchKMeans로 계산됨, false이면 표준 KMeans.</summary>
    public bool UsedMiniBatch { get; init; }
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

public class HdbscanReport
{
    /// <summary>탐지된 클러스터 수 (노이즈 제외).</summary>
    public uint NClusters { get; init; }

    /// <summary>노이즈로 분류된 데이터 포인트 수.</summary>
    public uint NoiseCount { get; init; }

    /// <summary>클러스터 라벨 (-1 = 노이즈).</summary>
    public int[] Labels { get; init; } = [];

    /// <summary>클러스터 멤버십 확률.</summary>
    public double[] Probabilities { get; init; } = [];
}

public class ClusterSummary
{
    public uint ClusterId { get; init; }
    public int Size { get; init; }
    public double Percentage { get; init; }
}
