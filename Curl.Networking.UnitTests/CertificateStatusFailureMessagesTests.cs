using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <see cref="CertificateStatusFailureMessages" />: curl's exit 91 text for each rejected
/// stapled OCSP response. The revoked, missing, unknown, <c>tryLater</c> and wrongly signed
/// cases were measured with curl 8.18.0 on OpenSSL 3.5.5 against <c>openssl s_server
/// -status_file</c> (BL-610), and the revoked and missing ones again with curl.se's Windows
/// build on LibreSSL 4.2.1; the rest are curl's <c>verifystatus()</c> strings.
/// </summary>
[TestClass]
public sealed class CertificateStatusFailureMessagesTests
{
    [TestMethod]
    [DataRow(OcspStapleStatus.NoResponse, 0, "No OCSP response received")]
    [DataRow(OcspStapleStatus.Malformed, 0, "Invalid OCSP response")]
    [DataRow(OcspStapleStatus.Unsuccessful, 3, "Invalid OCSP response status: trylater (3)")]
    [DataRow(OcspStapleStatus.Unsuccessful, 4, "Invalid OCSP response status: (UNKNOWN) (4)")]
    [DataRow(OcspStapleStatus.IssuerNotFound, 0, "Error finding issuer certificate")]
    [DataRow(OcspStapleStatus.SignatureInvalid, 0, "OCSP response verification failed")]
    [DataRow(OcspStapleStatus.ResponderNotAuthorised, 0, "OCSP response verification failed")]
    [DataRow(OcspStapleStatus.CertificateNotFound, 0, "Could not find certificate ID in OCSP response")]
    [DataRow(OcspStapleStatus.Expired, 0, "OCSP response has expired")]
    [DataRow(OcspStapleStatus.Unknown, 0, "SSL server certificate status verification FAILED")]
    [DataRow(OcspStapleStatus.Revoked, 0, "SSL certificate revocation reason: unspecified (0)")]
    [DataRow(OcspStapleStatus.Revoked, 1, "SSL certificate revocation reason: keyCompromise (1)")]
    [DataRow(OcspStapleStatus.Revoked, 2, "SSL certificate revocation reason: cACompromise (2)")]
    [DataRow(OcspStapleStatus.Revoked, 3, "SSL certificate revocation reason: affiliationChanged (3)")]
    [DataRow(OcspStapleStatus.Revoked, 4, "SSL certificate revocation reason: superseded (4)")]
    [DataRow(OcspStapleStatus.Revoked, 5, "SSL certificate revocation reason: cessationOfOperation (5)")]
    [DataRow(OcspStapleStatus.Revoked, 6, "SSL certificate revocation reason: certificateHold (6)")]
    [DataRow(OcspStapleStatus.Revoked, 8, "SSL certificate revocation reason: removeFromCRL (8)")]
    [DataRow(OcspStapleStatus.Revoked, -1, "SSL certificate revocation reason: (UNKNOWN) (-1)")]
    public void For_EachRejectedOutcome_IsCurlsText(OcspStapleStatus status, int code, string expected) =>
        Assert.AreEqual(expected, CertificateStatusFailureMessages.For(new OcspStapleOutcome(status, code)));
}
