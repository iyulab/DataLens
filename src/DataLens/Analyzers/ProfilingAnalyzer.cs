using DataLens.Adapters;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

public class ProfilingAnalyzer : IAnalyzer<ProfileReport>
{
    public Task<ProfileReport> AnalyzeAsync(
        DataAdapter adapter,
        AnalysisOptions options,
        ICollection<AnalysisWarning>? warnings = null)
    {
        try
        {
            using var client = new InsightClient();
            var csvString = adapter.ToCsvString();
            var profile = client.ProfileCsv(csvString);

            return Task.FromResult(BuildReport(profile, adapter.AllColumns, adapter.RowCount, adapter.ColumnCount));
        }
        catch (InsightException ex)
        {
            // Upstream (UInsight) profiling failure is non-fatal: record it as a warning and return
            // an empty column set so the pipeline continues — but never swallow it silently (a caller
            // must be able to tell "profiling failed" from "profiled zero columns"). A non-Insight
            // exception (a bug in this layer) is intentionally NOT caught: it propagates rather than
            // masquerading as an empty-but-successful profile — the F-class silent-failure this fixes.
            warnings?.Add(AnalysisWarning.FromInsightException("Profiling", ex));
            return Task.FromResult(new ProfileReport
            {
                RowCount = adapter.RowCount,
                ColumnCount = adapter.ColumnCount,
                Columns = []
            });
        }
    }

    /// <summary>The report for an engine profile whose columns are named by <paramref name="columnNames"/>
    /// (the engine's column summaries carry an index, not a name).</summary>
    internal static ProfileReport BuildReport(
        ProfileResult profile,
        IReadOnlyList<string> columnNames,
        int rowCount,
        int columnCount)
    {
        var columns = new List<ColumnProfile>();

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
                // null for a non-numeric column and for a statistic the
                // data does not define (the standard deviation of one value).
                Mean = col.Mean,
                StdDev = col.StdDev,
                Min = col.Min,
                Max = col.Max
            });
        }

        return new ProfileReport
        {
            RowCount = rowCount,
            ColumnCount = columnCount,
            Columns = columns
        };
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
