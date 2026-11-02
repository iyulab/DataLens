using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class FeatureAnalyzer : IAnalyzer<FeatureReport>
{
    public Task<FeatureReport> AnalyzeAsync(
        DataAdapter adapter,
        AnalysisOptions options,
        ICollection<AnalysisWarning>? warnings = null)
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
        catch (Exception ex)
        {
            warnings?.Add(AnalysisWarning.FromException("FeatureImportance", ex));
        }

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
                // 레이블이 없는 행(uint.MaxValue)은 레이블과 행렬에서 함께 뺀다 — 레이블만 빼면
                // 남은 레이블이 다른 행에 붙고, 행 수보다 짧은 타깃이 네이티브 호출에 넘어간다.
                var labelled = labels.Select(l => l != uint.MaxValue).ToArray();
                var cleanLabels = labels.Where(l => l != uint.MaxValue).ToArray();
                var labelledMatrix = KeepRows(matrix, labelled);

                if (cleanLabels.Length > 3)
                {
                    try
                    {
                        var anovaResult = client.AnovaSelect(labelledMatrix, cleanLabels, options.SignificanceLevel);
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
                    catch (Exception ex)
                    {
                        warnings?.Add(AnalysisWarning.FromException("ANOVA", ex));
                    }

                    try
                    {
                        var miResult = client.MutualInfo(labelledMatrix, cleanLabels);
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
                    catch (Exception ex)
                    {
                        warnings?.Add(AnalysisWarning.FromException("MutualInfo", ex));
                    }
                }
            }
            // 숫자 타겟 → PermutationImportance
            else if (numericCols.Contains(targetCol))
            {
                try
                {
                    // 타겟을 피처 행렬에서 제외한다. 제외하지 않으면 모델이 타겟으로 타겟을
                    // 예측(self-prediction)해 완벽 적합 → 실피처를 섞어도 점수 변화가 없어
                    // 모든 실피처의 permutation importance가 0이 된다. 피처-전용 행렬은
                    // 전 행을 유지하므로(중앙값 대체) targetArray 행 정합은 기존과 동일하다.
                    var featureCols = numericCols.Where(c => c != targetCol).ToList();
                    if (featureCols.Count >= 1)
                    {
                        // 타깃이 비어 있는 행은 행렬에서도 뺀다(타깃 배열과 행 정합).
                        var rawTarget = adapter.ToArray(targetCol);
                        var present = rawTarget.Select(v => !double.IsNaN(v)).ToArray();
                        var featureMatrix = KeepRows(adapter.ToImputedMatrix(featureCols.ToArray()), present);
                        var targetArray = rawTarget.Where(v => !double.IsNaN(v)).ToArray();
                        var permResult = client.PermutationImportance(featureMatrix, targetArray);
                        var permFeatures = new List<PermutationFeatureResult>();
                        foreach (var f in permResult.Features)
                        {
                            permFeatures.Add(new PermutationFeatureResult
                            {
                                Name = (int)f.Index < featureCols.Count ? featureCols[(int)f.Index] : $"col_{f.Index}",
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
                }
                catch (Exception ex)
                {
                    warnings?.Add(AnalysisWarning.FromException("PermutationImportance", ex));
                }
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

    /// <summary>`keep[i]` 인 행만 남긴 행렬.</summary>
    private static double[,] KeepRows(double[,] matrix, bool[] keep)
    {
        int nCols = matrix.GetLength(1);
        int kept = keep.Count(k => k);
        var result = new double[kept, nCols];
        int r = 0;
        for (int i = 0; i < keep.Length; i++)
        {
            if (!keep[i]) continue;
            for (int j = 0; j < nCols; j++)
                result[r, j] = matrix[i, j];
            r++;
        }
        return result;
    }
}
