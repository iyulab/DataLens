namespace DataLens.Models;

public class OutlierReport
{
    public IsolationForestReport? IsolationForest { get; init; }
    public LofReport? Lof { get; init; }
    public MahalanobisReport? Mahalanobis { get; init; }

    /// <summary>
    /// 단변량 (per-column) outlier 검출 결과 — Tukey IQR / 3σ / Hampel 각 방법별 컬럼 사전.
    /// 다변량 IF/LOF/Mahalanobis 와 직교 — 컬럼별 fence 기반 가시성 보강.
    /// </summary>
    public UnivariateOutlierReport? Univariate { get; init; }

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
