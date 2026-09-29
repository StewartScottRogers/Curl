using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// Checks a stapled OCSP response for <c>--cert-status</c> (RFC 6960) the way curl's
/// OpenSSL build does, in its order: the response parses and is <c>successful</c>; the
/// leaf's issuer is in the server's chain; the responder's signature verifies, and the
/// responder is the issuer or a certificate the issuer signed with <c>id-kp-OCSPSigning</c>;
/// a <c>SingleResponse</c> names the leaf by <c>CertID</c>; its status is good; and
/// <c>thisUpdate</c> and <c>nextUpdate</c> hold at the given time with OpenSSL's five
/// minutes' leeway (<c>OCSP_check_validity</c>, no maximum age).
/// </summary>
public static class OcspStapleVerifier
{
    private static readonly TimeSpan Leeway = TimeSpan.FromMinutes(5);

    /// <summary>Checks <paramref name="ocspResponse" /> for the leaf of <paramref name="serverCertificates" /> at <paramref name="now" />.</summary>
    /// <param name="ocspResponse">The DER <c>OCSPResponse</c> the server stapled, or <see langword="null" /> when it stapled none.</param>
    /// <param name="serverCertificates">The server's DER certificates, leaf first.</param>
    /// <param name="now">The time to judge <c>thisUpdate</c> and <c>nextUpdate</c> against.</param>
    /// <returns>The outcome; only <see cref="OcspStapleStatus.Good" /> passes.</returns>
    /// <exception cref="ArgumentException"><paramref name="serverCertificates" /> is empty.</exception>
    public static OcspStapleOutcome Verify(byte[]? ocspResponse, IReadOnlyList<byte[]> serverCertificates, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(serverCertificates);
        if (serverCertificates.Count == 0)
        {
            throw new ArgumentException("The server's chain must hold its leaf certificate.", nameof(serverCertificates));
        }

        if (ocspResponse is null || ocspResponse.Length == 0)
        {
            return new OcspStapleOutcome(OcspStapleStatus.NoResponse);
        }

        try
        {
            return Check(ocspResponse, serverCertificates, now);
        }
        catch (AsnContentException)
        {
            return new OcspStapleOutcome(OcspStapleStatus.Malformed);
        }
    }

    private static OcspStapleOutcome Check(byte[] ocspResponse, IReadOnlyList<byte[]> serverCertificates, DateTimeOffset now)
    {
        OcspBasicResponse? basic = OcspBasicResponse.Read(ocspResponse, out int responseStatus);
        if (basic is null)
        {
            return new OcspStapleOutcome(OcspStapleStatus.Unsuccessful, responseStatus);
        }

        OcspCertificateFields leaf = OcspCertificateFields.Read(serverCertificates[0]);
        OcspCertificateFields? issuer = FindIssuer(leaf, serverCertificates);
        if (issuer is null)
        {
            return new OcspStapleOutcome(OcspStapleStatus.IssuerNotFound);
        }

        if (CheckResponder(basic, issuer) is { } responderProblem)
        {
            return new OcspStapleOutcome(responderProblem);
        }

        return Judge(FindSingleResponse(basic, leaf, issuer), now);
    }

    /// <summary>The status of the response that names the leaf, then whether it is current (curl reports a revoked certificate before an expired response).</summary>
    private static OcspStapleOutcome Judge(OcspSingleResponse? single, DateTimeOffset now)
    {
        if (single is null)
        {
            return new OcspStapleOutcome(OcspStapleStatus.CertificateNotFound);
        }

        if (single.Status != OcspStapleStatus.Good)
        {
            return new OcspStapleOutcome(single.Status, single.RevocationReason);
        }

        return new OcspStapleOutcome(IsCurrent(single, now) ? OcspStapleStatus.Good : OcspStapleStatus.Expired);
    }

    /// <summary>The certificate in the chain whose subject is the leaf's issuer; a self-signed leaf is its own.</summary>
    private static OcspCertificateFields? FindIssuer(OcspCertificateFields leaf, IReadOnlyList<byte[]> serverCertificates)
    {
        foreach (byte[] certificate in serverCertificates)
        {
            OcspCertificateFields candidate = OcspCertificateFields.Read(certificate);
            if (candidate.Subject.AsSpan().SequenceEqual(leaf.Issuer))
            {
                return candidate;
            }
        }

        return null;
    }

    private static OcspSingleResponse? FindSingleResponse(OcspBasicResponse basic, OcspCertificateFields leaf, OcspCertificateFields issuer)
    {
        foreach (OcspSingleResponse response in basic.Responses)
        {
            if (NamesCertificate(response, leaf, issuer))
            {
                return response;
            }
        }

        return null;
    }

    /// <summary>The issuer when the responder ID names it, otherwise the first certificate in the response the ID names.</summary>
    private static OcspCertificateFields? FindResponder(OcspBasicResponse basic, OcspCertificateFields issuer)
    {
        if (IsResponder(basic, issuer))
        {
            return issuer;
        }

        foreach (byte[] certificate in basic.Certificates)
        {
            OcspCertificateFields candidate = OcspCertificateFields.Read(certificate);
            if (IsResponder(basic, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Finds the responder, checks its signature over the response, then its authority to sign for the issuer.</summary>
    private static OcspStapleStatus? CheckResponder(OcspBasicResponse basic, OcspCertificateFields issuer)
    {
        OcspCertificateFields? responder = FindResponder(basic, issuer);
        if (responder is null)
        {
            return OcspStapleStatus.ResponderNotAuthorised;
        }

        if (!Verifies(responder.PublicKey, basic.SignatureAlgorithm, basic.ResponseData, basic.Signature))
        {
            return OcspStapleStatus.SignatureInvalid;
        }

        return IsAuthorised(responder, issuer) ? null : OcspStapleStatus.ResponderNotAuthorised;
    }

    private static bool IsResponder(OcspBasicResponse basic, OcspCertificateFields candidate) => basic.ResponderName is { } name
        ? name.AsSpan().SequenceEqual(candidate.Subject)
        : basic.ResponderKeyHash.AsSpan().SequenceEqual(SHA1.HashData(candidate.PublicKey.KeyBits));

    /// <summary>RFC 6960 section 4.2.2.2: the issuer itself, or a certificate it issued for OCSP signing.</summary>
    private static bool IsAuthorised(OcspCertificateFields responder, OcspCertificateFields issuer) =>
        ReferenceEquals(responder, issuer)
        || (responder.CanSignOcspResponses
            && responder.Issuer.AsSpan().SequenceEqual(issuer.Subject)
            && Verifies(issuer.PublicKey, responder.SignatureAlgorithm, responder.TbsCertificate, responder.Signature));

    private static bool Verifies(TlsCertificatePublicKey signer, byte[] algorithm, byte[] content, byte[] signature) =>
        OcspSignatureAlgorithm.FindRule(algorithm, signer) is { } rule && signer.VerifySignature(rule, content, signature) is null;

    private static bool NamesCertificate(OcspSingleResponse response, OcspCertificateFields leaf, OcspCertificateFields issuer) =>
        OcspSignatureAlgorithm.FindHash(response.HashAlgorithmOid) is { } hash
        && response.SerialNumber.AsSpan().SequenceEqual(leaf.SerialNumber)
        && response.IssuerNameHash.AsSpan().SequenceEqual(CryptographicOperations.HashData(hash, issuer.Subject))
        && response.IssuerKeyHash.AsSpan().SequenceEqual(CryptographicOperations.HashData(hash, issuer.PublicKey.KeyBits));

    private static bool IsCurrent(OcspSingleResponse response, DateTimeOffset now) =>
        response.ThisUpdate <= now + Leeway
        && (response.NextUpdate is not { } nextUpdate || (nextUpdate >= now - Leeway && nextUpdate >= response.ThisUpdate));
}
