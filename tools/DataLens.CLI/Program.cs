using System.CommandLine;
using System.Text;
using DataLens.CLI.Commands;

// The OS console codepage (e.g. CP949 on a default Korean Windows install) does not
// match the UTF-8 bytes System.CommandLine/Spectre.Console emit for localized text and
// box-drawing table borders, garbling all output. Force UTF-8 before anything writes.
Console.OutputEncoding = Encoding.UTF8;

var rootCommand = new RootCommand("DataLens - Exploratory Data Analysis CLI");

rootCommand.Subcommands.Add(ProfileCommand.Create());
rootCommand.Subcommands.Add(AnalyzeCommand.Create());
rootCommand.Subcommands.Add(CorrelateCommand.Create());
rootCommand.Subcommands.Add(ImportanceCommand.Create());

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
