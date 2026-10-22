using System.Text;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.Asn1.Certinfo;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1651.c</c>: <c>Curl_extract_certinfo</c>, here
/// <see cref="PeerCertificateText.Format(ReadOnlyMemory{byte})"/>, must read <see cref="Unit1651Certificate"/>
/// (one check); then, as the C program's "poor man's fuzzing", each of the certificate's first 45 bytes is
/// overwritten in turn with 1, 18, 35 and so on up to 239 and the certificate extracted again, the result
/// ignored. Curl's refusal of a damaged certificate is that ignored result; any exception it throws instead
/// is a failure, where the C program would crash. The GnuTLS and Schannel builds compile the checks; the
/// OpenSSL build only prints that they are not tested and passes, and so does this port off Windows (ADR-0723).
/// </summary>
/// <param name="extractsText">Prints a certificate's records, empty when it cannot be read; <see cref="PeerCertificateText.Format(ReadOnlyMemory{byte})"/> unless a test injects another.</param>
/// <param name="extractsCertinfo">Whether this is the Schannel build, whose curl compiles <c>Curl_extract_certinfo</c>.</param>
public sealed class Unit1651CertificateExtraction(Func<ReadOnlyMemory<byte>, string> extractsText, bool extractsCertinfo) : IUnitProgram
{
    /// <summary>The line the OpenSSL build's program prints with <c>puts</c> in place of its checks.</summary>
    public const string NotTestedLine = "not tested since Curl_extract_certinfo() is not built in\n";

    private const int FuzzedByteCount = 45;

    /// <summary>Creates the port over <see cref="PeerCertificateText.Format(ReadOnlyMemory{byte})"/>, running the checks on Windows only, as the platform's curl build does.</summary>
    public Unit1651CertificateExtraction()
        : this(OperatingSystem.IsWindows())
    {
    }

    /// <summary>Creates the port over <see cref="PeerCertificateText.Format(ReadOnlyMemory{byte})"/>.</summary>
    /// <param name="extractsCertinfo">Whether to run the checks, as the Schannel build does, or print that <c>Curl_extract_certinfo</c> is not built in.</param>
    public Unit1651CertificateExtraction(bool extractsCertinfo)
        : this(PeerCertificateText.Format, extractsCertinfo)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1651";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!extractsCertinfo)
        {
            await context.StandardOutput.WriteAsync(Encoding.ASCII.GetBytes(NotTestedLine), cancellationToken).ConfigureAwait(false);
            return UnitProgramResult.Success;
        }

        UnitChecks checks = new(context.StandardError, ProgramName);
        byte[] certificate = Unit1651Certificate.Create();
        checks.FailUnless(Extracts(certificate, checks), "Curl_extract_certinfo returned error");
        for (int value = 1; value < 255; value += 17)
        {
            for (int index = 0; index < FuzzedByteCount; index++)
            {
                byte backup = certificate[index];
                certificate[index] = (byte)value;
                Extracts(certificate, checks);
                certificate[index] = backup;
            }
        }

        return checks.Result;
    }

    // Curl_extract_certinfo's CURLE_OK: the certificate printed. A refusal prints nothing; anything thrown
    // instead is where the C program would crash, so it fails a check.
    private bool Extracts(byte[] certificate, UnitChecks checks)
    {
        try
        {
            return extractsText(certificate).Length > 0;
        }
#pragma warning disable CA1031 // Any exception is the crash the C program's fuzzing guards against, so each is reported, not rethrown.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            checks.FailUnless(false, $"Curl_extract_certinfo threw {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }
}
