using System.Text;
using Curl.Api.Codes;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.Asn1.Der;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1667.c</c>: <c>ASN1tostr</c>, here
/// <see cref="DerText.Format"/> on a primitive element of each row's tag, run on each of the C program's
/// 105 rows in order into a <c>dynbuf</c> of 40 bytes, so text of 40 bytes or more is <c>CURLE_TOO_LARGE</c>.
/// Each row's content is its C literal's first <c>size</c> bytes, its terminating NUL included, and its
/// text is compared as a C string, up to the first NUL. It writes the C program's lines for each row
/// whose code, length or text differs. The Schannel build compiles the checks; the OpenSSL build only
/// prints that they are not tested and passes, and so does this port off Windows (ADR-0723).
/// </summary>
/// <param name="format">Prints a primitive element's content by its tag; <see cref="DerText.Format"/> unless a test injects another.</param>
/// <param name="formatsElements">Whether this is the Schannel build, whose curl compiles <c>ASN1tostr</c>.</param>
public sealed class Unit1667ElementText(Func<int, byte[], string> format, bool formatsElements) : IUnitProgram
{
    /// <summary>The line the OpenSSL build's program prints with <c>puts</c> in place of its checks.</summary>
    public const string NotTestedLine = "not tested since ASN1tostr() is not built in\n";

    private const int BufferLimit = 40;

    private const CurlCode Ok = CurlCode.Ok;
    private const CurlCode Bad = CurlCode.BadFunctionArgument;
    private const CurlCode TooLarge = CurlCode.TooLarge;

    private const int Boolean = 1;
    private const int Integer = 2;
    private const int BitString = 3;
    private const int OctetString = 4;
    private const int Null = 5;
    private const int ObjectIdentifier = 6;
    private const int Enumerated = 10;
    private const int Utf8String = 12;
    private const int NumericString = 18;
    private const int PrintableString = 19;
    private const int TeletexString = 20;
    private const int Ia5String = 22;
    private const int UtcTime = 23;
    private const int GeneralizedTime = 24;
    private const int VisibleString = 26;
    private const int UniversalString = 28;
    private const int BmpString = 30;

    private static readonly string Ff11 = new('\xff', 11);

    private static readonly (int Tag, string Asn1, int Size, string Out, CurlCode Result)[] Rows =
    [
        (31, "abcde", 5, "", Bad),
        (99, "abcde", 5, "", Bad),
        (Utf8String, "abcde", 5, "abcde", Ok),
        (Utf8String, "\xc3\xa5\xc3\xa4\xc3\xb6", 6, "\xc3\xa5\xc3\xa4\xc3\xb6", Ok),
        (NumericString, "abcde", 5, "abcde", Ok),
        (PrintableString, "abcde", 5, "abcde", Ok),
        (TeletexString, "abcde", 5, "abcde", Ok),
        (Ia5String, "abcde", 5, "abcde", Ok),
        (VisibleString, "abcde", 5, "abcde", Ok),
        (UniversalString, "abcde", 5, "abcde", Bad),
        (UniversalString, "abcd", 4, "abcd", CurlCode.WeirdServerReply),
        (UniversalString, "\x00\x00\x12\x34", 4, "\xe1\x88\xb4", Ok),
        (BmpString, "abcde", 5, "abcde", Bad),
        (GeneralizedTime, "19851106210627.3", 16, "1985-11-06 21:06:27.3", Ok),
        (GeneralizedTime, "19851106210627.3Z", 17, "1985-11-06 21:06:27.3 GMT", Ok),
        (GeneralizedTime, "19851106210627.3-0500", 21, "1985-11-06 21:06:27.3 UTC-0500", Ok),
        (GeneralizedTime, "20231301000000Z", 15, "2023-13-01 00:00:00 GMT", Ok),
        (GeneralizedTime, "20230101000000.123Z", 19, "2023-01-01 00:00:00.123 GMT", Ok),
        (UtcTime, "991231235959Z", 13, "1999-12-31 23:59:59 GMT", Ok),
        (UtcTime, "991231235959+0200", 17, "1999-12-31 23:59:59 +0200", Ok),
        (UtcTime, "991231235959-0200", 17, "1999-12-31 23:59:59 -0200", Ok),
        (UtcTime, "991231235959+999", 16, "1999-12-31 23:59:59 +999", Ok),
        (UtcTime, "240229120000Z", 13, "2024-02-29 12:00:00 GMT", Ok),
        (UtcTime, "491231235959Z", 13, "2049-12-31 23:59:59 GMT", Ok),
        (UtcTime, "500101000000Z", 13, "1950-01-01 00:00:00 GMT", Ok),
        (ObjectIdentifier, "\x2A\x86\x48\xCE\x38\x04\x01", 7, "dsa", Ok),
        (ObjectIdentifier, "\x2A\x86\x48\x86\xF7\x0D\x01\x01\x0D", 9, "sha512WithRSAEncryption", Ok),
        (ObjectIdentifier, "\x2A\x86\x48\xCE\x38\x04\x02", 7, "1.2.840.10040.4.2", Ok),
        (ObjectIdentifier, "\x60\x86\x48\x01\x65\x03\x04\x02\x03", 9, "sha512", Ok),
        (ObjectIdentifier, "\x88\x37\x03", 3, "2.999.3", Ok),
        (ObjectIdentifier, "\x28", 1, "1.0", Ok),
        (ObjectIdentifier, "\x88", 1, "", Bad),
        (Null, "", 0, "", Ok),
        (Null, "a", 1, "", Ok),
        (Null, "aa", 2, "", Ok),
        (OctetString, "\x00\x00\x00\x04\x05", 5, "00:00:00:04:05:", Ok),
        (OctetString, Ff11 + "\xff\xff", 14, "", TooLarge),
        (BitString, "\x00\x55", 2, "55:", Ok),
        (BitString, "\x08\x55", 2, "", Bad),
        (BitString, "\x00\xaa\x55", 3, "aa:55:", Ok),
        (BitString, "\x00" + Ff11 + "\xff\xff", 15, "", TooLarge),
        (BitString, "\x03\xf0", 2, "f0:", Ok),
        (Integer, "\x00", 1, "0", Ok),
        (Integer, "\x01", 1, "1", Ok),
        (Integer, "\x09", 1, "9", Ok),
        (Integer, "\x0a", 1, "10", Ok),
        (Integer, "\x0b", 1, "11", Ok),
        (Integer, "\x11", 1, "17", Ok),
        (Integer, "\x7f", 1, "127", Ok),
        (Integer, "\x80", 1, "-128", Ok),
        (Integer, "\x81", 1, "-127", Ok),
        (Integer, "\x82", 1, "-126", Ok),
        (Integer, "\xff", 1, "-1", Ok),
        (Integer, "\x27\x0f", 2, "9999", Ok),
        (Integer, "\x27\x10", 2, "0x2710", Ok),
        (Integer, "\x7f\x81", 2, "0x7f81", Ok),
        (Integer, "\x81\x7f", 2, "0xffff817f", Ok),
        (Integer, "\xff\xff", 2, "-1", Ok),
        (Integer, "\x00\x00", 2, "0", Ok),
        (Integer, "\x01\x02\x03", 3, "0x10203", Ok),
        (Integer, "\xff\x02\xff", 3, "0xffff02ff", Ok),
        (Integer, "\x00\xff\x00", 3, "0xff00", Ok),
        (Integer, "\x01\x02\x03\x04", 4, "0x1020304", Ok),
        (Integer, "\xff\x02\x03\x04", 4, "0xff020304", Ok),
        (Integer, "\x00\x00\x00\x04", 4, "4", Ok),
        (Integer, "\x00\x00\x00\x04\x05", 5, "00:00:00:04:05:", Ok),
        (Integer, "\xff\x00\x00\x04\x05", 5, "ff:00:00:04:05:", Ok),
        (Integer, Ff11, 11, "ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:", Ok),
        (Integer, Ff11 + "\xff", 12, "ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:", Ok),
        (Integer, Ff11 + "\xff\xff", 14, "", TooLarge),
        (Integer, Ff11 + "\xff\xff\xff", 15, "", TooLarge),
        (Integer, "", 0, "", Bad),
        (Integer, "\x00\x80", 2, "128", Ok),
        (Enumerated, "\x00", 1, "0", Ok),
        (Enumerated, "\x01", 1, "1", Ok),
        (Enumerated, "\x09", 1, "9", Ok),
        (Enumerated, "\x0a", 1, "10", Ok),
        (Enumerated, "\x0b", 1, "11", Ok),
        (Enumerated, "\x11", 1, "17", Ok),
        (Enumerated, "\x7f", 1, "127", Ok),
        (Enumerated, "\x80", 1, "-128", Ok),
        (Enumerated, "\xff", 1, "-1", Ok),
        (Enumerated, "\x7f\x81", 2, "0x7f81", Ok),
        (Enumerated, "\xff\xff", 2, "-1", Ok),
        (Enumerated, "\x00\x00", 2, "0", Ok),
        (Enumerated, "\x01\x02\x03", 3, "0x10203", Ok),
        (Enumerated, "\xff\x02\xff", 3, "0xffff02ff", Ok),
        (Enumerated, "\x00\xff\x00", 3, "0xff00", Ok),
        (Enumerated, "\x01\x02\x03\x04", 4, "0x1020304", Ok),
        (Enumerated, "\xff\x02\x03\x04", 4, "0xff020304", Ok),
        (Enumerated, "\x00\x00\x00\x04", 4, "4", Ok),
        (Enumerated, "\x00\x00\x00\x04\x05", 5, "00:00:00:04:05:", Ok),
        (Enumerated, "\xff\x00\x00\x04\x05", 5, "ff:00:00:04:05:", Ok),
        (Enumerated, Ff11, 11, "ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:", Ok),
        (Enumerated, Ff11 + "\xff", 12, "ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:ff:", Ok),
        (Enumerated, Ff11 + "\xff\xff", 14, "", TooLarge),
        (Enumerated, Ff11 + "\xff\xff\xff", 15, "", TooLarge),
        (Enumerated, "", 0, "", Bad),
        (Boolean, "\xff", 1, "TRUE", Ok),
        (Boolean, "\x01", 1, "", Bad),
        (Boolean, "\x02", 1, "", Bad),
        (Boolean, "\x00", 1, "FALSE", Ok),
        (Boolean, "\x01\x01", 2, "", Bad),
        (Boolean, "\x00\x01", 2, "", Bad),
        (Boolean, "", 0, "", Bad),
    ];

    /// <summary>Creates the port over <see cref="DerText.Format"/>, running the checks on Windows only, as the platform's curl build does.</summary>
    public Unit1667ElementText()
        : this(OperatingSystem.IsWindows())
    {
    }

    /// <summary>Creates the port over <see cref="DerText.Format"/>.</summary>
    /// <param name="formatsElements">Whether to run the checks, as the Schannel build does, or print that <c>ASN1tostr</c> is not built in.</param>
    public Unit1667ElementText(bool formatsElements)
        : this((tag, content) => DerText.Format(content, new DerElement(tag, false, 0, content.Length, content.Length)), formatsElements)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1667";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!formatsElements)
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
        checks.FailUnless(allOk, "some tests of ASN1tostr() failed");
        return checks.Result;
    }

    private bool CheckRow(TextWriter standardError, int index, (int Tag, string Asn1, int Size, string Out, CurlCode Result) row)
    {
        byte[] content = Encoding.Latin1.GetBytes(row.Asn1 + "\0")[..row.Size];
        DerTextOutcome outcome = DerTextOutcome.Print(() => format(row.Tag, content), BufferLimit);
        if (outcome.Code != row.Result)
        {
            standardError.WriteLine($"test {index} (type {row.Tag}): expect result {(int)row.Result}, got {(int)outcome.Code}");
            if (row.Result == Ok)
            {
                standardError.WriteLine($"test {index}: expected output '{Text(Encoding.Latin1.GetBytes(row.Out))}'");
            }

            return false;
        }

        return outcome.Code != Ok || CheckText(standardError, index, row, outcome.Text);
    }

    // strlen and strcmp of the dynbuf's C string against the row's, then the dynbuf's every byte.
    private static bool CheckText(TextWriter standardError, int index, (int Tag, string Asn1, int Size, string Out, CurlCode Result) row, byte[] data)
    {
        byte[] expected = Encoding.Latin1.GetBytes(row.Out);
        int terminator = Array.IndexOf(data, (byte)0);
        byte[] actual = terminator < 0 ? data : data[..terminator];
        bool ok = true;
        if (actual.Length != expected.Length)
        {
            standardError.WriteLine($"test {index} (type {row.Tag}): unexpected length. Got {actual.Length}, expected {expected.Length})");
            ok = false;
        }

        if (!actual.AsSpan().SequenceEqual(expected))
        {
            standardError.WriteLine($"test {index} (type {row.Tag}): expected output '{Text(expected)}', got '{Text(actual)}' (length {data.Length})");
            for (int loop = 0; loop < data.Length; loop++)
            {
                standardError.WriteLine($"test {index}: index {loop} byte {data[loop]:x2}");
            }

            ok = false;
        }

        return ok;
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
