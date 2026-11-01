using System.Globalization;
using System.Text;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Sftp;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/unit/unit2604.c</c>: every row of its <c>Curl_get_pathname</c> table
/// (the first a path of 90720 <c>a</c>s, too large) through <c>SftpQuoteCommand.ReadPathname</c>, checking
/// the code, and for <c>CURLE_OK</c> the text left after the path and the path itself, writing the C
/// program's <c>N - Curl_get_pathname(...)</c> line for each row.
/// </summary>
public sealed class Unit2604GetPathname : IUnitProgram
{
    private const int TooLongLength = 90720;

    private static readonly string A540 = new('a', 540);

    private static readonly (string Text, string Expect, string Next, string Home, CurlExitCode Result)[] Rows =
    [
        ("-too-long-", "", "", "", CurlExitCode.TooLarge),
        (A540 + " c", A540, "c", "/", CurlExitCode.Ok),
        ("\" " + A540 + "\" c", " " + A540, "c", "/", CurlExitCode.Ok),
        ("a a", "a", "a", "/home/", CurlExitCode.Ok),
        ("b a", "b", "a", "/", CurlExitCode.Ok),
        ("a", "a", "", "/home/", CurlExitCode.Ok),
        ("b", "b", "", "/", CurlExitCode.Ok),
        ("\"foo bar\"\tb", "foo bar", "b", "/", CurlExitCode.Ok),
        ("/~/hej", "/home/user/hej", "", "/home/user", CurlExitCode.Ok),
        ("\"foo bar", "", "", "/", CurlExitCode.QuoteError),
        ("\"foo\\\"bar\" a", "foo\"bar", "a", "/", CurlExitCode.Ok),
        ("\"foo\\'bar\" b", "foo'bar", "b", "/", CurlExitCode.Ok),
        ("\"foo\\\\bar\" c", "foo\\bar", "c", "/", CurlExitCode.Ok),
        ("\"foo\\pbar\" c", "foo\\bar", "", "/", CurlExitCode.QuoteError),
        ("\"\" c", "", "", "", CurlExitCode.QuoteError),
        ("foo\"", "foo\"", "", "/", CurlExitCode.Ok),
        ("foo \"", "foo", "\"", "/", CurlExitCode.Ok),
        ("   \t\t   \t  ", "", "", "/", CurlExitCode.QuoteError),
        ("              ", "", "", "/", CurlExitCode.QuoteError),
        ("", "", "", "/", CurlExitCode.QuoteError),
        ("       \r \n  ", "\r", "\n  ", "/", CurlExitCode.Ok),
    ];

    /// <inheritdoc/>
    public string ProgramName => "unit2604";

    /// <summary>Checks one row's answer as the C program does: the code, and for <c>CURLE_OK</c> the rest and the path.</summary>
    /// <param name="checks">Where a failed check is counted.</param>
    /// <param name="answer">What <c>ReadPathname</c> returned.</param>
    /// <param name="expect">The path expected.</param>
    /// <param name="next">The rest expected.</param>
    /// <param name="result">The code expected.</param>
    public static void CheckAnswer(UnitChecks checks, (CurlExitCode Result, byte[] Path, string Remainder) answer, string expect, string next, CurlExitCode result)
    {
        ArgumentNullException.ThrowIfNull(checks);
        checks.FailUnless(answer.Result == result, string.Create(CultureInfo.InvariantCulture, $"... returned {(int)answer.Result}"));
        if (answer.Result == CurlExitCode.Ok)
        {
            checks.FailUnless(answer.Remainder == next, $"... cp points to '{answer.Remainder}', not '{next}' as expected");
            string path = Encoding.UTF8.GetString(answer.Path);
            checks.FailUnless(path == expect, $"... gave '{path}', not '{expect}' as expected");
        }
    }

    /// <inheritdoc/>
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        StandardOutputLines output = new(context);
        for (int row = 0; row < Rows.Length; row++)
        {
            (string text, string expect, string next, string home, CurlExitCode result) = Rows[row];
            string value = row == 0 ? new string('a', TooLongLength) : text;
            (CurlExitCode Result, byte[] Path, string Remainder) answer = SftpQuoteCommand.ReadPathname(value, Encoding.UTF8.GetBytes(home));
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{row} - Curl_get_pathname(\"{text}\", ... \"{home}\") == {(int)result}"), cancellationToken).ConfigureAwait(false);
            CheckAnswer(checks, answer, expect, next, result);
        }

        return checks.Result;
    }
}
