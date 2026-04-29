using System.CommandLine;
using DataLens.Adapters;
using DataLens.CLI.Presenters;

namespace DataLens.CLI.Commands;

internal static class ImportanceCommand
{
    public static Command Create()
    {
        var fileArg = CommonOptions.FileArgument();
        var formatOption = CommonOptions.FormatOption();
        var encodingOption = CommonOptions.EncodingOption();

        var targetOption = new Option<string>("--target", "-t")
        {
            Description = "Target column (required)",
            Required = true
        };

        var command = new Command("importance", "Compute feature importance for a target column")
        {
            fileArg,
            targetOption,
            formatOption,
            encodingOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileArg)!;
            var target = parseResult.GetValue(targetOption)!;
            var format = parseResult.GetValue(formatOption)!;
            var encoding = parseResult.GetValue(encodingOption)!;

            var loadOptions = new CsvLoadOptions { Encoding = encoding };
            var report = await DataLensEngine.FeatureImportance(file.FullName, target, loadOptions, cancellationToken);

            if (format == "json")
            {
                var result = new AnalysisResult { Features = report };
                Console.WriteLine(result.ToJson(Section.Features));
            }
            else
            {
                AnalysisPresenter.RenderFeatures(report);
            }
        });

        return command;
    }
}
