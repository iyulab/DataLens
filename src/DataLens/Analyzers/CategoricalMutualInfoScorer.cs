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
    /// 두 레이블 배열의 상호정보량(nat) — 일치표의 plug-in 추정에서 <b>우연 수준</b>을 뺀 값, 0 에서 자른다. 한쪽이라도 레이블이
    /// 없는 행(<see cref="uint.MaxValue"/>)은 뺀다.
    /// plug-in 추정은 칸이 많고 행이 적을수록 위로 치우친다(독립인 열도 양의 값). 우연 수준은 «두 열의 행을 무작위로 짝지었을
    /// 때 추정의 기댓값»(순열 귀무, 초기하 분포의 정확한 기댓값 — 시드도 표본도 없다)이라 <b>독립인 열은 수준 수 · 행 수에
    /// 상관없이 0 근처</b>에 선다. 점근 보정(Miller–Madow <c>(kx−1)(ky−1)/(2n)</c>)은 수준당 행이 ≲ 2 인 희소 구간에서 편향을
    /// 다 걷지 못해(600 행 · 400 수준의 무관한 열이 0.10 nat, 약한 진짜 요인의 세 배) 순위를 뒤집었다.
    /// </summary>
    internal static (double Mi, int Levels, int Rows) Score(uint[] x, uint[] y)
    {
        var t = Tabulate(x, y);
        if (t.Rows < 3 || t.XCount.Count < 2 || t.YCount.Count < 2)
            return (0.0, t.XCount.Count, t.Rows);

        return (Math.Max(0.0, PlugIn(t) - ChanceLevel(t)), t.XCount.Count, t.Rows);
    }

    /// <summary>일치표의 plug-in 상호정보량(nat) — 보정 전.</summary>
    internal static double PlugIn(uint[] x, uint[] y) => PlugIn(Tabulate(x, y));

    /// <summary>두 열의 행을 무작위로 짝지었을 때 plug-in 추정의 기댓값(nat) — 순열 귀무의 정확한 평균.</summary>
    internal static double ChanceLevel(uint[] x, uint[] y) => ChanceLevel(Tabulate(x, y));

    private sealed record Table(Dictionary<(uint, uint), int> Joint, Dictionary<uint, int> XCount, Dictionary<uint, int> YCount, int Rows);

    private static Table Tabulate(uint[] x, uint[] y)
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

        return new Table(joint, xCount, yCount, rows);
    }

    private static double PlugIn(Table t)
    {
        double mi = 0.0;
        foreach (var ((xv, yv), c) in t.Joint)
        {
            double pxy = (double)c / t.Rows;
            mi += pxy * Math.Log(pxy * t.Rows * t.Rows / ((double)t.XCount[xv] * t.YCount[yv]));
        }

        return mi;
    }

    /// <summary>
    /// Σ<sub>a,b</sub> Σ<sub>k</sub> (k/N)·ln(N·k/(a·b))·P(k) — a, b 는 두 열의 수준별 행 수, N 은 행 수, k 는 한 칸의 행 수이고
    /// P(k) 는 그 칸이 k 가 될 초기하 확률. 같은 행 수를 가진 수준은 한 번에 센다(고유값이 많은 열은 대부분 a = 1 · 2 라
    /// 서로 다른 a 가 몇 개 안 된다) — 합 항의 수는 많아야 N·√(kx·ky) 이다.
    /// </summary>
    private static double ChanceLevel(Table t)
    {
        int n = t.Rows;
        var logFactorial = new double[n + 1];
        for (int i = 2; i <= n; i++) logFactorial[i] = logFactorial[i - 1] + Math.Log(i);

        var aSizes = t.XCount.Values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
        var bSizes = t.YCount.Values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());

        double expected = 0.0;
        foreach (var (a, aTimes) in aSizes)
        foreach (var (b, bTimes) in bSizes)
        {
            double head = logFactorial[a] + logFactorial[b] + logFactorial[n - a] + logFactorial[n - b] - logFactorial[n];
            double cell = 0.0;
            for (int k = Math.Max(1, a + b - n); k <= Math.Min(a, b); k++)
            {
                double logP = head - logFactorial[k] - logFactorial[a - k] - logFactorial[b - k] - logFactorial[n - a - b + k];
                cell += (double)k / n * Math.Log((double)n * k / ((double)a * b)) * Math.Exp(logP);
            }

            expected += cell * aTimes * bTimes;
        }

        return expected;
    }
}
