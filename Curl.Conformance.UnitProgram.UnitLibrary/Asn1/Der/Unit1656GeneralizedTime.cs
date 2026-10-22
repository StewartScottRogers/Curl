using System.Text;
using Curl.Api.Codes;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.Asn1.Der;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1656.c</c>: <c>GTime2str</c>, here
/// <see cref="DerText.Format"/> over a <c>GeneralizedTime</c>, run on each of the C program's 26 rows in
/// order into a 32 KiB <c>dynbuf</c>, writing its line for each row whose code or text differs. The
/// Schannel build compiles the checks; the OpenSSL build only prints that they are not tested and passes,
/// and so does this port off Windows (ADR-0723).
/// </summary>
/// <param name="printGeneralizedTime">Prints a <c>GeneralizedTime</c>'s content; <see cref="DerText.Format"/> unless a test injects another.</param>
/// <param name="printsGeneralizedTime">Whether this is the Schannel build, whose curl compiles <c>GTime2str</c>.</param>
public sealed class Unit1656GeneralizedTime(Func<byte[], string> printGeneralizedTime, bool printsGeneralizedTime) : IUnitProgram
{
    /// <summary>The line the OpenSSL build's program prints with <c>puts</c> in place of its checks.</summary>
    public const string NotTestedLine = "not tested since Curl_x509_GTime2str() is not built in\n";

    private const int GeneralizedTimeTag = 24;

    private const int BufferLimit = 32 * 1024;

    private static readonly (string Input, string Expected, CurlCode Result)[] Rows =
    [
        ("190321134340", "1903-21-13 43:40:00", CurlCode.Ok),
        ("", "", CurlCode.BadFunctionArgument),
        ("WTF", "", CurlCode.BadFunctionArgument),
        ("0WTF", "", CurlCode.BadFunctionArgument),
        ("19032113434", "", CurlCode.BadFunctionArgument),
        ("19032113434WTF", "", CurlCode.BadFunctionArgument),
        ("190321134340.", "", CurlCode.BadFunctionArgument),
        ("190321134340.1", "1903-21-13 43:40:00.1", CurlCode.Ok),
        ("19032113434017.0", "1903-21-13 43:40:17", CurlCode.Ok),
        ("19032113434017.01", "1903-21-13 43:40:17.01", CurlCode.Ok),
        ("19032113434003.001", "1903-21-13 43:40:03.001", CurlCode.Ok),
        ("19032113434003.090", "1903-21-13 43:40:03.09", CurlCode.Ok),
        ("190321134340Z", "1903-21-13 43:40:00 GMT", CurlCode.Ok),
        ("19032113434017.0Z", "1903-21-13 43:40:17 GMT", CurlCode.Ok),
        ("19032113434017.01Z", "1903-21-13 43:40:17.01 GMT", CurlCode.Ok),
        ("19032113434003.001Z", "1903-21-13 43:40:03.001 GMT", CurlCode.Ok),
        ("19032113434003.090Z", "1903-21-13 43:40:03.09 GMT", CurlCode.Ok),
        ("190321134340CET", "1903-21-13 43:40:00 CET", CurlCode.Ok),
        ("19032113434017.0CET", "1903-21-13 43:40:17 CET", CurlCode.Ok),
        ("19032113434017.01CET", "1903-21-13 43:40:17.01 CET", CurlCode.Ok),
        ("190321134340+02:30", "1903-21-13 43:40:00 UTC+02:30", CurlCode.Ok),
        ("19032113434017.0+02:30", "1903-21-13 43:40:17 UTC+02:30", CurlCode.Ok),
        ("19032113434017.01+02:30", "1903-21-13 43:40:17.01 UTC+02:30", CurlCode.Ok),
        ("190321134340-3", "1903-21-13 43:40:00 UTC-3", CurlCode.Ok),
        ("19032113434017.0-04", "1903-21-13 43:40:17 UTC-04", CurlCode.Ok),
        ("19032113434017.01-01:10", "1903-21-13 43:40:17.01 UTC-01:10", CurlCode.Ok),
    ];

    /// <summary>Creates the port over <see cref="DerText.Format"/>, running the checks on Windows only, as the platform's curl build does.</summary>
    public Unit1656GeneralizedTime()
        : this(OperatingSystem.IsWindows())
    {
    }

    /// <summary>Creates the port over <see cref="DerText.Format"/>.</summary>
    /// <param name="printsGeneralizedTime">Whether to run the checks, as the Schannel build does, or print that <c>GTime2str</c> is not built in.</param>
    public Unit1656GeneralizedTime(bool printsGeneralizedTime)
        : this(content => DerText.Format(content, new DerElement(GeneralizedTimeTag, false, 0, content.Length, content.Length)), printsGeneralizedTime)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1656";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!printsGeneralizedTime)
        {
            await context.StandardOutput.WriteAsync(Encoding.ASCII.GetBytes(NotTestedLine), cancellationToken).ConfigureAwait(false);
            return UnitProgramResult.Success;
        }

        bool allOk = true;
        for (int index = 0; index < Rows.Length; index++)
        {
            allOk &= CheckRow(context.StandardError, index, Rows[index]);
        }

        UnitChecks checks = new(context.StandardError, ProgramName);
        checks.FailUnless(allOk, "some tests of Curl_x509_GTime2str() fails");
        return checks.Result;
    }

    private bool CheckRow(TextWriter standardError, int index, (string Input, string Expected, CurlCode Result) row)
    {
        byte[] input = Encoding.ASCII.GetBytes(row.Input);
        DerTextOutcome outcome = DerTextOutcome.Print(() => printGeneralizedTime(input), BufferLimit);
        if (outcome.Code != row.Result)
        {
            standardError.WriteLine($"test {index}: expect result {(int)row.Result}, got {(int)outcome.Code}");
            return false;
        }

        string actual = Encoding.UTF8.GetString(outcome.Text);
        if (outcome.Code == CurlCode.Ok && actual != row.Expected)
        {
            standardError.WriteLine($"test {index}: input '{row.Input}', expected output '{row.Expected}', got '{actual}'");
            return false;
        }

        return true;
    }
}
