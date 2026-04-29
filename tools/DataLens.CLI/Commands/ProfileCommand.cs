using System.CommandLine;
using DataLens.Adapters;
using DataLens.CLI.Presenters;

namespace DataLens.CLI.Commands;

internal static class ProfileCommand
{
    public static Command Create()
    {
        var fileArg = CommonOptions.FileArgument();
        var formatOption = CommonOptions.FormatOption();
        var encodingOption = CommonOptions.EncodingOption();

        var command = new Command("profile", "Quick dataset profiling")
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

            var loadOptions = new CsvLoadOptions { Encoding = encoding };
            var report = await DataLensEngine.Profile(file.FullName, loadOptions, cancellationToken);

            if (format == "json")
            {
                var result = new AnalysisResult { Profile = report };
                Console.WriteLine(result.ToJson(Section.Profile));
            }
            else
            {
                AnalysisPresenter.RenderProfile(report);
            }
        });

        return command;
    }
}
