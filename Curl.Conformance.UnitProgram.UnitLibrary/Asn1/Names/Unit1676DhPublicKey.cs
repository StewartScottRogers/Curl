using System.Text;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.Asn1.Names;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1676.c</c>: <c>Curl_extract_certinfo</c>, here
/// <see cref="PeerCertificateText.Format"/>, over the C program's hand-made certificate with a
/// Diffie-Hellman key, whose <c>dh(p):</c>, <c>dh(g):</c> and <c>dh(pub_key):</c> records must read
/// <c>17</c>, <c>34</c> and <c>51</c>. The Schannel build compiles the checks; the OpenSSL build only
/// prints that they are not tested and passes, and so does this port off Windows (ADR-0723).
/// </summary>
/// <param name="format">Prints a certificate's records; <see cref="PeerCertificateText.Format"/> unless a test injects another.</param>
/// <param name="extractsCertificateInfo">Whether this is the Schannel build, whose curl compiles <c>Curl_extract_certinfo</c>.</param>
public sealed class Unit1676DhPublicKey(Func<ReadOnlyMemory<byte>, string> format, bool extractsCertificateInfo) : IUnitProgram
{
    /// <summary>The line the OpenSSL build's program prints with <c>puts</c> in place of its checks.</summary>
    public const string NotTestedLine = "not tested since Curl_extract_certinfo() is not built in\n";

    private static readonly byte[] Certificate =
    [
        0x30, 0x81, 0x85, 0x30, 0x72, 0xA0, 0x03, 0x02, 0x01, 0x02, 0x02, 0x01,
        0x01, 0x30, 0x0B, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01,
        0x01, 0x0B, 0x30, 0x0F, 0x31, 0x0D, 0x30, 0x0B, 0x06, 0x03, 0x55, 0x04,
        0x03, 0x0C, 0x04, 0x74, 0x65, 0x73, 0x74, 0x30, 0x1E, 0x17, 0x0D, 0x32,
        0x35, 0x30, 0x31, 0x30, 0x31, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x5A,
        0x17, 0x0D, 0x32, 0x36, 0x30, 0x31, 0x30, 0x31, 0x30, 0x30, 0x30, 0x30,
        0x30, 0x30, 0x5A, 0x30, 0x0F, 0x31, 0x0D, 0x30, 0x0B, 0x06, 0x03, 0x55,
        0x04, 0x03, 0x0C, 0x04, 0x74, 0x65, 0x73, 0x74, 0x30, 0x19, 0x30, 0x11,
        0x06, 0x07, 0x2A, 0x86, 0x48, 0xCE, 0x3E, 0x02, 0x01, 0x30, 0x06, 0x02,
        0x01, 0x11, 0x02, 0x01, 0x22, 0x03, 0x04, 0x00, 0x02, 0x01, 0x33, 0x30,
        0x0B, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B,
        0x03, 0x02, 0x00, 0xFF,
    ];

    /// <summary>Creates the port over <see cref="PeerCertificateText.Format"/>, running the checks on Windows only, as the platform's curl build does.</summary>
    public Unit1676DhPublicKey()
        : this(PeerCertificateText.Format, OperatingSystem.IsWindows())
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1676";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!extractsCertificateInfo)
        {
            await context.StandardOutput.WriteAsync(Encoding.ASCII.GetBytes(NotTestedLine), cancellationToken).ConfigureAwait(false);
            return UnitProgramResult.Success;
        }

        UnitChecks checks = new(context.StandardError, ProgramName);
        string text = format(Certificate);
        checks.FailUnless(text.Length > 0, "Curl_extract_certinfo returned error");
        if (text.Length > 0)
        {
            CheckDhRecords(checks, text.Split('\n'));
        }

        return checks.Result;
    }

    private static void CheckDhRecords(UnitChecks checks, string[] lines)
    {
        string? p = LastValue(lines, "dh(p):");
        string? g = LastValue(lines, "dh(g):");
        string? publicKey = LastValue(lines, "dh(pub_key):");
        string? missing = p is null ? "dh(p) not found in certinfo"
            : g is null ? "dh(g) not found in certinfo"
            : publicKey is null ? "dh(pub_key) not found in certinfo"
            : null;
        checks.FailUnless(missing is null, missing!);
        if (missing is null)
        {
            checks.FailUnless(p != g, "dh(p) and dh(g) have the same value (bug: g re-reads p)");
            checks.FailUnless(p == "17", "dh(p) expected 17 (0x11)");
            checks.FailUnless(g == "34", "dh(g) expected 34 (0x22)");
            checks.FailUnless(publicKey == "51", "dh(pub_key) expected 51 (0x33)");
        }
    }

    private static string? LastValue(string[] lines, string label) =>
        lines.LastOrDefault(line => line.StartsWith(label, StringComparison.Ordinal))?[label.Length..];
}
