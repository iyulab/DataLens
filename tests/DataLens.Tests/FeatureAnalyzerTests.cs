using DataLens.Adapters;
using DataLens.Analyzers;
using DataLens.Models;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

/// <summary>
/// FeatureAnalyzer target-aware importance — 타깃 컬럼이 피처 행렬에서 제외되어야
/// permutation importance가 self-prediction으로 0이 되지 않는다.
/// </summary>
public class FeatureAnalyzerTests
{
    // 타깃 Y = 2*X (+미세 noise), N = 무관 noise. 타깃을 행렬에서 제외하면
    // X는 Y를 예측하므로 permutation importance가 0이 아니어야 하고,
    // 결과에 타깃 Y가 self-reference로 등장하지 않아야 한다.
    private static DataAdapter BuildAdapter(int n = 80)
    {
        var rng = new Random(7);
        var data = new List<Dictionary<string, string>>(n);
        for (int i = 0; i < n; i++)
        {
            double x = i;
            double noise = rng.NextDouble() * 100;
            double y = 2 * x + (rng.NextDouble() - 0.5); // X로 강하게 예측 가능
            data.Add(new Dictionary<string, string>
            {
                ["X"] = x.ToString("R"),
                ["N"] = noise.ToString("R"),
                ["Y"] = y.ToString("R"),
            });
        }
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    // The same columns in any unit flag the same low-variance features: a constant column and a
    // column that barely moves relative to its size are low-variance whether they are in metres or nanometres.
    private static DataAdapter BuildScaledAdapter(double scale, int n = 120)
    {
        var rng = new Random(11);
        var data = new List<Dictionary<string, string>>(n);
        for (int i = 0; i < n; i++)
        {
            var a = rng.NextDouble() * 50;
            data.Add(new Dictionary<string, string>
            {
                ["Constant"] = (5.0 * scale).ToString("R"),
                ["Pinned"] = ((1000.0 + (i % 3) * 1e-7) * scale).ToString("R"),
                ["A"] = (a * scale).ToString("R"),
                ["B"] = (rng.NextDouble() * 10 * scale).ToString("R"),
                ["Y"] = (3 * a + rng.NextDouble()).ToString("R"),
            });
        }
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    [Theory]
    [InlineData(1e-9)]
    [InlineData(1e9)]
    public async Task LowVarianceCount_DoesNotDependOnTheUnitOfTheData(double scale)
    {
        var options = new AnalysisOptions { TargetColumn = "Y" };

        var inUnits = await new FeatureAnalyzer().AnalyzeAsync(BuildScaledAdapter(1.0), options);
        var scaled = await new FeatureAnalyzer().AnalyzeAsync(BuildScaledAdapter(scale), options);

        var flaggedInUnits = inUnits.Importance!.LowVarianceCount;
        Assert.True(flaggedInUnits >= 2, $"the constant and the pinned column should be flagged, got {flaggedInUnits}");
        Assert.Equal(flaggedInUnits, scaled.Importance!.LowVarianceCount);
    }

    [Fact]
    public async Task NumericTarget_ExcludedFromMatrix_PermutationNotAllZero()
    {
        var adapter = BuildAdapter();

        var report = await new FeatureAnalyzer().AnalyzeAsync(
            adapter, new AnalysisOptions { TargetColumn = "Y" });

        Assert.NotNull(report.Permutation);
        var feats = report.Permutation!.Features;

        // (1) 타깃 Y가 피처 결과에 self-reference로 등장하지 않는다.
        Assert.DoesNotContain(feats, f => f.Name == "Y");
        // 피처는 X, N 둘뿐(타깃 제외).
        Assert.Equal(2, feats.Count);

        // (2) 핵심 회귀: 실피처 importance가 전부 0이 아니다(이전엔 self-prediction으로 전부 0).
        Assert.Contains(feats, f => Math.Abs(f.Importance) > 1e-9);

        // (3) 예측적 피처 X가 무관 noise N보다 중요.
        var x = feats.First(f => f.Name == "X");
        var noise = feats.First(f => f.Name == "N");
        Assert.True(x.Importance > noise.Importance,
            $"X({x.Importance}) should outrank noise N({noise.Importance})");
    }

    // The structural summary (scores, condition number, correlated pairs) is about the features. A numeric target that
    // tracks one of them used to join it: the target was ranked among its own features and its pair with the feature that
    // predicts it was counted as multicollinearity.
    [Fact]
    public async Task NumericTarget_IsNotAFeatureOfTheStructuralSummary()
    {
        var adapter = BuildAdapter();

        var withTarget = await new FeatureAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions { TargetColumn = "Y" });
        var withoutTarget = await new FeatureAnalyzer().AnalyzeAsync(adapter, new AnalysisOptions());

        Assert.NotNull(withTarget.Importance);
        Assert.Equal(["N", "X"], withTarget.Importance!.Scores.Select(f => f.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(0u, withTarget.Importance.HighCorrPairsCount);   // X and Y track each other; Y is the target, not a feature
        // no target named: nothing is the target, so X and Y are two features that say the same thing (the check sees the pair)
        Assert.Equal(["N", "X", "Y"], withoutTarget.Importance!.Scores.Select(f => f.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.True(withoutTarget.Importance.HighCorrPairsCount >= 1u);
    }

    // A categorical target with a missing label: the row leaves the matrix with its label, and each
    // ANOVA result is named after its own column whatever its place in the p-value order.
    [Fact]
    public async Task CategoricalTarget_AnovaNamesItsColumns_AndDropsUnlabelledRows()
    {
        var rng = new Random(3);
        var data = new List<Dictionary<string, string>>();
        for (int i = 0; i < 60; i++)
        {
            var cls = i % 3;
            data.Add(new Dictionary<string, string>
            {
                ["Noise"] = (rng.NextDouble() * 10).ToString("R"),
                ["Separates"] = (cls * 10 + rng.NextDouble()).ToString("R"),
                ["Class"] = i == 7 ? "" : $"c{cls}",
            });
        }
        var adapter = new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
        var warnings = new List<AnalysisWarning>();

        var report = await new FeatureAnalyzer().AnalyzeAsync(
            adapter, new AnalysisOptions { TargetColumn = "Class" }, warnings);

        Assert.Empty(warnings);
        Assert.NotNull(report.Anova);
        var first = report.Anova!.Features[0];
        Assert.Equal("Separates", first.Name);
        Assert.Equal(1, first.Index);
        Assert.True(first.PValue < 1e-6, $"p = {first.PValue}");
        Assert.Equal("Noise", report.Anova.Features[1].Name);
    }

    // Native analyses read a numeric matrix, so a table whose features are all categories left the report empty and the
    // ranking blank. Two categorical columns are scored on their contingency table with the categorical target.
    private static DataAdapter BuildAllCategoricalAdapter(int n = 600)
    {
        var rng = new Random(3);
        string[] levels = ["Phishy", "Suspicious", "Legitimate"];
        var data = new List<Dictionary<string, string>>(n);
        for (int i = 0; i < n; i++)
        {
            var y = levels[rng.Next(3)];
            data.Add(new Dictionary<string, string>
            {
                ["Strong"] = rng.NextDouble() < 0.85 ? y : levels[rng.Next(3)],
                ["Weak"] = rng.NextDouble() < 0.45 ? y : levels[rng.Next(3)],
                ["Noise"] = levels[rng.Next(3)],
                ["RowId"] = $"row-{i}",
                ["Label"] = y,
            });
        }
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    [Fact]
    public async Task AllCategoricalFeatures_AreRankedAgainstACategoricalTarget()
    {
        var report = await new FeatureAnalyzer().AnalyzeAsync(
            BuildAllCategoricalAdapter(), new AnalysisOptions { TargetColumn = "Label" });

        Assert.NotNull(report.CategoricalMutualInfo);
        var ranked = report.CategoricalMutualInfo!.Features;
        Assert.DoesNotContain(ranked, f => f.Name == "Label");
        Assert.Equal(["Strong", "Weak"], ranked.Take(2).Select(f => f.Name).ToArray());
        Assert.True(ranked[0].Mi > ranked[1].Mi);
        Assert.Equal(3, ranked[0].Levels);
        Assert.Equal(600, ranked[0].Rows);
    }

    // A column with a different value on every row names each row, so its plain table estimate equals the entropy of
    // the target - without the bias correction it would top the ranking of a table it says nothing about.
    [Fact]
    public async Task AColumnThatNamesEveryRow_DoesNotOutrankARealFeature()
    {
        var report = await new FeatureAnalyzer().AnalyzeAsync(
            BuildAllCategoricalAdapter(), new AnalysisOptions { TargetColumn = "Label" });

        var byName = report.CategoricalMutualInfo!.Features.ToDictionary(f => f.Name);
        Assert.True(byName["RowId"].Mi < byName["Weak"].Mi,
            $"RowId({byName["RowId"].Mi}) should rank below Weak({byName["Weak"].Mi})");
        Assert.True(byName["Noise"].Mi < 0.02, $"an unrelated column scored {byName["Noise"].Mi}");
    }

    [Fact]
    public async Task NumericTarget_GivesNoCategoricalScores()
    {
        var report = await new FeatureAnalyzer().AnalyzeAsync(
            BuildAdapter(), new AnalysisOptions { TargetColumn = "Y" });

        Assert.Null(report.CategoricalMutualInfo);
    }

    [Fact]
    public void Score_OfIdenticalColumns_IsTheEntropyOfTheColumn()
    {
        uint[] x = [.. Enumerable.Range(0, 400).Select(i => (uint)(i % 2))];
        var (mi, levels, rows) = CategoricalMutualInfoScorer.Score(x, x);

        // ln 2 minus the Miller-Madow term (1 * 1) / (2 * 400)
        Assert.Equal(Math.Log(2) - 1.0 / 800.0, mi, 9);
        Assert.Equal(2, levels);
        Assert.Equal(400, rows);
    }

    [Fact]
    public void Score_SkipsRowsWithoutALabel()
    {
        uint[] x = [0, 1, 0, 1, uint.MaxValue, 0, 1, 0, 1, 0];
        uint[] y = [0, 1, 0, 1, 0, uint.MaxValue, 1, 0, 1, 0];
        var (_, _, rows) = CategoricalMutualInfoScorer.Score(x, y);

        Assert.Equal(8, rows);
    }


    // A numeric target with categorical features: the native permutation importance reads numeric columns only, so these columns had no score.
    private static DataAdapter BuildNumericTargetWithCategories(int n = 600)
    {
        var rng = new Random(5);
        string[] plans = ["basic", "plus", "pro", "max"];
        var data = new List<Dictionary<string, string>>(n);
        for (int i = 0; i < n; i++)
        {
            var plan = rng.Next(4);
            var region = rng.Next(4);
            var weak = rng.Next(4);
            double y = plan * 10.0 + (weak == 0 ? 8.0 : 0.0) + rng.NextDouble();      // plan drives it; weak moves it a little
            data.Add(new Dictionary<string, string>
            {
                ["Plan"] = plans[plan],
                ["Weak"] = "w" + weak,
                ["Region"] = "r" + region,
                ["RowId"] = $"row-{i}",
                ["Spend"] = y.ToString("R"),
            });
        }
        return new DataAdapter(DataPipeline.FromData(data).ToDataFrame());
    }

    [Fact]
    public async Task NumericTarget_ScoresCategoricalFeaturesByTheShareOfVarianceTheyExplain()
    {
        var report = await new FeatureAnalyzer().AnalyzeAsync(
            BuildNumericTargetWithCategories(), new AnalysisOptions { TargetColumn = "Spend" });

        Assert.NotNull(report.CategoricalCorrelationRatio);
        var byName = report.CategoricalCorrelationRatio!.Features.ToDictionary(f => f.Name);
        Assert.DoesNotContain("Spend", byName.Keys);
        Assert.Equal("Plan", report.CategoricalCorrelationRatio.Features[0].Name);
        Assert.InRange(byName["Plan"].Ratio, 0.85, 0.95);   // about 125 of the 137 units of variance; the rest is Weak and noise
        Assert.InRange(byName["Weak"].Ratio, 0.02, 0.2);
        Assert.True(byName["Region"].Ratio < 0.01, $"an unrelated column scored {byName["Region"].Ratio}");
        Assert.Equal(4, byName["Plan"].Levels);
        Assert.Equal(600, byName["Plan"].Rows);
    }

    // A column with a different value on every row would explain all of the variance without the adjustment.
    [Fact]
    public async Task NumericTarget_AColumnThatNamesEveryRow_ExplainsNothing()
    {
        var report = await new FeatureAnalyzer().AnalyzeAsync(
            BuildNumericTargetWithCategories(), new AnalysisOptions { TargetColumn = "Spend" });

        Assert.Equal(0.0, report.CategoricalCorrelationRatio!.Features.First(f => f.Name == "RowId").Ratio);
    }

    [Fact]
    public async Task CategoricalTarget_GivesNoCorrelationRatio()
    {
        var report = await new FeatureAnalyzer().AnalyzeAsync(
            BuildAllCategoricalAdapter(), new AnalysisOptions { TargetColumn = "Label" });

        Assert.Null(report.CategoricalCorrelationRatio);
    }

    [Fact]
    public void CorrelationRatio_OfLevelsThatSplitTheTargetExactly_IsOne_AndSkipsRowsWithoutAValue()
    {
        uint[] levels = [.. Enumerable.Range(0, 400).Select(i => (uint)(i % 2)), uint.MaxValue];
        double[] target = [.. Enumerable.Range(0, 400).Select(i => i % 2 == 0 ? 1.0 : 5.0), 3.0];
        var (ratio, count, rows) = CategoricalCorrelationRatioScorer.Score(levels, target);

        Assert.Equal(1.0, ratio, 9);
        Assert.Equal(2, count);
        Assert.Equal(400, rows);                                   // the row without a level is left out

        var (_, _, rowsWithNaN) = CategoricalCorrelationRatioScorer.Score([0u, 1u, 0u, 1u, 0u], [1.0, double.NaN, 2.0, 3.0, 1.5]);
        Assert.Equal(4, rowsWithNaN);
    }
}
