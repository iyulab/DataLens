using System.CommandLine;
using DataLens.Adapters;
using DataLens.CLI.Presenters;

namespace DataLens.CLI.Commands;

internal static class CorrelateCommand
{
    public static Command Create()
    {
        var fileArg = CommonOptions.FileArgument();
        var formatOption = CommonOptions.FormatOption();
        var encodingOption = CommonOptions.EncodingOption();

        var command = new Command("correlate", "Run correlation analysis")
        {
            fileArg,
            formatOption,
            encodingOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileArg)!;
            var format = parseResult.GetValue(formatOption)!;
            var encoding = parseResult.GetValue(encodingOption)!;

            var options = new AnalysisOptions
            {
                IncludeProfiling = false,
                IncludeDescriptive = false,
                IncludeCorrelation = true,
                IncludeRegression = false,
                IncludeDistribution = false,
                IncludeClustering = false,
                IncludeOutliers = false,
                IncludeFeatures = false,
                IncludePca = false
            };

            var loadOptions = new CsvLoadOptions { Encoding = encoding };
            var result = await DataLensEngine.Analyze(file.FullName, options, loadOptions, cancellationToken);

            if (format == "json")
            {
                Console.WriteLine(result.ToJson(Section.Correlation));
            }
            else
            {
                if (result.Correlation is not null)
                    AnalysisPresenter.RenderCorrelation(result.Correlation);
                else
                    Console.Error.WriteLine("No correlation results available.");
            }
        });

        return command;
    }
}
