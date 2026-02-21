using System.CommandLine;
using DataLens.CLI.Presenters;

namespace DataLens.CLI.Commands;

internal static class ProfileCommand
{
    public static Command Create()
    {
        var fileArg = CommonOptions.FileArgument();
        var formatOption = CommonOptions.FormatOption();

        var command = new Command("profile", "Quick dataset profiling")
        {
            fileArg,
            formatOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileArg)!;
            var format = parseResult.GetValue(formatOption)!;

            var report = await DataLensEngine.Profile(file.FullName);

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
