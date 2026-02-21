using System.CommandLine;
using DataLens.CLI.Presenters;

namespace DataLens.CLI.Commands;

internal static class CorrelateCommand
{
    public static Command Create()
    {
        var fileArg = CommonOptions.FileArgument();
        var formatOption = CommonOptions.FormatOption();

        var command = new Command("correlate", "Run correlation analysis")
        {
            fileArg,
            formatOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileArg)!;
            var format = parseResult.GetValue(formatOption)!;

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

            var result = await DataLensEngine.Analyze(file.FullName, options);

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
