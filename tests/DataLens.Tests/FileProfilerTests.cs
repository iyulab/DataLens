using System.Text;
using System.Text.Json;
using DataLens.Adapters;
using DataLens.Analyzers;

namespace DataLens.Tests;

/// <summary>
/// <see cref="DataLensEngine.Profile(string, CancellationToken)"/> hands a plain CSV to the engine by
/// path; the loader remains the answer for everything else. Both routes must give the same report.
/// </summary>
public class FileProfilerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("datalens-fileprofiler-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string text, Encoding? encoding = null)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text, encoding ?? new UTF8Encoding(false));
        return path;
    }

    private string WriteBytes(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>The report the loader route gives (explicit options skip the path route).</summary>
    private static async Task<string> ViaLoader(string path) =>
        JsonSerializer.Serialize(await DataLensEngine.Profile(path, new CsvLoadOptions()));

    private static async Task<string> ViaDefault(string path) =>
        JsonSerializer.Serialize(await DataLensEngine.Profile(path));

    public static TheoryData<string, string> PlainFiles => new()
    {
        { "mixed.csv", "id,score,name,flag,when\n1,3.5,alpha,true,2020-01-01\n2,,beta,false,2020-01-02\n3,-1e3,\"gamma, with comma\",true,2020-01-03\n4,NA,delta,false,\n" },
        { "crlf.csv", "a,b\r\n1,2\r\n3,4\r\n\r\n" },
        { "no-final-newline.csv", "a,b\n1,2\n3,4" },
        { "quoted-newline.csv", "a,b\n1,\"line one\nline two\"\n2,plain\n" },
        { "single-column.csv", "only\n1\n2\n3\n" },
        { "all-missing-column.csv", "a,b\n1,\n2,\n3,\n" },
    };

    [Theory]
    [MemberData(nameof(PlainFiles))]
    public async Task PlainCsv_IsProfiledByPath_AndMatchesTheLoaderRoute(string name, string text)
    {
        var path = Write(name, text);

        Assert.NotNull(FileProfiler.TryProfile(path));
        Assert.Equal(await ViaLoader(path), await ViaDefault(path));
    }

    [Fact]
    public async Task LargerNumericFile_MatchesTheLoaderRoute()
    {
        var random = new Random(7);
        var sb = new StringBuilder("a,b,c,label\n");
        for (var i = 0; i < 5000; i++)
            sb.Append(FormattableString.Invariant($"{random.NextDouble():F4},{random.Next(1000)},{random.NextDouble() * 100:F2},{(random.Next(2) == 0 ? "x" : "y")}\n"));
        var path = Write("large.csv", sb.ToString());

        Assert.NotNull(FileProfiler.TryProfile(path));
        Assert.Equal(await ViaLoader(path), await ViaDefault(path));
    }

    public static TheoryData<string, byte[]> NotPlain => new()
    {
        { "bom.csv", [0xEF, 0xBB, 0xBF, .. "a,b\n1,2\n"u8.ToArray()] },
        { "semicolon.csv", "a;b\n1;2\n3;4\n"u8.ToArray() },
        { "tab-separated.csv", "a\tb\n1\t2\n"u8.ToArray() },
        { "quoted-header.csv", "\"a\",\"b\"\n1,2\n"u8.ToArray() },
        { "repeated-name.csv", "a,a\n1,2\n"u8.ToArray() },
        { "padded-name.csv", "a, b\n1,2\n"u8.ToArray() },
        { "blank-name.csv", "a,,c\n1,2,3\n"u8.ToArray() },
        { "not-utf8.csv", [.. "name,v\n"u8.ToArray(), 0xB0, 0xA1, .. ",1\n"u8.ToArray()] },
        { "header-only.csv", "a,b"u8.ToArray() },
    };

    [Theory]
    [MemberData(nameof(NotPlain))]
    public async Task NotPlainCsv_LeavesTheFileToTheLoader(string name, byte[] bytes)
    {
        var path = WriteBytes(name, bytes);

        Assert.Null(FileProfiler.TryProfile(path));
        Assert.Equal(await ViaLoader(path), await ViaDefault(path));
    }

    [Fact]
    public void OtherExtension_IsNotProfiledByPath()
    {
        Assert.Null(FileProfiler.TryProfile(Write("data.tsv", "a,b\n1,2\n")));
    }

    [Fact]
    public async Task MissingFile_StillThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => DataLensEngine.Profile(Path.Combine(_dir, "missing.csv")));
    }

    [Fact]
    public async Task RaggedRows_FallBackToTheLoader()
    {
        // A row of the wrong width is an engine refusal; the loader still decides what the file means.
        var path = Write("ragged.csv", "a,b\n1,2\n3\n4,5,6\n");

        Assert.Null(FileProfiler.TryProfile(path));
        Assert.Equal(await ViaLoader(path), await ViaDefault(path));
    }
}
