using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class FeatureAnalyzer : IAnalyzer<FeatureReport>
{
    public Task<FeatureReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        var numericCols = adapter.NumericColumns;
        if (numericCols.Count < 2)
            return Task.FromResult(new FeatureReport());

        using var client = new InsightClient();
        // 피처 중요도는 모든 행이 필요 → 결측값 중앙값 대체
        var matrix = adapter.ToImputedMatrix();
        if (matrix.GetLength(0) < 3)
            return Task.FromResult(new FeatureReport());

        // 기본 FeatureImportance (타겟 불필요)
        FeatureImportanceSummary? importance = null;
        try
        {
            var fiResult = client.FeatureImportance(matrix);
            var scores = new List<FeatureScore>();
            for (int i = 0; i < fiResult.Scores.Length && i < numericCols.Count; i++)
            {
                scores.Add(new FeatureScore
                {
                    Name = numericCols[i],
                    Index = i,
                    Score = fiResult.Scores[i]
                });
            }
            scores.Sort((a, b) => b.Score.CompareTo(a.Score));

            importance = new FeatureImportanceSummary
            {
                Scores = scores,
                ConditionNumber = fiResult.ConditionNumber,
                LowVarianceCount = fiResult.NLowVariance,
                HighCorrPairsCount = fiResult.NHighCorrPairs
            };
        }
        catch { }

        // 타겟이 지정된 경우 추가 분석
        AnovaSummary? anova = null;
        MutualInfoSummary? mutualInfo = null;
        PermutationSummary? permutation = null;

        var targetCol = options.TargetColumn;
        if (!string.IsNullOrEmpty(targetCol))
        {
            // 범주형 타겟 → ANOVA + MutualInfo
            if (adapter.CategoricalColumns.Contains(targetCol))
            {
                var labels = adapter.ToLabels(targetCol);
                // clean labels (uint.MaxValue 제거)
                var cleanLabels = labels.Where(l => l != uint.MaxValue).ToArray();

                if (cleanLabels.Length > 3)
                {
                    try
                    {
                        var anovaResult = client.AnovaSelect(matrix, cleanLabels, options.SignificanceLevel);
                        var anovaFeatures = new List<AnovaFeatureResult>();
                        foreach (var f in anovaResult.Features)
                        {
                            anovaFeatures.Add(new AnovaFeatureResult
                            {
                                Name = (int)f.Index < numericCols.Count ? numericCols[(int)f.Index] : $"col_{f.Index}",
                                Index = (int)f.Index,
                                FStatistic = f.FStatistic,
                                PValue = f.PValue
                            });
                        }
                        anova = new AnovaSummary
                        {
                            Features = anovaFeatures,
                            SelectedCount = anovaResult.NSelected
                        };
                    }
                    catch { }

                    try
                    {
                        var miResult = client.MutualInfo(matrix, cleanLabels);
                        var miFeatures = new List<MutualInfoFeatureResult>();
                        foreach (var f in miResult.Features)
                        {
                            miFeatures.Add(new MutualInfoFeatureResult
                            {
                                Name = (int)f.Index < numericCols.Count ? numericCols[(int)f.Index] : $"col_{f.Index}",
                                Index = (int)f.Index,
                                Mi = f.Mi
                            });
                        }
                        mutualInfo = new MutualInfoSummary { Features = miFeatures };
                    }
                    catch { }
                }
            }
            // 숫자 타겟 → PermutationImportance
            else if (numericCols.Contains(targetCol))
            {
                try
                {
                    var targetArray = adapter.ToCleanArray(targetCol);
                    var permResult = client.PermutationImportance(matrix, targetArray);
                    var permFeatures = new List<PermutationFeatureResult>();
                    foreach (var f in permResult.Features)
                    {
                        permFeatures.Add(new PermutationFeatureResult
                        {
                            Name = (int)f.Index < numericCols.Count ? numericCols[(int)f.Index] : $"col_{f.Index}",
                            Index = (int)f.Index,
                            Importance = f.Importance,
                            StdDev = f.StdDev
                        });
                    }
                    permutation = new PermutationSummary
                    {
                        BaselineScore = permResult.BaselineScore,
                        Features = permFeatures
                    };
                }
                catch { }
            }
        }

        return Task.FromResult(new FeatureReport
        {
            TargetColumn = targetCol,
            Importance = importance,
            Anova = anova,
            MutualInfo = mutualInfo,
            Permutation = permutation
        });
    }
}
