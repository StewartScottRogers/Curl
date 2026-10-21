using System.Text;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/unit/unit3200.c</c>: each of its six file contents written to the file the
/// case names in the program's first argument and read back with <see cref="CurlFileLineReader"/>, the
/// <c>Curl_get_line</c> counterpart, checking every line the C program checks and then its last read: the
/// end of the file (an <c>abort_unless</c>, which ends the program) or an empty buffer for a line too large.
/// </summary>
/// <param name="cases">The file contents to check, <see cref="UpstreamCases"/> for the C program's own.</param>
public sealed class Unit3200GetLine(IReadOnlyList<GetLineFileCase> cases) : IUnitProgram
{
    private static readonly string C4096 = string.Concat(Enumerable.Repeat("0123456789abcdef", 256));

    /// <summary>Creates the program over the C program's six file contents.</summary>
    public Unit3200GetLine()
        : this(UpstreamCases)
    {
    }

    /// <summary>The C program's six file contents and their checks.</summary>
    public static IReadOnlyList<GetLineFileCase> UpstreamCases { get; } =
    [
        new("LINE1\nLINE2 NEWLINE\n", [("LINE1\n", "First line failed (1)"), ("LINE2 NEWLINE\n", "Second line failed (1)")], EndsAtEndOfFile: true, "Missed EOF (1)"),
        new("LINE1\nLINE2 NONEWLINE", [("LINE1\n", "First line failed (2)"), ("LINE2 NONEWLINE\n", "Second line failed (2)")], EndsAtEndOfFile: true, "Missed EOF (2)"),
        new("LINE1\n" + C4096, [("LINE1\n", "First line failed (3)")], EndsAtEndOfFile: false, "Did not detect max read on EOF (3)"),
        new("LINE1\n" + C4096 + "SOME EXTRA TEXT", [("LINE1\n", "First line failed (4)")], EndsAtEndOfFile: false, "Did not ignore partial on EOF (4)"),
        new("LINE1\n" + C4096 + "SOME EXTRA TEXT\nLINE3\n", [("LINE1\n", "First line failed (5)")], EndsAtEndOfFile: false, "Did not bail out on too long line"),
        new("LINE1\u001aTEST", [("LINE1\u001aTEST\n", "Missed/Misinterpreted ^Z (6)")], EndsAtEndOfFile: true, "Missed EOF (6)"),
    ];

    /// <inheritdoc/>
    public string ProgramName => "unit3200";

    /// <summary>Writes <paramref name="fileCase"/>'s contents to <paramref name="path"/> and checks it as the C program's <c>switch</c> case does.</summary>
    /// <param name="checks">Where a failed check is counted.</param>
    /// <param name="path">The test file.</param>
    /// <param name="fileCase">The contents, the lines expected and the last read's check.</param>
    /// <param name="cancellationToken">Stops the file's write and reads.</param>
    /// <returns><see langword="false"/> when the end-of-file check, an <c>abort_unless</c>, failed and the program must stop.</returns>
    public static async Task<bool> CheckCaseAsync(UnitChecks checks, string path, GetLineFileCase fileCase, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(fileCase);
        await File.WriteAllBytesAsync(path, Encoding.Latin1.GetBytes(fileCase.Contents), cancellationToken).ConfigureAwait(false);
        await using FileStream file = File.OpenRead(path);
        CurlFileLineReader reader = new(file);
        foreach ((string expected, string message) in fileCase.Lines)
        {
            byte[]? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            checks.FailUnless(line is not null && Encoding.Latin1.GetString(line) == expected, message);
        }

        byte[]? last = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (fileCase.EndsAtEndOfFile)
        {
            checks.FailUnless(reader.EndOfFile, fileCase.LastReadMessage);
            return reader.EndOfFile;
        }

        checks.FailUnless(last is null, fileCase.LastReadMessage);
        return true;
    }

    /// <inheritdoc/>
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string path = context.Arguments.Count > 0
            ? context.Arguments[0]
            : throw new ArgumentException("unit3200 needs the test file's path as its argument.", nameof(context));
        UnitChecks checks = new(context.StandardError, ProgramName);
        for (int index = 0; index < cases.Count; index++)
        {
            await context.StandardError.WriteAsync($"Test {index}...").ConfigureAwait(false);
            if (!await CheckCaseAsync(checks, path, cases[index], cancellationToken).ConfigureAwait(false))
            {
                return checks.Result;
            }

            await context.StandardError.WriteLineAsync("OK").ConfigureAwait(false);
        }

        return checks.Result;
    }
}
