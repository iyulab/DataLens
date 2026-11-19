namespace DataLens.Models;

public class FeatureReport
{
    public string? TargetColumn { get; init; }
    public FeatureImportanceSummary? Importance { get; init; }
    public AnovaSummary? Anova { get; init; }
    public MutualInfoSummary? MutualInfo { get; init; }
    public PermutationSummary? Permutation { get; init; }

    /// <summary>
    /// 범주 타깃과 범주 피처 사이의 상호정보량(nat, Miller–Madow 보정). <see cref="MutualInfo"/> 는 숫자 피처만 읽으므로
    /// 범주 피처는 여기서 따로 점수를 받는다 — 두 추정기가 달라 같은 눈금이 아니다.
    /// </summary>
    public CategoricalMutualInfoSummary? CategoricalMutualInfo { get; init; }

    /// <summary>
    /// 숫자 타깃과 범주 피처 사이의 상관비(η², 자유도 보정). 숫자 타깃의 <see cref="Permutation"/> 은 숫자 피처만 읽으므로 범주 피처는 여기서
    /// 따로 점수를 받는다 — <see cref="CategoricalMutualInfo"/> 와는 다른 양(타깃 분산 대비 설명 몫)이라 눈금이 같지 않다.
    /// </summary>
    public CategoricalCorrelationRatioSummary? CategoricalCorrelationRatio { get; init; }
}

public class CategoricalCorrelationRatioSummary
{
    public List<CategoricalCorrelationRatioFeatureResult> Features { get; init; } = [];
}

public class CategoricalCorrelationRatioFeatureResult
{
    public string Name { get; init; } = "";
    public int Index { get; init; }

    /// <summary>타깃 분산 중 이 열의 값이 설명하는 몫(0..1, 자유도 보정 — 음수는 0).</summary>
    public double Ratio { get; init; }

    /// <summary>점수에 쓴 행에서 이 열이 가진 서로 다른 값의 수.</summary>
    public int Levels { get; init; }

    /// <summary>점수에 쓴 행 수(열 값과 타깃 숫자가 모두 있는 행).</summary>
    public int Rows { get; init; }
}

public class CategoricalMutualInfoSummary
{
    public List<CategoricalMutualInfoFeatureResult> Features { get; init; } = [];
}

public class CategoricalMutualInfoFeatureResult
{
    public string Name { get; init; } = "";
    public int Index { get; init; }
    public double Mi { get; init; }

    /// <summary>점수에 쓴 행에서 이 열이 가진 서로 다른 값의 수.</summary>
    public int Levels { get; init; }

    /// <summary>점수에 쓴 행 수(열 값과 타깃이 모두 있는 행).</summary>
    public int Rows { get; init; }
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
