using System.CommandLine;
using DataLens.CLI.Commands;

namespace DataLens.CLI.Tests;

public class CommandParsingTests
{
    private static string CreateTempCsv()
    {
        var path = Path.GetTempFileName();
        File.Move(path, path + ".csv");
        path += ".csv";
        File.WriteAllText(path, "A,B\n1,2\n3,4\n");
        return path;
    }

    [Fact]
    public void ProfileCommand_ParsesFileArgument()
    {
        var csv = CreateTempCsv();
        try
        {
            var command = ProfileCommand.Create();
            var root = new RootCommand { command };

            var result = root.Parse($"profile {csv}");

            Assert.Empty(result.Errors);
        }
        finally { File.Delete(csv); }
    }

    [Fact]
    public void ProfileCommand_ParsesFormatOption()
    {
        var csv = CreateTempCsv();
        try
        {
            var command = ProfileCommand.Create();
            var root = new RootCommand { command };

            var result = root.Parse($"profile {csv} --format json");

            Assert.Empty(result.Errors);
        }
        finally { File.Delete(csv); }
    }

    [Fact]
    public void AnalyzeCommand_ParsesAllOptions()
    {
        var csv = CreateTempCsv();
        try
        {
            var command = AnalyzeCommand.Create();
            var root = new RootCommand { command };

            var result = root.Parse($"analyze {csv} --target B --include correlation,distribution --format json");

            Assert.Empty(result.Errors);
        }
        finally { File.Delete(csv); }
    }

    [Fact]
    public void CorrelateCommand_ParsesFileArgument()
    {
        var csv = CreateTempCsv();
        try
        {
            var command = CorrelateCommand.Create();
            var root = new RootCommand { command };

            var result = root.Parse($"correlate {csv}");

            Assert.Empty(result.Errors);
        }
        finally { File.Delete(csv); }
    }

    [Fact]
    public void ImportanceCommand_RequiresTargetOption()
    {
        var csv = CreateTempCsv();
        try
        {
            var command = ImportanceCommand.Create();
            var root = new RootCommand { command };

            var result = root.Parse($"importance {csv}");

            Assert.NotEmpty(result.Errors);
        }
        finally { File.Delete(csv); }
    }

    [Fact]
    public void ImportanceCommand_ParsesWithTarget()
    {
        var csv = CreateTempCsv();
        try
        {
            var command = ImportanceCommand.Create();
            var root = new RootCommand { command };

            var result = root.Parse($"importance {csv} --target B");

            Assert.Empty(result.Errors);
        }
        finally { File.Delete(csv); }
    }

    [Fact]
    public void AnalyzeCommand_ParsesShortAlias()
    {
        var csv = CreateTempCsv();
        try
        {
            var command = AnalyzeCommand.Create();
            var root = new RootCommand { command };

            var result = root.Parse($"analyze {csv} -t B -i correlation -f json");

            Assert.Empty(result.Errors);
        }
        finally { File.Delete(csv); }
    }
}
