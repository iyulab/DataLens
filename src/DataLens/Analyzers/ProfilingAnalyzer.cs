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
                    Mean = col.DataType <= 1 ? col.Mean : null,    // numeric types
                    StdDev = col.DataType <= 1 ? col.StdDev : null,
                    Min = col.DataType <= 1 ? col.Min : null,
                    Max = col.DataType <= 1 ? col.Max : null
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

    private static string MapDataType(uint dataType) => dataType switch
    {
        0 => "integer",
        1 => "float",
        2 => "string",
        3 => "boolean",
        _ => "unknown"
    };
}
