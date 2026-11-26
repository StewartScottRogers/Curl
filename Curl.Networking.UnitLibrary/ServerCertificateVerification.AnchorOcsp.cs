using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The OCSP half of the revocation check below a non-self-signed anchor: the responder a
/// certificate's authority information access names is asked, and its answer is preferred
/// over any CRL, as the Windows chain engine prefers it (BL-5877, STD-196).
/// </summary>
internal sealed partial class ServerCertificateVerification
{
    private const string AuthorityInformationAccessOid = "1.3.6.1.5.5.7.1.1";

    private const string Sha1Oid = "1.3.14.3.2.26";

    /// <summary>
    /// Downloads an OCSP response from the address of an RFC 6960 GET request, on the terms of
    /// <see cref="FetchRevocationList" />, whose time budget it shares. A test seam.
    /// </summary>
    internal Func<Uri, CancellationToken, byte[]> FetchOcspResponse { get; init; } = RevocationListDownload.Fetch;

    // The first responder that says good or revoked decides; nothing when none does (no responder, no
    // answer, an unknown status, an unusable response or an out-of-date good one), which leaves it to the CRLs.
    private BelowAnchorRevocation? OcspRevocationOf(X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget) =>
        OcspResponderUris(certificate)
            .Select(responder => OcspAnswerOf(responder, certificate, issuer, budget))
            .FirstOrDefault(answer => answer is not null);

    // The response must be signed by the issuer, or by a certificate the issuer signed for OCSP signing.
    private BelowAnchorRevocation? OcspAnswerOf(Uri responder, X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        var address = OcspGetAddress(responder, OcspRequestFor(certificate, issuer));
        var response = TryFetch(FetchOcspResponse, "OCSP response", address, budget);
        return OcspStapleVerifier.VerifyForSchannelRevocationCheck(response, [certificate.RawData, issuer.RawData], timeProvider.GetUtcNow()).Status switch
        {
            OcspStapleStatus.Good => BelowAnchorRevocation.Clean,
            OcspStapleStatus.Revoked => BelowAnchorRevocation.Revoked,
            _ => null,
        };
    }

    // The http OCSP responders of a certificate's authority information access (RFC 5280, 4.2.2.1);
    // none for a certificate without the extension or with one that does not parse.
    internal static IReadOnlyList<Uri> OcspResponderUris(X509Certificate2 certificate)
    {
        if (certificate.Extensions[AuthorityInformationAccessOid] is not { } extension)
        {
            return [];
        }

        try
        {
            return new X509AuthorityInformationAccessExtension(extension.RawData)
                .EnumerateOcspUris()
                .Select(HttpAddressOrNothing)
                .OfType<Uri>()
                .ToArray();
        }
        catch (CryptographicException)
        {
            return [];
        }
    }

    // RFC 6960, appendix A.1: {responder}/{URL-encoding of the base64 of the DER request}.
    internal static Uri OcspGetAddress(Uri responder, byte[] request) =>
        new($"{responder.AbsoluteUri.TrimEnd('/')}/{Uri.EscapeDataString(Convert.ToBase64String(request))}");

    // OCSPRequest ::= SEQUENCE { tbsRequest SEQUENCE { requestList SEQUENCE OF SEQUENCE { reqCert CertID } } }
    // (RFC 6960, 4.1.1), one SHA-1 CertID and no nonce, so a responder's pre-signed answer fits it.
    internal static byte[] OcspRequestFor(X509Certificate2 certificate, X509Certificate2 issuer)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        using (writer.PushSequence())
        using (writer.PushSequence())
        using (writer.PushSequence())
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(Sha1Oid);
                writer.WriteNull();
            }

            writer.WriteOctetString(SHA1.HashData(issuer.SubjectName.RawData));
            writer.WriteOctetString(SHA1.HashData(issuer.PublicKey.EncodedKeyValue.RawData));
            writer.WriteInteger(certificate.SerialNumberBytes.Span);
        }

        return writer.Encode();
    }
}
