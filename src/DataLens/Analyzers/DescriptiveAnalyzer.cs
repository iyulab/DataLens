using DataLens.Adapters;
using DataLens.Models;
using FilePrepper.Pipeline;

namespace DataLens.Analyzers;

public class DescriptiveAnalyzer : IAnalyzer<DescriptiveReport>
{
    public Task<DescriptiveReport> AnalyzeAsync(DataAdapter adapter, AnalysisOptions options)
    {
        var columns = new List<ColumnDescriptive>();
        var pipeline = DataPipeline.FromData(adapter.DataFrame.Rows);

        foreach (var col in adapter.NumericColumns)
        {
            var stats = pipeline.GetStatistics(col);
            var values = adapter.ToCleanArray(col);

            double? skewness = null;
            double? kurtosis = null;

            if (values.Length > 2)
            {
                skewness = ComputeSkewness(values, stats.Mean, stats.Std);
                kurtosis = ComputeKurtosis(values, stats.Mean, stats.Std);
            }

            columns.Add(new ColumnDescriptive
            {
                Name = col,
                Count = stats.Count,
                NullCount = stats.NullCount,
                Mean = stats.Mean,
                Median = stats.Median,
                Std = stats.Std,
                Variance = stats.Variance,
                Min = stats.Min,
                Max = stats.Max,
                Q1 = stats.Q1,
                Q3 = stats.Q3,
                Iqr = stats.IQR,
                Skewness = skewness,
                Kurtosis = kurtosis
            });
        }

        return Task.FromResult(new DescriptiveReport { Columns = columns });
    }

    private static double ComputeSkewness(double[] values, double mean, double std)
    {
        if (std == 0) return 0;
        int n = values.Length;
        double sum = values.Sum(v => Math.Pow((v - mean) / std, 3));
        return (double)n / ((n - 1) * (n - 2)) * sum;
    }

    private static double ComputeKurtosis(double[] values, double mean, double std)
    {
        if (std == 0) return 0;
        int n = values.Length;
        double sum = values.Sum(v => Math.Pow((v - mean) / std, 4));
        double excess = ((double)n * (n + 1)) / ((n - 1) * (n - 2) * (n - 3)) * sum
                       - 3.0 * (n - 1) * (n - 1) / ((n - 2) * (n - 3));
        return excess;
    }
}
