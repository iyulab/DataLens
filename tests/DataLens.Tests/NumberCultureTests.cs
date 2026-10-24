using System.Globalization;
using DataLens.Adapters;
using FilePrepper.Pipeline;

namespace DataLens.Tests;

/// <summary>
/// A data file's numbers are read in the invariant culture: on a comma-decimal machine (de-DE) the cell <c>1.5</c> was
/// read as 15, so every statistic over it was off by a power of ten.
/// </summary>
public class NumberCultureTests
{
    [Fact]
    public void A_comma_decimal_machine_reads_the_files_numbers()
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var frame = DataPipeline.FromData(new List<Dictionary<string, string>>
            {
                new() { ["x"] = "1.5" }, new() { ["x"] = "2.5" },
            }).ToDataFrame();
            var adapter = new DataAdapter(frame);

            Assert.Contains("x", adapter.NumericColumns);
            Assert.Equal([1.5, 2.5], adapter.ToCleanArray("x"));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
