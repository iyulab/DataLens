using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class RegressionAnalyzer : IAnalyzer<RegressionReport>
{
    public Task<RegressionReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        var targetCol = options.TargetColumn;
        var numericCols = adapter.NumericColumns;

        if (string.IsNullOrEmpty(targetCol) || !numericCols.Contains(targetCol))
        {
            // 타겟 미지정 시 첫 번째 숫자 컬럼을 타겟으로 사용
            if (numericCols.Count < 2)
                return Task.FromResult(new RegressionReport { TargetColumn = targetCol });

            targetCol = numericCols[0];
        }

        var featureCols = numericCols.Where(c => c != targetCol).ToList();
        if (featureCols.Count == 0)
            return Task.FromResult(new RegressionReport { TargetColumn = targetCol });

        using var client = new InsightClient();
        var targetArray = adapter.ToCleanArray(targetCol);
        var entries = new List<RegressionEntry>();

        foreach (var feature in featureCols)
        {
            try
            {
                var featureArray = adapter.ToCleanArray(feature);
                // 두 배열의 길이를 맞춤 (NaN 위치가 다를 수 있으므로)
                var (x, y) = AlignArrays(adapter, feature, targetCol);
                if (x.Length < 3) continue;

                var result = client.Regression(x, y);
                entries.Add(new RegressionEntry
                {
                    FeatureColumn = feature,
                    Slope = result.Slope,
                    Intercept = result.Intercept,
                    RSquared = result.RSquared,
                    AdjRSquared = result.AdjRSquared,
                    FPValue = result.FPValue
                });
            }
            catch
            {
                // 회귀 실패 시 무시
            }
        }

        return Task.FromResult(new RegressionReport
        {
            TargetColumn = targetCol,
            Entries = entries
        });
    }

    private static (double[] x, double[] y) AlignArrays(DataAdapter adapter, string xCol, string yCol)
    {
        var rows = adapter.DataFrame.Rows;
        var xList = new List<double>();
        var yList = new List<double>();

        foreach (var row in rows)
        {
            row.TryGetValue(xCol, out var xVal);
            row.TryGetValue(yCol, out var yVal);

            if (double.TryParse(xVal, out var x) && double.TryParse(yVal, out var y))
            {
                xList.Add(x);
                yList.Add(y);
            }
        }

        return (xList.ToArray(), yList.ToArray());
    }
}
