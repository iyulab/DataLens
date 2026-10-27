using System.Text;
using DataLens.Models;
using UInsight;

namespace DataLens.Analyzers;

/// <summary>
/// Profiles a plain CSV file by handing the path to the engine, which reads it itself — the file is
/// neither loaded into a <c>DataFrame</c> nor written back out as one CSV string. The loader stays
/// the authority for everything it knows (encodings, delimiters, other formats, quoting in the header):
/// whenever the file is not plainly a comma-separated UTF-8 text this answers <c>null</c>, and so does
/// an engine refusal, and the caller profiles through the loader as before.
/// </summary>
internal static class FileProfiler
{
    private const int HeaderLimitBytes = 1 << 20;

    // A header carrying one of these is read by the loader with its own delimiter detection.
    private static readonly char[] OtherDelimiters = [';', '\t', '|', '"'];

    public static ProfileReport? TryProfile(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".csv", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var header = ReadPlainHeader(filePath);
            if (header is null || !IsUtf8(filePath))
                return null;

            using var client = new InsightClient();
            var profile = client.ProfileCsvFile(filePath);
            if (profile.Columns.Length != header.Count)
                return null;

            return ProfilingAnalyzer.BuildReport(profile, header, checked((int)profile.RowCount), header.Count);
        }
        catch (InsightException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The header's column names, or <c>null</c> when it is not a plain comma-separated line
    /// (byte-order mark, quotes, another delimiter, blank or padded or repeated names).</summary>
    private static List<string>? ReadPlainHeader(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var buffer = new byte[(int)Math.Min(HeaderLimitBytes, stream.Length)];
        var read = stream.Read(buffer, 0, buffer.Length);

        if (read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return null;

        var end = Array.IndexOf(buffer, (byte)'\n', 0, read);
        if (end < 0)
            return null;

        var line = Encoding.UTF8.GetString(buffer, 0, end).TrimEnd('\r');
        if (line.Length == 0 || line.IndexOfAny(OtherDelimiters) >= 0)
            return null;

        var names = line.Split(',').ToList();
        if (names.Any(n => n.Length == 0 || n != n.Trim()) || names.Distinct().Count() != names.Count)
            return null;

        return names;
    }

    /// <summary>Whether the whole file is valid UTF-8 — the engine reads bytes as UTF-8, while the
    /// loader would have detected and converted another encoding.</summary>
    private static bool IsUtf8(string filePath)
    {
        var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
        var buffer = new byte[1 << 20];
        try
        {
            using var stream = File.OpenRead(filePath);
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                decoder.GetCharCount(buffer, 0, read, flush: false);
            decoder.GetCharCount(buffer, 0, 0, flush: true);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
