using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Emulates upstream's <c>tests/libtest/test613.pl</c> (curl 8.21.0), which the FTP, SFTP and
/// <c>file://</c> directory-listing cases run as <c>%PERL %SRCDIR/libtest/test613.pl</c> in a
/// precheck to make a folder to list and in a postcheck to remove it and make the listing
/// platform-independent, without running Perl, which the harness neither has nor needs.
/// </summary>
/// <remarks>
/// <para><c>prepare DIR</c> makes DIR holding <c>asubdir</c>, <c>plainfile.txt</c> and
/// <c>emptyfile.txt</c> (both last written 2000-01-01 12:00 UTC) and the read-only
/// <c>rofile.txt</c> (2000-12-31 12:00 UTC). When DIR cannot be made it prints Perl's
/// <c>$!</c> text and exits 1. The script's <c>chmod 0777</c> and <c>0666</c> are left to the
/// default permissions; <c>0444</c> is the read-only attribute, which .NET maps to clearing
/// the write bits off Windows.</para>
/// <para><c>postprocess DIR [LOG [MTIME]]</c> removes those entries and then DIR, failing with
/// Perl's <c>die "$!"</c> exit code (2 missing, 39 not empty) when DIR will not go. With MTIME it
/// exits 1 unless LOG was last written at MTIME seconds after the epoch. Otherwise, when LOG is
/// not empty, it rewrites each listing line into the canonical form: a file line keeps its
/// type, user permissions, link count, size, date and name, a folder line keeps only its name,
/// <c>.</c> and <c>..</c> are dropped, and a line that does not match is passed through - or,
/// as in Perl, whose failed match keeps the last match's groups, is rewritten from the last line
/// that did match. The lines are then sorted by what follows their 57th character, as bytes.</para>
/// <para>Fewer than two arguments print the usage and exit 1, and so does an unknown verb, with
/// <c>Unsupported command VERB</c>.</para>
/// </remarks>
internal static class UpstreamTest613Script
{
    private const int NoSuchFile = 2;

    private const int NotEmpty = 39;

    private const int SortColumn = 57;

    private const string ScriptName = "test613.pl";

    private static readonly UpstreamPerlOneLinerResult Passed = new(0, "");

    private static readonly DateTime JanuaryFirst2000Noon = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime December31st2000Noon = new(2000, 12, 31, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Entries = ["rofile.txt", "emptyfile.txt", "plainfile.txt"];

    // Interpreted, not source-generated, so no generated code counts against the coverage gate.
    private static readonly Regex ListingLine = new(
        @"^(.)(..).(..).(..).\s*(\S+)\s+\S+\s+\S+\s+(\S+)\s+(\S+\s+\S+\s+\S+)\s+(.*)$", RegexOptions.CultureInvariant);

    private static readonly Regex LeadingInteger = new(@"^\s*[+-]?\d+", RegexOptions.CultureInvariant);

    /// <summary>Runs a <c>%PERL</c> line when its program is <c>test613.pl</c>.</summary>
    /// <param name="arguments">The expanded line after the Perl program's name, starting with the script's path.</param>
    /// <returns>What the script did, or <see langword="null"/> when the line runs another program.</returns>
    public static UpstreamPerlOneLinerResult? Run(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string[] words = arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || Path.GetFileName(words[0]) != ScriptName)
        {
            return null;
        }

        return words.Length <= 2 ? new(1, $"Usage: {words[0]} prepare|postprocess directory [logfile]\n")
            : words[1] == "prepare" ? Prepare(words[2])
            : words[1] == "postprocess" ? Postprocess(words[2..])
            : new(1, $"Unsupported command {words[1]}\n");
    }

    private static UpstreamPerlOneLinerResult Prepare(string folder)
    {
        string? error = Path.Exists(folder) ? "File exists"
            : !Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(folder))) ? "No such file or directory"
            : null;
        if (error is not null)
        {
            return new(1, error + "\n");
        }

        Directory.CreateDirectory(Path.Combine(folder, "asubdir"));
        WriteFile(Path.Combine(folder, "plainfile.txt"), "Test file to support curl test suite\n", JanuaryFirst2000Noon);
        WriteFile(Path.Combine(folder, "emptyfile.txt"), "", JanuaryFirst2000Noon);
        string readOnly = Path.Combine(folder, "rofile.txt");
        WriteFile(readOnly, "Read-only test file to support curl test suite\n", December31st2000Noon);
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        return Passed;
    }

    private static void WriteFile(string path, string content, DateTime lastWritten)
    {
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(content));
        File.SetLastWriteTimeUtc(path, lastWritten);
    }

    private static UpstreamPerlOneLinerResult Postprocess(string[] arguments)
    {
        int failure = RemoveFolder(arguments[0]);
        if (failure != 0)
        {
            return new(failure, "");
        }

        if (arguments.Length >= 3)
        {
            return new(LastWrittenSeconds(arguments[1]) == PerlInteger(arguments[2]) ? 0 : 1, "");
        }

        if (arguments.Length == 2 && new FileInfo(arguments[1]) is { Exists: true, Length: > 0 } log)
        {
            Canonicalize(log.FullName);
        }

        return Passed;
    }

    // Each unlink and the inner rmdir fail silently in Perl; only the last rmdir dies.
    private static int RemoveFolder(string folder)
    {
        foreach (string entry in Entries.Select(name => Path.Combine(folder, name)).Where(File.Exists))
        {
            File.SetAttributes(entry, FileAttributes.Normal);
            File.Delete(entry);
        }

        string subfolder = Path.Combine(folder, "asubdir");
        if (Directory.Exists(subfolder) && !Directory.EnumerateFileSystemEntries(subfolder).Any())
        {
            Directory.Delete(subfolder);
        }

        return !Directory.Exists(folder) ? NoSuchFile
            : Directory.EnumerateFileSystemEntries(folder).Any() ? NotEmpty
            : Done(() => Directory.Delete(folder));
    }

    // Perl's stat of a missing file gives undef, which compares as 0.
    private static long LastWrittenSeconds(string path) =>
        File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds() : 0;

    // Perl's int() and %d read a string's leading integer and treat anything else as 0.
    private static long PerlInteger(string text) =>
        LeadingInteger.Match(text) is { Success: true } number ? long.Parse(number.Value, NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : 0;

    private static void Canonicalize(string path)
    {
        List<string> lines = [];
        Match? last = null;
        foreach (string line in SplitKeepingLineFeeds(File.ReadAllText(path, Encoding.Latin1)))
        {
            last = ListingLine.Match(line) is { Success: true } match ? match : last;
            if (CanonicalLine(last, line) is { } canonical)
            {
                lines.Add(canonical);
            }
        }

        string sorted = string.Concat(lines.OrderBy(line => line.Length > SortColumn ? line[SortColumn..] : "", StringComparer.Ordinal));
        File.WriteAllText(path, sorted, Encoding.Latin1);
    }

    private static IEnumerable<string> SplitKeepingLineFeeds(string text)
    {
        int start = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\n', start);
            end = end < 0 ? text.Length : end + 1;
            yield return text[start..end];
            start = end;
        }
    }

    private static string? CanonicalLine(Match? match, string line)
    {
        string type = match?.Groups[1].Value ?? "";
        string name = match?.Groups[8].Value ?? "";
        return type switch
        {
            "d" when name is "." or ".." => null,
            "d" => $"d?????????    N U         U               N ???  N NN:NN {name}\n",
            "-" => string.Create(
                CultureInfo.InvariantCulture,
                $"-{match!.Groups[2].Value}???????{PerlInteger(match.Groups[5].Value),5} U         U {PerlInteger(match.Groups[6].Value),15} {match.Groups[7].Value} {name}\n"),
            _ => line,
        };
    }

    private static int Done(Action action)
    {
        action();
        return 0;
    }
}
