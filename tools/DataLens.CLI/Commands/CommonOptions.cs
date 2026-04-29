using System.CommandLine;

namespace DataLens.CLI.Commands;

internal static class CommonOptions
{
    public static Option<string> FormatOption()
    {
        return new Option<string>("--format", "-f")
        {
            Description = "Output format: table or json",
            DefaultValueFactory = _ => "table"
        };
    }

    public static Option<string> EncodingOption()
    {
        return new Option<string>("--encoding", "-e")
        {
            Description = "CSV/TSV input encoding (e.g., auto, utf-8, utf-8-bom, cp949, euc-kr). Ignored for JSON inputs.",
            DefaultValueFactory = _ => "auto"
        };
    }

    public static Argument<FileInfo> FileArgument()
    {
        var arg = new Argument<FileInfo>("file")
        {
            Description = "Path to the data file (CSV, TSV, JSON)"
        };
        arg.Validators.Add(result =>
        {
            var file = result.GetValue(arg);
            if (file is not null && !file.Exists)
                result.AddError($"File not found: {file.FullName}");
        });
        return arg;
    }
}
