using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// The <c>%sha256b64file[&lt;path&gt;]sha256b64file%</c> and <c>%strippemfile[&lt;path&gt;]strippemfile%</c>
/// instructions of a test file line, carried out as <c>subsha256base64file</c> and
/// <c>substrippemfile</c> in upstream's <c>testutil.pm</c> carry them out at <c>curl-8_21_0</c>,
/// after <c>%include</c>.
/// </summary>
/// <remarks>
/// Names match case-insensitively and each is replaced from the leftmost match, repeatedly, until
/// none is left. The path has its <c>%XX</c> pairs decoded before the file is read. A file that
/// cannot be read counts as empty.
/// </remarks>
internal static class UpstreamTestFileContentInstructions
{
    private const string Sha256Opening = "%sha256b64file[";

    private const string Sha256Closing = "]sha256b64file%";

    private const string StripPemOpening = "%strippemfile[";

    private const string StripPemClosing = "]strippemfile%";

    // get_file_content's s/(^|-----END .*?-----[\r\n]?)(.*?)(-----BEGIN .*?-----|$)/$1$3/gs.
    private static readonly Regex OutsidePemBlocks = new(
        @"(^|-----END .*?-----[\r\n]?)(.*?)(-----BEGIN .*?-----|$)",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>Replaces every <c>%sha256b64file</c>, then every <c>%strippemfile</c>.</summary>
    /// <param name="line">The line, one character per byte.</param>
    /// <param name="readFile">Reads a file by path, or returns <see langword="null"/> when it cannot.</param>
    /// <returns>The line with every instruction replaced.</returns>
    public static string Replace(string line, Func<string, byte[]?> readFile)
    {
        string text = UpstreamTestInstructions.ReplaceEach(line, Sha256Opening, Sha256Closing, path => Convert.ToBase64String(SHA256.HashData(Read(readFile, path))));
        return UpstreamTestInstructions.ReplaceEach(text, StripPemOpening, StripPemClosing, path => StripOutsidePemBlocks(Encoding.Latin1.GetString(Read(readFile, path))));
    }

    /// <summary>
    /// Adds <c>%sha256b64file</c> and <c>%strippemfile</c> to the list, once each, when the line
    /// holds one, for an expansion given no way to read files.
    /// </summary>
    /// <param name="line">The expanded line.</param>
    /// <param name="unsupported">The names found so far.</param>
    public static void AddUnread(string line, List<string> unsupported)
    {
        foreach (string opening in new[] { Sha256Opening, StripPemOpening })
        {
            string name = opening.TrimEnd('[');
            if (line.Contains(opening, StringComparison.OrdinalIgnoreCase) && !unsupported.Contains(name))
            {
                unsupported.Add(name);
            }
        }
    }

    // get_file_content: everything outside the PEM blocks removed, CRLF turned into LF, then
    // one trailing line feed removed (chomp).
    private static string StripOutsidePemBlocks(string content)
    {
        string stripped = OutsidePemBlocks.Replace(content, "$1$3").Replace("\r\n", "\n", StringComparison.Ordinal);
        return stripped.EndsWith('\n') ? stripped[..^1] : stripped;
    }

    // The path is carried one character per byte; variables were inserted as UTF-8.
    private static byte[] Read(Func<string, byte[]?> readFile, string path) =>
        readFile(Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(UpstreamTestInstructions.DecodePercentPairs(path)))) ?? [];
}
