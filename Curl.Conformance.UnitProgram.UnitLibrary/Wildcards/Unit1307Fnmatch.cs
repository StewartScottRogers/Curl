using System.Globalization;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Original.Protocol.Ftp;

namespace Curl.Conformance.UnitProgram.Wildcards;

/// <summary>
/// Port of curl 8.22.0's <c>tests/unit/unit1307.c</c> (https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1307.c):
/// every row of its <c>tests</c> table, in order, through <see cref="FtpWildcardPattern.Match" />, which answers as
/// <c>Curl_fnmatch</c> does on the platform running the program (ADR-0723). A row's <c>LINUX_*</c> and <c>MAC_*</c>
/// answers apply on their platforms, the base answer everywhere else. The C program's <c>\xNN</c> bytes are the
/// characters of the same value, and like it this one counts one failure, <c>pattern mismatch</c>, per wrong row.
/// </summary>
public sealed class Unit1307Fnmatch : IUnitProgram
{
    private const FtpNameMatchAnswer M = FtpNameMatchAnswer.Match;
    private const FtpNameMatchAnswer N = FtpNameMatchAnswer.NoMatch;
    private const FtpNameMatchAnswer F = FtpNameMatchAnswer.Fail;

    private static readonly Row[] Rows =
    [
        // brackets syntax
        new("*[*" + new string('[', 177) + "\u0001\u007f" + new string('[', 21), new string('[', 200), N, null, F),
        new("\\[", "[", M),
        new("[", "[", N, M, F),
        new("[]", "[]", N, M, F),
        new("[][]", "[", M),
        new("[][]", "]", M),
        new("[[]", "[", M),
        new("[[[]", "[", M),
        new("[[[[]", "[", M),
        new("[[[[]", "[", M),
        new("[][[]", "]", M),
        new("[][[[]", "[", M),
        new("[[]", "]", N),
        new("[a@]", "a", M),
        new("[a-z]", "a", M),
        new("[a-z]", "A", N),
        new("?[a-z]", "?Z", N),
        new("[A-Z]", "C", M),
        new("[A-Z]", "c", N),
        new("[0-9]", "7", M),
        new("[7-8]", "7", M),
        new("[7-]", "7", M),
        new("[7-]", "-", M),
        new("[7-]", "[", N),
        new("[a-bA-F]", "F", M),
        new("[a-bA-B9]", "9", M),
        new("[a-bA-B98]", "8", M),
        new("[a-bA-B98]", "C", N),
        new("[a-bA-Z9]", "F", M),
        new("[a-bA-Z9]ero*", "Zero chance.", M),
        new("S[a-][x]opho*", "Saxophone", M),
        new("S[a-][x]opho*", "SaXophone", N),
        new("S[a-][x]*.txt", "S-x.txt", M),
        new("[\\a-\\b]", "a", M),
        new("[\\a-\\b]", "b", M),
        new("[?*[][?*[][?*[]", "?*[", M),
        new("[][?*-]", "]", M),
        new("[][?*-]", "[", M),
        new("[][?*-]", "?", M),
        new("[][?*-]", "*", M),
        new("[][?*-]", "-", M),
        new("[]?*-]", "-", M),
        new("[ÿ]", "ÿ", M, F, F),
        new("?/b/c", "a/b/c", M),
        new("^_{}~", "^_{}~", M),
        new("!#%+,-./01234567889", "!#%+,-./01234567889", M),
        new("PQRSTUVWXYZ]abcdefg", "PQRSTUVWXYZ]abcdefg", M),
        new(":;=@ABCDEFGHIJKLMNO", ":;=@ABCDEFGHIJKLMNO", M),

        // negate
        new("[!a]", "b", M),
        new("[!a]", "a", N),
        new("[^a]", "b", M),
        new("[^a]", "a", N),
        new("[^a-z0-9A-Z]", "a", N),
        new("[^a-z0-9A-Z]", "-", M),
        new("curl[!a-z]lib", "curl lib", M),
        new("curl[! ]lib", "curl lib", N),
        new("[! ][ ]", "  ", N),
        new("[! ][ ]", "a ", M),
        new("*[^a].t?t", "a.txt", N),
        new("*[^a].t?t", "ca.txt", N),
        new("*[^a].t?t", "ac.txt", M),
        new("*[^a]", "", N),
        new("[!ÿ]", "", N, F),
        new("[!ÿ]", "ÿ", N, F, F),
        new("[!ÿ]", "a", M, F, F),
        new("[!?*[]", "?", N),
        new("[!!]", "!", N),
        new("[!!]", "x", M),
        new("[[:alpha:]]", "a", M),
        new("[[:alpha:]]", "9", N),
        new("[[:alnum:]]", "a", M),
        new("[[:alnum:]]", "[", N),
        new("[[:alnum:]]", "]", N),
        new("[[:alnum:]]", "9", M),
        new("[[:digit:]]", "9", M),
        new("[[:xdigit:]]", "9", M),
        new("[[:xdigit:]]", "F", M),
        new("[[:xdigit:]]", "G", N),
        new("[[:upper:]]", "U", M),
        new("[[:upper:]]", "u", N),
        new("[[:lower:]]", "l", M),
        new("[[:lower:]]", "L", N),
        new("[[:print:]]", "L", M),
        new("[[:print:]]", "\b", N),
        new("[[:print:]]", "\b", N),
        new("[[:print:]]", "\t", N),
        new("[[:print:]]", "\n", N),
        new("[[:print:]]", "\r", N),
        new("[[:space:]]", " ", M),
        new("[[:space:]]", "x", N),
        new("[[:graph:]]", " ", N),
        new("[[:graph:]]", "x", M),
        new("[[:graph:]]", "\t", N),
        new("[[:graph:]]", "\r", N),
        new("[[:blank:]]", "\t", M),
        new("[[:blank:]]", " ", M),
        new("[[:blank:]]", "\r", N),
        new("[^[:blank:]]", "\t", N),
        new("[^[:print:]]", "\b", M),
        new("[[:lower:]][[:lower:]]", "ll", M),
        new("[[:foo:]]", "bar", N, null, F),
        new("[[:foo:]]", "f]", M, N, F),
        new("curl[[:blank:]];-)", "curl ;-)", M),
        new("*[[:blank:]]*", " ", M),
        new("*[[:blank:]]*", "", N),
        new("*[[:blank:]]*", "hi, im_Pavel", M),

        // common using
        new("Filename.dat", "Filename.dat", M),
        new("*curl*", "lets use curl!!", M),
        new("filename.txt", "filename.dat", N),
        new("*.txt", "text.txt", M),
        new("*.txt", "a.txt", M),
        new("*.txt", ".txt", M),
        new("*.txt", "txt", N),
        new("??.txt", "99.txt", M),
        new("??.txt", "a99.txt", N),
        new("?.???", "a.txt", M),
        new("*.???", "somefile.dat", M),
        new("*.???", "photo.jpeg", N),
        new(".*", ".htaccess", M),
        new(".*", ".", M),
        new(".*", "..", M),

        // many stars => one star
        new("**.txt", "text.txt", M),
        new("***.txt", "t.txt", M),
        new("****.txt", ".txt", M),

        // empty string or pattern
        new("", "", M),
        new("", "hello", N),
        new("file", "", N),
        new("?", "", N),
        new("*", "", M),
        new("x", "", N),

        // backslash
        new("\\", "\\", M, N),
        new("\\\\", "\\", M),
        new("\\\\", "\\\\", N),
        new("\\?", "?", M),
        new("\\*", "*", M),
        new("?.txt", "?.txt", M),
        new("*.txt", "*.txt", M),
        new("\\?.txt", "?.txt", M),
        new("\\*.txt", "*.txt", M),
        new("\\?.txt", "x.txt", N),
        new("\\*.txt", "x.txt", N),
        new("\\*\\\\.txt", "*\\.txt", M),
        new("*\\**\\?*\\\\*", "cc*cc?cccc", N),
        new("*\\?*\\**", "cc?cc", N),
        new("\\\"\\$\\&\\'\\(\\)", "\"$&'()", M),
        new("\\*\\?\\[\\\\\\`\\|", "*?[\\`|", M),
        new("[\\a\\b]c", "ac", M),
        new("[\\a\\b]c", "bc", M),
        new("[\\a\\b]d", "bc", N),
        new("[a-bA-B\\?]", "?", M),
        new("cu[a-ab-b\\r]l", "curl", M),
        new("[\\a-z]", "c", M),
        new("?*?*?.*?*", "abc.c", M),
        new("?*?*?.*?*", "abcc", N),
        new("?*?*?.*?*", "abc.", N),
        new("?*?*?.*?*", "abc.c++", M),
        new("?*?*?.*?*", "abcdef.c++", M),
        new("?*?*?.?", "abcdef.c", M),
        new("?*?*?.?", "abcdef.cd", N),

        // U+00E4 Latin Small Letter A with Diaeresis, as UTF-8 bytes
        new("LindmÃ¤tarv", "LindmÃ¤tarv", M),
        new("", "", M),
        new(
            "**]*[*[\u0013]**[*\u0013)]*]*[**[*\u0013~r-]*]**[.*]*[" + new string('ã', 29)
            + "*[\u0013]**[*\u0013)]*]*[*[\u0013]*[~r]*]*º\u0013¦~b-]*",
            "a",
            N,
            F),
    ];

    /// <summary>One row of the C program's <c>tests</c> table: the answer on curl's own matcher, and where given the Linux and macOS ones.</summary>
    /// <param name="Pattern">The wildcard pattern.</param>
    /// <param name="Text">The file name matched against it.</param>
    /// <param name="Custom">The answer of curl's own <c>Curl_fnmatch</c>, which the other platforms share unless their answer is given.</param>
    /// <param name="Linux">The Linux answer, or <see langword="null"/> where it is <paramref name="Custom"/>.</param>
    /// <param name="MacOs">The macOS answer, or <see langword="null"/> where it is <paramref name="Custom"/>.</param>
    public readonly record struct Row(
        string Pattern,
        string Text,
        FtpNameMatchAnswer Custom,
        FtpNameMatchAnswer? Linux = null,
        FtpNameMatchAnswer? MacOs = null);

    /// <inheritdoc/>
    public string ProgramName => "unit1307";

    /// <summary>Checks one row as the C program's loop does, writing a line to standard error when it fails.</summary>
    /// <param name="standardError">Where a failed row's line is written.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="row">The row.</param>
    /// <param name="matcher">The platform whose answers are checked.</param>
    /// <returns><see langword="true"/> when the row passed.</returns>
    public static bool CheckRow(TextWriter standardError, int index, Row row, FtpWildcardMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        FtpNameMatchAnswer expected = matcher switch
        {
            FtpWildcardMatcher.Linux => row.Linux ?? row.Custom,
            FtpWildcardMatcher.MacOs => row.MacOs ?? row.Custom,
            _ => row.Custom,
        };
        FtpNameMatchAnswer actual = FtpWildcardPattern.Match(row.Pattern, row.Text, matcher);
        if (actual == expected)
        {
            return true;
        }

        standardError.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Curl_fnmatch(\"{row.Pattern}\", \"{row.Text}\") should return {NameOf(expected)} (returns {NameOf(actual)}) [{index}]"));
        return false;
    }

    /// <summary>Runs every row of the upstream table for one platform.</summary>
    /// <param name="standardError">Where each failed row's line is written.</param>
    /// <param name="matcher">The platform whose answers are checked.</param>
    /// <returns>The number of rows that failed.</returns>
    public static int CountFailedRows(TextWriter standardError, FtpWildcardMatcher matcher) =>
        CountFailedRows(standardError, matcher, Rows);

    /// <summary>Runs the given rows for one platform.</summary>
    /// <param name="standardError">Where each failed row's line is written.</param>
    /// <param name="matcher">The platform whose answers are checked.</param>
    /// <param name="rows">The rows to check.</param>
    /// <returns>The number of rows that failed.</returns>
    public static int CountFailedRows(TextWriter standardError, FtpWildcardMatcher matcher, IReadOnlyList<Row> rows)
    {
        int failed = 0;
        for (int index = 0; index < rows.Count; index++)
        {
            if (!CheckRow(standardError, index, rows[index], matcher))
            {
                failed++;
            }
        }

        return failed;
    }

    /// <inheritdoc/>
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        FtpWildcardMatcher matcher = FtpWildcardPattern.MatcherFor(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());
        return Task.FromResult(ResultFor(context.StandardError, CountFailedRows(context.StandardError, matcher)));
    }

    /// <summary>The program's result for a count of failed rows: one <c>pattern mismatch</c> failure each, as <c>fail()</c> counts them.</summary>
    /// <param name="standardError">Where each failure is written.</param>
    /// <param name="failedRows">The number of rows that failed.</param>
    /// <returns>The result, whose failure count is <paramref name="failedRows"/>.</returns>
    public UnitProgramResult ResultFor(TextWriter standardError, int failedRows)
    {
        UnitChecks checks = new(standardError, ProgramName);
        for (int mismatch = 0; mismatch < failedRows; mismatch++)
        {
            checks.FailUnless(false, "pattern mismatch");
        }

        return checks.Result;
    }

    private static string NameOf(FtpNameMatchAnswer answer) => answer switch
    {
        FtpNameMatchAnswer.Match => "MATCH",
        FtpNameMatchAnswer.NoMatch => "NOMATCH",
        _ => "FAIL",
    };
}
