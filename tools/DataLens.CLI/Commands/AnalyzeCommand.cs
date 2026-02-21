using System.CommandLine;
using DataLens.CLI.Presenters;

namespace DataLens.CLI.Commands;

internal static class AnalyzeCommand
{
    private static readonly Dictionary<string, Action<AnalysisOptions>> AnalyzerMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["profile"] = o => o.IncludeProfiling = true,
        ["descriptive"] = o => o.IncludeDescriptive = true,
        ["correlation"] = o => o.IncludeCorrelation = true,
        ["regression"] = o => o.IncludeRegression = true,
        ["distribution"] = o => o.IncludeDistribution = true,
        ["clustering"] = o => o.IncludeClustering = true,
        ["outlier"] = o => o.IncludeOutliers = true,
        ["feature"] = o => o.IncludeFeatures = true,
        ["pca"] = o => o.IncludePca = true,
    };

    public static Command Create()
    {
        var fileArg = CommonOptions.FileArgument();
        var formatOption = CommonOptions.FormatOption();

        var targetOption = new Option<string?>("--target", "-t")
        {
            Description = "Target column for regression/feature importance"
        };

        var includeOption = new Option<string?>("--include", "-i")
        {
            Description = "Comma-separated analyzer names to include (e.g., correlation,distribution)"
        };

        var command = new Command("analyze", "Run full or selective analysis")
        {
            fileArg,
            formatOption,
            targetOption,
            includeOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileArg)!;
            var format = parseResult.GetValue(formatOption)!;
            var target = parseResult.GetValue(targetOption);
            var include = parseResult.GetValue(includeOption);

            var options = BuildOptions(target, include);
            var result = await DataLensEngine.Analyze(file.FullName, options);

            if (format == "json")
            {
                Console.WriteLine(result.ToJson());
            }
            else
            {
                AnalysisPresenter.RenderAll(result);
            }
        });

        return command;
    }

    private static AnalysisOptions BuildOptions(string? target, string? include)
    {
        var options = new AnalysisOptions { TargetColumn = target };

        if (!string.IsNullOrWhiteSpace(include))
        {
            // Disable all first, then enable selected
            options.IncludeProfiling = false;
            options.IncludeDescriptive = false;
            options.IncludeCorrelation = false;
            options.IncludeRegression = false;
            options.IncludeDistribution = false;
            options.IncludeClustering = false;
            options.IncludeOutliers = false;
            options.IncludeFeatures = false;
            options.IncludePca = false;

            var names = include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var name in names)
            {
                if (AnalyzerMap.TryGetValue(name, out var setter))
                    setter(options);
                else
                    Console.Error.WriteLine($"[warn] Unknown analyzer: {name}");
            }
        }

        return options;
    }
}
