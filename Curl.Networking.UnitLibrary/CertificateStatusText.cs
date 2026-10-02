using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The <c>-v</c> line curl prints once <c>--cert-status</c> has found the server's
/// certificate in a stapled OCSP response it accepted as current: <c>SSL certificate status:
/// good (0)</c>, <c>revoked (1)</c> or <c>unknown (2)</c>, OpenSSL's
/// <c>OCSP_cert_status_str</c> name and code, from <c>verifystatus()</c> in
/// <c>lib/vtls/openssl.c</c>. Every platform prints it, as every platform prints
/// <see cref="CertificateStatusFailureMessages" />'s texts (ADR-0191, BL-875).
/// </summary>
internal static class CertificateStatusText
{
    /// <summary>Reports the status line for a checked stapled response, when curl prints one.</summary>
    /// <param name="events">Receives the line.</param>
    /// <param name="outcome">
    /// What the check found, or <see langword="null" /> when <c>--cert-status</c> was not given.
    /// Only a good, revoked or unknown status has a line: curl stops before it for a response
    /// it could not read, verify or find the certificate in, or one that has expired.
    /// </param>
    public static void Report(ITransferEvents events, OcspStapleOutcome? outcome)
    {
        var line = outcome?.Status switch
        {
            OcspStapleStatus.Good => "SSL certificate status: good (0)",
            OcspStapleStatus.Revoked => "SSL certificate status: revoked (1)",
            OcspStapleStatus.Unknown => "SSL certificate status: unknown (2)",
            _ => null,
        };
        if (line is not null)
        {
            events.ReportInfo(line);
        }
    }
}
