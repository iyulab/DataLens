using DataLens.Adapters;
using DataLens.Models;

namespace DataLens.Analyzers;

/// <summary>
/// 범주 피처와 범주 타깃 사이의 상호정보량. 네이티브 분석(ANOVA · MutualInfo)은 숫자 행렬만 읽으므로 범주 열은
/// 그 표에 들어가지 못하고, 피처가 전부 범주인 표는 순위가 통째로 비었다. 두 범주 열의 상호정보량은 일치표(contingency
/// table)에서 바로 구해지므로 행렬을 거치지 않는다.
/// </summary>
internal static class CategoricalMutualInfoScorer
{
    /// <summary>
    /// 타깃이 범주 열일 때 다른 모든 범주 열의 점수(내림차순). 타깃이 없거나 숫자 열이거나 점수를 낼 열이 없으면 null.
    /// </summary>
    public static CategoricalMutualInfoSummary? Rank(DataAdapter adapter, string? target)
    {
        if (string.IsNullOrEmpty(target) || !adapter.CategoricalColumns.Contains(target))
            return null;

        var y = adapter.ToLabels(target);
        var features = new List<CategoricalMutualInfoFeatureResult>();
        var categorical = adapter.CategoricalColumns;
        for (int i = 0; i < categorical.Count; i++)
        {
            var name = categorical[i];
            if (string.Equals(name, target, StringComparison.Ordinal)) continue;

            var (mi, levels, rows) = Score(adapter.ToLabels(name), y);
            features.Add(new CategoricalMutualInfoFeatureResult
            {
                Name = name,
                Index = i,
                Mi = mi,
                Levels = levels,
                Rows = rows
            });
        }

        if (features.Count == 0) return null;
        features.Sort((a, b) => b.Mi.CompareTo(a.Mi));
        return new CategoricalMutualInfoSummary { Features = features };
    }

    /// <summary>
    /// 두 레이블 배열의 상호정보량(nat). 한쪽이라도 레이블이 없는 행(<see cref="uint.MaxValue"/>)은 뺀다.
    /// 일치표의 plug-in 추정은 칸이 많고 행이 적을수록 위로 치우치므로(독립인 열도 양의 값) Miller–Madow 보정
    /// <c>(kx−1)(ky−1)/(2n)</c> 을 빼고 0 에서 자른다 — 보정이 없으면 고유값이 많은 열(식별자 · 자유 문장)이 항상 맨 위에 선다.
    /// </summary>
    internal static (double Mi, int Levels, int Rows) Score(uint[] x, uint[] y)
    {
        int n = Math.Min(x.Length, y.Length);
        var joint = new Dictionary<(uint, uint), int>();
        var xCount = new Dictionary<uint, int>();
        var yCount = new Dictionary<uint, int>();
        int rows = 0;
        for (int i = 0; i < n; i++)
        {
            if (x[i] == uint.MaxValue || y[i] == uint.MaxValue) continue;
            rows++;
            joint[(x[i], y[i])] = joint.GetValueOrDefault((x[i], y[i])) + 1;
            xCount[x[i]] = xCount.GetValueOrDefault(x[i]) + 1;
            yCount[y[i]] = yCount.GetValueOrDefault(y[i]) + 1;
        }

        if (rows < 3 || xCount.Count < 2 || yCount.Count < 2)
            return (0.0, xCount.Count, rows);

        double mi = 0.0;
        foreach (var ((xv, yv), c) in joint)
        {
            double pxy = (double)c / rows;
            mi += pxy * Math.Log(pxy * rows * rows / ((double)xCount[xv] * yCount[yv]));
        }

        mi -= (xCount.Count - 1.0) * (yCount.Count - 1.0) / (2.0 * rows);
        return (Math.Max(0.0, mi), xCount.Count, rows);
    }
}
