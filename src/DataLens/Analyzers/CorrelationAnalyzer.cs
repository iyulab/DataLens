using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class CorrelationAnalyzer : IAnalyzer<CorrelationReport>
{
    public Task<CorrelationReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        var numericCols = adapter.NumericColumns;
        if (numericCols.Count < 2)
        {
            return Task.FromResult(new CorrelationReport
            {
                ColumnNames = numericCols.ToList()
            });
        }

        using var client = new InsightClient();
        var matrix = adapter.ToCleanMatrix();

        if (matrix.GetLength(0) < 3)
        {
            return Task.FromResult(new CorrelationReport
            {
                ColumnNames = numericCols.ToList()
            });
        }

        var result = client.Correlation(matrix);

        // 고상관 쌍 추출
        var highPairs = new List<CorrelationPair>();
        for (int i = 0; i < numericCols.Count; i++)
        {
            for (int j = i + 1; j < numericCols.Count; j++)
            {
                var val = result.Matrix[i, j];
                if (Math.Abs(val) > options.CorrelationThreshold)
                {
                    highPairs.Add(new CorrelationPair
                    {
                        Column1 = numericCols[i],
                        Column2 = numericCols[j],
                        Value = val
                    });
                }
            }
        }

        highPairs.Sort((a, b) => b.AbsValue.CompareTo(a.AbsValue));

        // 범주형 변수 연관 분석 (Cramér's V)
        var categoricalAssociations = new List<CategoricalAssociation>();
        var catCols = adapter.CategoricalColumns;

        if (catCols.Count >= 2)
        {
            for (int i = 0; i < catCols.Count; i++)
            {
                for (int j = i + 1; j < catCols.Count; j++)
                {
                    try
                    {
                        var table = BuildContingencyTable(adapter, catCols[i], catCols[j]);
                        if (table != null)
                        {
                            var cv = client.CramersV(table);
                            categoricalAssociations.Add(new CategoricalAssociation
                            {
                                Column1 = catCols[i],
                                Column2 = catCols[j],
                                CramersV = cv.V,
                                ChiSquared = cv.ChiSquared,
                                PValue = cv.PValue
                            });
                        }
                    }
                    catch
                    {
                        // Cramér's V 계산 실패 시 무시
                    }
                }
            }
        }

        return Task.FromResult(new CorrelationReport
        {
            ColumnNames = numericCols.ToList(),
            Matrix = result.Matrix,
            HighCorrelationPairs = highPairs,
            CategoricalAssociations = categoricalAssociations
        });
    }

    private static double[,]? BuildContingencyTable(DataAdapter adapter, string col1, string col2)
    {
        var rows = adapter.DataFrame.Rows;
        var vals1 = new List<string>();
        var vals2 = new List<string>();

        foreach (var row in rows)
        {
            row.TryGetValue(col1, out var v1);
            row.TryGetValue(col2, out var v2);
            if (!string.IsNullOrWhiteSpace(v1) && !string.IsNullOrWhiteSpace(v2))
            {
                vals1.Add(v1);
                vals2.Add(v2);
            }
        }

        if (vals1.Count == 0) return null;

        var unique1 = vals1.Distinct().OrderBy(v => v).ToList();
        var unique2 = vals2.Distinct().OrderBy(v => v).ToList();

        if (unique1.Count < 2 || unique2.Count < 2) return null;

        var map1 = unique1.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);
        var map2 = unique2.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);

        var table = new double[unique1.Count, unique2.Count];
        for (int i = 0; i < vals1.Count; i++)
            table[map1[vals1[i]], map2[vals2[i]]]++;

        return table;
    }
}
