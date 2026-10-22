using System.Text;
using Curl.Api.Codes;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.Asn1.Der;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1666.c</c>: <c>encodeOID</c>, here
/// <see cref="DerText.EncodeObjectIdentifier"/>, run on each of the C program's 68 rows in order into a
/// <c>dynbuf</c> of 100 bytes, so a dotted form of 100 characters or more is <c>CURLE_TOO_LARGE</c>, writing
/// its lines for each row whose code or text differs. The Schannel build compiles the checks; the OpenSSL
/// build only prints that they are not tested and passes, and so does this port off Windows (ADR-0723).
/// </summary>
/// <param name="encode">Prints object identifier content in dotted form; <see cref="DerText.EncodeObjectIdentifier"/> unless a test injects another.</param>
/// <param name="encodesObjectIdentifiers">Whether this is the Schannel build, whose curl compiles <c>encodeOID</c>.</param>
public sealed class Unit1666ObjectIdentifierEncoding(Func<byte[], string> encode, bool encodesObjectIdentifiers) : IUnitProgram
{
    /// <summary>The line the OpenSSL build's program prints with <c>puts</c> in place of its checks.</summary>
    public const string NotTestedLine = "not tested since encodeOID() is not built in\n";

    private const int BufferLimit = 100;

    private const CurlCode Bad = CurlCode.BadFunctionArgument;

    private static readonly (string Oid, string Dotted, CurlCode Result)[] Rows =
    [
        ("", "", Bad),
        ("", "", Bad), // "\x81" with a size of 0
        ("\x8F\xFF\xFF\xFF\x7F", "2.4294967215", CurlCode.Ok),
        ("\x90\x80\x80\x80\x00", "", Bad),
        ("\x88\x80\x80\x80\x4F", "2.2147483647", CurlCode.Ok),
        ("\x88\x80\x80\x80\x50", "2.2147483648", CurlCode.Ok),
        ("\x88\x80\x80\x80\x51", "2.2147483649", CurlCode.Ok),
        ("\x88\x80\x80\x80\x52", "2.2147483650", CurlCode.Ok),
        ("\x8F\xFF\xFF\xFF\x7F\x8F\xFF\xFF\xFF\x7F", "2.4294967215.4294967295", CurlCode.Ok),
        ("\xB7\x28\x02", "2.7000.2", CurlCode.Ok),
        ("\x81\x00", "2.48", CurlCode.Ok),
        ("\x81\x00\x01", "2.48.1", CurlCode.Ok),
        ("\x81\x00\x02", "2.48.2", CurlCode.Ok),
        ("\xC0\x80\x81\x1F", "2.134217807", CurlCode.Ok),
        ("\x2b\x06\x01\x04\x01\x82\x37\x15\x14", "1.3.6.1.4.1.311.21.20", CurlCode.Ok),
        ("\x2b\x06\x01\x04\x01\x82\x37\x15", "1.3.6.1.4.1.311.21", CurlCode.Ok),
        ("\x2b\x06\x01\x04\x01\x82\x37", "1.3.6.1.4.1.311", CurlCode.Ok),
        ("\x2b\x06\x01\x04\x01", "1.3.6.1.4.1", CurlCode.Ok),
        ("\x2b\x06\x01\x04", "1.3.6.1.4", CurlCode.Ok),
        ("\x2b\x06\x01", "1.3.6.1", CurlCode.Ok),
        ("\x2b\x06", "1.3.6", CurlCode.Ok),
        ("\x2b", "1.3", CurlCode.Ok),
        ("\x2c", "1.4", CurlCode.Ok),
        ("\x2d", "1.5", CurlCode.Ok),
        ("\x2e", "1.6", CurlCode.Ok),
        ("\x2f", "1.7", CurlCode.Ok),
        ("\x30", "1.8", CurlCode.Ok),
        ("\x31", "1.9", CurlCode.Ok),
        ("\x32", "1.10", CurlCode.Ok),
        ("\x50", "2.0", CurlCode.Ok),
        ("\x7f", "2.47", CurlCode.Ok),
        ("\xff\x7f", "2.16303", CurlCode.Ok),
        ("\xff", "", Bad),
        ("\xff\x80\x01", "2.2080689", CurlCode.Ok),
        ("\xff\x80\x80\x01", "2.266338225", CurlCode.Ok),
        ("\x80\x80\x80\x80\x01", "", Bad),
        ("\x80\x80\x80\x80\x80\x01", "", Bad),
        ("\x80\x80\x80\x80\x80\x80\x01", "", Bad),
        ("\x80\xff\x7f", "", Bad),
        ("\x80\xff\xff\xff\x7f", "", Bad),
        ("\x2b\xff\xff\xff\xff\x7f", "", Bad),
        ("\x80\x80", "", Bad),
        ("\x80\x80\x80", "", Bad),
        ("\x80\x80\x80\x80", "", Bad),
        ("\x80\x80\x80\x80\x80", "", Bad),
        ("\x80\x80\x80\x80\x80\x80", "", Bad),
        ("\x2b\x06\x01\x04\x01\xee", "", Bad),
        ("\x00", "0.0", CurlCode.Ok),
        ("\x01", "0.1", CurlCode.Ok),
        ("\x02", "0.2", CurlCode.Ok),
        ("\x03", "0.3", CurlCode.Ok),
        ("\x04", "0.4", CurlCode.Ok),
        ("\x05", "0.5", CurlCode.Ok),
        ("\x2b" + Repeat("\x01", 41), "1.3" + Repeat(".1", 41), CurlCode.Ok),
        ("\x2b" + Repeat("\x64", 24) + "\x7f", "", CurlCode.TooLarge),
        ("\x2b" + Repeat("\x64", 24) + "\x63", "", CurlCode.TooLarge),
        ("\x2b" + Repeat("\x64", 24) + "\x09", "", CurlCode.TooLarge),
        ("\x2b" + Repeat("\x01", 49), "", CurlCode.TooLarge),
        ("\x2b" + Repeat("\x01", 48), "1.3" + Repeat(".1", 48), CurlCode.Ok), // one byte shorter than the previous is below the limit
        ("\x78", "2.40", CurlCode.Ok),
        ("\x81\x34", "2.100", CurlCode.Ok),
        ("\x2b\x00\x01", "1.3.0.1", CurlCode.Ok),
        ("\x2b\x06\x01\x00", "1.3.6.1.0", CurlCode.Ok),
        ("\x2b\x8f\xff\xff\xff\x7f", "1.3.4294967295", CurlCode.Ok),
        ("\x2b\x81\x00", "1.3.128", CurlCode.Ok),
        ("\x2b\x80\x05", "", Bad),
        ("\x2b\x80\x80\x01", "", Bad),
        ("\x2b\x06\x81", "", Bad),
    ];

    /// <summary>Creates the port over <see cref="DerText.EncodeObjectIdentifier"/>, running the checks on Windows only, as the platform's curl build does.</summary>
    public Unit1666ObjectIdentifierEncoding()
        : this(OperatingSystem.IsWindows())
    {
    }

    /// <summary>Creates the port over <see cref="DerText.EncodeObjectIdentifier"/>.</summary>
    /// <param name="encodesObjectIdentifiers">Whether to run the checks, as the Schannel build does, or print that <c>encodeOID</c> is not built in.</param>
    public Unit1666ObjectIdentifierEncoding(bool encodesObjectIdentifiers)
        : this(DerText.EncodeObjectIdentifier, encodesObjectIdentifiers)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1666";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!encodesObjectIdentifiers)
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
        checks.FailUnless(allOk, "some tests of encodeOID() failed");
        return checks.Result;
    }

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    private bool CheckRow(TextWriter standardError, int index, (string Oid, string Dotted, CurlCode Result) row)
    {
        byte[] oid = Encoding.Latin1.GetBytes(row.Oid);
        DerTextOutcome outcome = DerTextOutcome.Print(() => encode(oid), BufferLimit);
        if (outcome.Code != row.Result)
        {
            standardError.WriteLine($"test {index}: expect result {(int)row.Result}, got {(int)outcome.Code}");
            if (row.Result == CurlCode.Ok)
            {
                standardError.WriteLine($"test {index}: expected output '{row.Dotted}'");
            }

            return false;
        }

        string actual = Encoding.UTF8.GetString(outcome.Text);
        if (outcome.Code == CurlCode.Ok && actual != row.Dotted)
        {
            standardError.WriteLine($"test {index}: expected output '{row.Dotted}', got '{actual}'");
            return false;
        }

        return true;
    }
}
