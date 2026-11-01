using System.Globalization;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Sftp;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/unit/unit2605.c</c>: every row of its <c>Curl_ssh_range</c> table through
/// <c>SftpDownloadPart.ReadRange</c>, checking the code, and for <c>CURLE_OK</c> the start and size, writing
/// the C program's <c>N: 'range' (file size: S)</c> line for each row and <c>ok</c> when none failed.
/// </summary>
public sealed class Unit2605SshRange : IUnitProgram
{
    private static readonly (string Range, long FileSize, long Start, long Size, CurlExitCode Result)[] Rows =
    [
        ("0-9", 100, 0, 10, CurlExitCode.Ok),
        ("1-10", 100, 1, 10, CurlExitCode.Ok),
        ("222222-222222", 300000, 222222, 1, CurlExitCode.Ok),
        ("4294967296 - 4294967297", 4294967298, 4294967296, 2, CurlExitCode.Ok),
        ("-10", 100, 90, 10, CurlExitCode.Ok),
        ("-20", 100, 80, 20, CurlExitCode.Ok),
        ("-1", 100, 99, 1, CurlExitCode.Ok),
        ("-0", 100, 0, 0, CurlExitCode.RangeError),
        ("--2", 100, 0, 0, CurlExitCode.RangeError),
        ("-100", 100, 0, 100, CurlExitCode.Ok),
        ("-101", 100, 0, 100, CurlExitCode.Ok),
        ("-1000", 100, 0, 100, CurlExitCode.Ok),
        ("2-1000", 100, 2, 98, CurlExitCode.Ok),
        (".2-3", 100, 0, 0, CurlExitCode.RangeError),
        ("+2-3", 100, 0, 0, CurlExitCode.RangeError),
        ("2 - 3", 100, 2, 2, CurlExitCode.Ok),
        (" 2 - 3", 100, 2, 2, CurlExitCode.RangeError),
        ("2 - 3 ", 100, 2, 2, CurlExitCode.RangeError),
        ("3-2", 100, 0, 0, CurlExitCode.RangeError),
        ("2.-3", 100, 0, 0, CurlExitCode.RangeError),
        ("-3-2", 100, 0, 0, CurlExitCode.RangeError),
        ("101-102", 100, 0, 0, CurlExitCode.RangeError),
        ("0-", 100, 0, 100, CurlExitCode.Ok),
        ("1-", 100, 1, 99, CurlExitCode.Ok),
        ("99-", 100, 99, 1, CurlExitCode.Ok),
        ("100-", 100, 0, 0, CurlExitCode.RangeError),
    ];

    /// <inheritdoc/>
    public string ProgramName => "unit2605";

    /// <summary>Checks one row's answer as the C program does: the code, and for <c>CURLE_OK</c> the start and size.</summary>
    /// <param name="checks">Where a failed check is counted.</param>
    /// <param name="answer">What <c>ReadRange</c> returned.</param>
    /// <param name="start">The start expected.</param>
    /// <param name="size">The size expected.</param>
    /// <param name="result">The code expected.</param>
    public static void CheckAnswer(UnitChecks checks, (CurlExitCode Result, long Start, long Size) answer, long start, long size, CurlExitCode result)
    {
        ArgumentNullException.ThrowIfNull(checks);
        checks.FailUnless(answer.Result == result, string.Create(CultureInfo.InvariantCulture, $"... returned {(int)answer.Result}"));
        if (answer.Result == CurlExitCode.Ok)
        {
            checks.FailUnless(answer.Start == start, string.Create(CultureInfo.InvariantCulture, $"... start ({answer.Start}) was not {start}"));
            checks.FailUnless(answer.Size == size, string.Create(CultureInfo.InvariantCulture, $"... size ({answer.Size}) was not {size}"));
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
            (string range, long fileSize, long start, long size, CurlExitCode result) = Rows[row];
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{row}: '{range}' (file size: {fileSize})"), cancellationToken).ConfigureAwait(false);
            CheckAnswer(checks, SftpDownloadPart.ReadRange(range, fileSize), start, size, result);
        }

        await WriteOkWhenPassedAsync(output, checks.Result, cancellationToken).ConfigureAwait(false);
        return checks.Result;
    }

    /// <summary>Writes the C program's closing <c>ok</c> line when <paramref name="result"/> counts no failure.</summary>
    /// <param name="output">The program's standard output.</param>
    /// <param name="result">The program's result so far.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once the line, if any, is written.</returns>
    public static async Task WriteOkWhenPassedAsync(StandardOutputLines output, UnitProgramResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (result.FailureCount == 0)
        {
            await output.WriteLineAsync("ok", cancellationToken).ConfigureAwait(false);
        }
    }
}
