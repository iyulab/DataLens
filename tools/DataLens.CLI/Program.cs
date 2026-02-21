using System.CommandLine;
using DataLens.CLI.Commands;

var rootCommand = new RootCommand("DataLens - Exploratory Data Analysis CLI");

rootCommand.Subcommands.Add(ProfileCommand.Create());
rootCommand.Subcommands.Add(AnalyzeCommand.Create());
rootCommand.Subcommands.Add(CorrelateCommand.Create());
rootCommand.Subcommands.Add(ImportanceCommand.Create());

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
