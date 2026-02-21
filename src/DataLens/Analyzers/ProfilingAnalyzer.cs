using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class ProfilingAnalyzer : IAnalyzer<ProfileReport>
{
    public Task<ProfileReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        try
        {
            using var client = new InsightClient();
            var csvString = adapter.ToCsvString();
            var profile = client.ProfileCsv(csvString);

            var columns = new List<ColumnProfile>();
            var columnNames = adapter.AllColumns;

            for (int i = 0; i < profile.Columns.Length && i < columnNames.Count; i++)
            {
                var col = profile.Columns[i];
                var totalCount = (long)col.ValidCount + (long)col.NullCount;
                var isNumeric = col.DataType == InsightDataType.Numeric;
                columns.Add(new ColumnProfile
                {
                    Name = columnNames[i],
                    Index = i,
                    DataType = MapDataType(col.DataType),
                    ValidCount = (long)col.ValidCount,
                    NullCount = (long)col.NullCount,
                    NullPercentage = totalCount > 0
                        ? (double)col.NullCount / totalCount * 100.0
                        : 0,
                    Mean = isNumeric ? col.Mean : null,
                    StdDev = isNumeric ? col.StdDev : null,
                    Min = isNumeric ? col.Min : null,
                    Max = isNumeric ? col.Max : null
                });
            }

            return Task.FromResult(new ProfileReport
            {
                RowCount = adapter.RowCount,
                ColumnCount = adapter.ColumnCount,
                Columns = columns
            });
        }
        catch
        {
            return Task.FromResult(new ProfileReport
            {
                RowCount = adapter.RowCount,
                ColumnCount = adapter.ColumnCount,
                Columns = []
            });
        }
    }

    private static string MapDataType(InsightDataType dataType) => dataType switch
    {
        InsightDataType.Numeric => "numeric",
        InsightDataType.Boolean => "boolean",
        InsightDataType.Categorical => "categorical",
        InsightDataType.Text => "text",
        _ => "unknown"
    };
}
