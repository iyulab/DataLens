using System.Text.RegularExpressions;
using Xunit;

namespace DataLens.Tests.Documentation;

/// <summary>
/// The README's "Analysis Modules" section covers every analysis the result carries. A section of <see cref="AnalysisResult"/> with no
/// module in the README reads as an analysis DataLens does not do - the distribution analysis had none, though it had its own switch,
/// report and tests.
/// </summary>
public class ReadmeAnalysisModulesContractTests
{
    private static string ModulesSection()
    {
        var readme = File.ReadAllText(Path.Combine(RepoSourceTree.RepoRoot, "README.md"));
        return ModulesOf(readme);
    }

    internal static string ModulesOf(string readme) =>
        Regex.Split(readme, @"^## Analysis Modules\s*$", RegexOptions.Multiline).ElementAtOrDefault(1) is { } after
            ? Regex.Split(after, @"^## ", RegexOptions.Multiline)[0]
            : "";

    /// <summary>The result sections that no module in <paramref name="modules"/> reads (<c>.Section</c>) or switches on (<c>IncludeSection</c>).</summary>
    internal static List<string> Unmentioned(string modules, IEnumerable<string> sections) =>
        [.. sections.Where(s => !Regex.IsMatch(modules, @"(\.|Include)" + Regex.Escape(s) + @"\b"))];

    private static IEnumerable<string> ResultSections() =>
        typeof(AnalysisResult).GetProperties()
            .Where(p => p.Name != nameof(AnalysisResult.Warnings) && p.PropertyType.IsClass && p.PropertyType.Name.EndsWith("Report", StringComparison.Ordinal))
            .Select(p => p.Name);

    [Fact]
    public void EveryAnalysisTheResultCarriesHasAModuleInTheReadme()
    {
        var sections = ResultSections().ToList();
        Assert.Contains("Distribution", sections);
        Assert.NotEmpty(ModulesSection());

        Assert.Empty(Unmentioned(ModulesSection(), sections));
    }

    [Fact]
    public void TheScan_SeesASectionTheModulesLeaveOut()
    {
        const string modules = "var p = analysis.Profile;\nvar o = new AnalysisOptions { IncludeChangepoints = true };";

        Assert.Equal(["Distribution"], Unmentioned(modules, ["Profile", "Changepoints", "Distribution"]));
        Assert.Equal("", ModulesOf("# Title\n\nno modules here"));
    }
}
