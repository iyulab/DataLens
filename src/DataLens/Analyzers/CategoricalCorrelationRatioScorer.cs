using DataLens.Adapters;
using DataLens.Models;

namespace DataLens.Analyzers;

/// <summary>
/// Categorical features against a numeric target: how much of the target's variance the levels of each column explain (the correlation
/// ratio, eta², corrected for the degrees of freedom the levels use). The native analyses read a numeric matrix, so a categorical
/// column cannot take part in the numeric target's permutation importance and a table with categorical features had none of them scored.
/// The sibling of <see cref="CategoricalMutualInfoScorer"/> for a numeric target; the two are different quantities and are reported apart.
/// </summary>
internal static class CategoricalCorrelationRatioScorer
{
    public static CategoricalCorrelationRatioSummary? Rank(DataAdapter adapter, string? target)
    {
        if (string.IsNullOrEmpty(target) || !adapter.NumericColumns.Contains(target))
            return null;

        var y = adapter.ToArray(target);
        var features = new List<CategoricalCorrelationRatioFeatureResult>();
        var categorical = adapter.CategoricalColumns;
        for (int i = 0; i < categorical.Count; i++)
        {
            var name = categorical[i];
            if (string.Equals(name, target, StringComparison.Ordinal)) continue;

            var (ratio, levels, rows) = Score(adapter.ToLabels(name), y);
            features.Add(new CategoricalCorrelationRatioFeatureResult
            {
                Name = name,
                Index = i,
                Ratio = ratio,
                Levels = levels,
                Rows = rows
            });
        }

        if (features.Count == 0) return null;
        features.Sort((a, b) => b.Ratio.CompareTo(a.Ratio));
        return new CategoricalCorrelationRatioSummary { Features = features };
    }

    /// <summary>
    /// Share of the target's variance the column's levels explain, adjusted: <c>1 − (1 − η²)(n − 1)/(n − k)</c> for k levels, floored at 0
    /// — without the adjustment a column with a level per row explains everything. Rows without a level or without a target number are left out.
    /// </summary>
    internal static (double Ratio, int Levels, int Rows) Score(uint[] levels, double[] target)
    {
        int n = Math.Min(levels.Length, target.Length);
        var sums = new Dictionary<uint, double>();
        var counts = new Dictionary<uint, int>();
        double total = 0;
        int rows = 0;
        for (int i = 0; i < n; i++)
        {
            if (levels[i] == uint.MaxValue || double.IsNaN(target[i]) || double.IsInfinity(target[i])) continue;
            rows++;
            total += target[i];
            sums[levels[i]] = sums.GetValueOrDefault(levels[i]) + target[i];
            counts[levels[i]] = counts.GetValueOrDefault(levels[i]) + 1;
        }

        if (rows < 3 || counts.Count < 2 || counts.Count >= rows)
            return (0.0, counts.Count, rows);

        double mean = total / rows;
        double between = 0;
        foreach (var (level, count) in counts)
            between += count * Math.Pow(sums[level] / count - mean, 2);

        double all = 0;
        for (int i = 0; i < n; i++)
        {
            if (levels[i] == uint.MaxValue || double.IsNaN(target[i]) || double.IsInfinity(target[i])) continue;
            all += (target[i] - mean) * (target[i] - mean);
        }
        if (all <= 0) return (0.0, counts.Count, rows);

        double eta2 = between / all;
        double adjusted = 1 - (1 - eta2) * (rows - 1.0) / (rows - counts.Count);
        return (Math.Max(0.0, adjusted), counts.Count, rows);
    }
}
