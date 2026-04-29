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

    /// <summary>
    /// 정규성 검정 + skewness/kurtosis 종합으로 부여한 분포 형태 라벨.
    /// IsNormal=true 면 <see cref="DistributionShape.NormalLike"/>; 아니면 skewness/kurtosis 기반 분류.
    /// </summary>
    public DistributionShape Shape { get; init; } = DistributionShape.Unknown;

    /// <summary>분포 라벨 부여에 사용된 왜도 (없으면 null — 샘플 부족 등).</summary>
    public double? Skewness { get; init; }

    /// <summary>분포 라벨 부여에 사용된 (excess) 첨도. 정규분포는 0, &gt; 0 leptokurtic, &lt; 0 platykurtic.</summary>
    public double? Kurtosis { get; init; }
}

/// <summary>
/// 컬럼 분포 형태 라벨. 정규성 검정 + 왜도 + (excess) 첨도 종합 판정.
/// </summary>
public enum DistributionShape
{
    /// <summary>샘플 부족 또는 검정 실패 — 라벨 미부여.</summary>
    Unknown,

    /// <summary>정규성 검정 통과 (IsNormal=true). 왜도/첨도가 정규에 가깝다.</summary>
    NormalLike,

    /// <summary>오른쪽으로 긴 꼬리 (skewness &gt; 0.5). lognormal-like.</summary>
    RightSkewed,

    /// <summary>왼쪽으로 긴 꼬리 (skewness &lt; -0.5).</summary>
    LeftSkewed,

    /// <summary>정규보다 두꺼운 꼬리 (excess kurtosis &gt; 1) — 이상치 많은 분포 신호.</summary>
    HeavyTailed,

    /// <summary>정규보다 평평 (excess kurtosis &lt; -1) — 균등분포에 가깝거나 다봉 가능성.</summary>
    Platykurtic,
}
