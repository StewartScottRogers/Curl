using System.Collections.Frozen;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The text curl prints after <c>curl: (91) </c> when <c>--cert-status</c> rejects the
/// server's stapled OCSP response: curl's OpenSSL <c>verifystatus()</c> messages, which
/// every platform prints (ADR-0191). Measured with curl 8.18.0 on OpenSSL 3.5.5 and with
/// curl.se's Windows build on LibreSSL 4.2.1 (BL-610).
/// </summary>
internal static class CertificateStatusFailureMessages
{
    // The outcomes whose message carries no code.
    private static readonly FrozenDictionary<OcspStapleStatus, string> FixedTexts = new Dictionary<OcspStapleStatus, string>
    {
        [OcspStapleStatus.NoResponse] = "No OCSP response received",
        [OcspStapleStatus.Malformed] = "Invalid OCSP response",
        [OcspStapleStatus.IssuerNotFound] = "Error finding issuer certificate",
        [OcspStapleStatus.SignatureInvalid] = "OCSP response verification failed",
        [OcspStapleStatus.ResponderNotAuthorised] = "OCSP response verification failed",
        [OcspStapleStatus.CertificateNotFound] = "Could not find certificate ID in OCSP response",
        [OcspStapleStatus.Expired] = "OCSP response has expired",

        // curl sets exit 91 for an unknown status without a message of its own, so the
        // tool prints curl_easy_strerror's text for the code.
        [OcspStapleStatus.Unknown] = "SSL server certificate status verification FAILED",
    }.ToFrozenDictionary();

    // OpenSSL's OCSP_crl_reason_str names; 7 is unused in RFC 5280.
    private static readonly FrozenDictionary<int, string> RevocationReasonNames = new Dictionary<int, string>
    {
        [0] = "unspecified",
        [1] = "keyCompromise",
        [2] = "cACompromise",
        [3] = "affiliationChanged",
        [4] = "superseded",
        [5] = "cessationOfOperation",
        [6] = "certificateHold",
        [8] = "removeFromCRL",
    }.ToFrozenDictionary();

    // OpenSSL's OCSP_response_status_str names.
    private static readonly FrozenDictionary<int, string> ResponseStatusNames = new Dictionary<int, string>
    {
        [0] = "successful",
        [1] = "malformedrequest",
        [2] = "internalerror",
        [3] = "trylater",
        [5] = "sigrequired",
        [6] = "unauthorized",
    }.ToFrozenDictionary();

    /// <summary>Returns curl's message for a stapled response the check rejected.</summary>
    /// <param name="outcome">What the check found; never <see cref="OcspStapleStatus.Good" />.</param>
    /// <returns>
    /// The message: for a revoked certificate <c>SSL certificate revocation reason: &lt;name&gt; (&lt;code&gt;)</c>,
    /// for an unsuccessful response <c>Invalid OCSP response status: &lt;name&gt; (&lt;code&gt;)</c>, with
    /// OpenSSL's <c>(UNKNOWN)</c> for a code it has no name for, and a fixed text otherwise.
    /// </returns>
    public static string For(OcspStapleOutcome outcome) => outcome.Status switch
    {
        OcspStapleStatus.Revoked => $"SSL certificate revocation reason: {NameOf(RevocationReasonNames, outcome.Code)} ({outcome.Code})",
        OcspStapleStatus.Unsuccessful => $"Invalid OCSP response status: {NameOf(ResponseStatusNames, outcome.Code)} ({outcome.Code})",
        _ => FixedTexts[outcome.Status],
    };

    private static string NameOf(FrozenDictionary<int, string> names, int code) =>
        names.GetValueOrDefault(code, "(UNKNOWN)");
}
