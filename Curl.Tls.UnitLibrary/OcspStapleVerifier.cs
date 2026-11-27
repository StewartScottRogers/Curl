using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// Checks an OCSP response (RFC 6960) for a server certificate the way curl's OpenSSL build
/// checks a stapled one for <c>--cert-status</c>, in its order: the response parses and is
/// <c>successful</c>; the leaf's issuer is in the server's chain; the responder's signature
/// verifies, and the responder is the issuer or a currently valid certificate the issuer
/// signed with <c>id-kp-OCSPSigning</c>; a <c>SingleResponse</c> names the leaf by a SHA-1
/// <c>CertID</c>; its status is good; and <c>thisUpdate</c> and <c>nextUpdate</c> hold at the
/// given time with OpenSSL's five minutes' leeway (<c>OCSP_check_validity</c>, no maximum
/// age). <see cref="VerifyForSchannelRevocationCheck" /> differs in one rule: it accepts a
/// delegated responder that is not yet valid, as curl's Schannel build does.
/// </summary>
public static class OcspStapleVerifier
{
    private static readonly TimeSpan Leeway = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Checks <paramref name="ocspResponse" /> for the leaf of <paramref name="serverCertificates" /> at
    /// <paramref name="now" /> as <c>--cert-status</c> does: a delegated responder must be valid at that time.
    /// </summary>
    /// <param name="ocspResponse">The DER <c>OCSPResponse</c> the server stapled, or <see langword="null" /> when it stapled none.</param>
    /// <param name="serverCertificates">The server's DER certificates, leaf first.</param>
    /// <param name="now">The time to judge a delegated responder's validity, <c>thisUpdate</c> and <c>nextUpdate</c> against.</param>
    /// <returns>The outcome; only <see cref="OcspStapleStatus.Good" /> passes.</returns>
    /// <exception cref="ArgumentException"><paramref name="serverCertificates" /> is empty.</exception>
    public static OcspStapleOutcome Verify(byte[]? ocspResponse, IReadOnlyList<byte[]> serverCertificates, DateTimeOffset now) =>
        VerifyResponse(ocspResponse, serverCertificates, now, acceptNotYetValidResponder: false);

    /// <summary>
    /// Checks <paramref name="ocspResponse" /> as <see cref="Verify" /> does, but for the Schannel build's
    /// revocation check: a delegated responder not yet valid at <paramref name="now" /> is accepted; an
    /// expired one is still refused.
    /// </summary>
    /// <param name="ocspResponse">The DER <c>OCSPResponse</c>, stapled or fetched, or <see langword="null" /> when there is none.</param>
    /// <param name="serverCertificates">DER certificates: the one asked about first, among them its issuer.</param>
    /// <param name="now">The time to judge a delegated responder's expiry, <c>thisUpdate</c> and <c>nextUpdate</c> against.</param>
    /// <returns>The outcome; only <see cref="OcspStapleStatus.Good" /> passes.</returns>
    /// <exception cref="ArgumentException"><paramref name="serverCertificates" /> is empty.</exception>
    public static OcspStapleOutcome VerifyForSchannelRevocationCheck(byte[]? ocspResponse, IReadOnlyList<byte[]> serverCertificates, DateTimeOffset now) =>
        VerifyResponse(ocspResponse, serverCertificates, now, acceptNotYetValidResponder: true);

    private static OcspStapleOutcome VerifyResponse(byte[]? ocspResponse, IReadOnlyList<byte[]> serverCertificates, DateTimeOffset now, bool acceptNotYetValidResponder)
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
            return Check(ocspResponse, serverCertificates, now, acceptNotYetValidResponder);
        }
        catch (AsnContentException)
        {
            return new OcspStapleOutcome(OcspStapleStatus.Malformed);
        }
    }

    private static OcspStapleOutcome Check(byte[] ocspResponse, IReadOnlyList<byte[]> serverCertificates, DateTimeOffset now, bool acceptNotYetValidResponder)
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

        if (CheckResponder(basic, issuer, now, acceptNotYetValidResponder) is { } responderProblem)
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
    private static OcspStapleStatus? CheckResponder(OcspBasicResponse basic, OcspCertificateFields issuer, DateTimeOffset now, bool acceptNotYetValidResponder)
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

        return IsAuthorised(responder, issuer, now, acceptNotYetValidResponder) ? null : OcspStapleStatus.ResponderNotAuthorised;
    }

    private static bool IsResponder(OcspBasicResponse basic, OcspCertificateFields candidate) => basic.ResponderName is { } name
        ? name.AsSpan().SequenceEqual(candidate.Subject)
        : basic.ResponderKeyHash.AsSpan().SequenceEqual(SHA1.HashData(candidate.PublicKey.KeyBits));

    /// <summary>RFC 6960 section 4.2.2.2: the issuer itself, or a certificate it issued for OCSP signing that is within its validity.</summary>
    private static bool IsAuthorised(OcspCertificateFields responder, OcspCertificateFields issuer, DateTimeOffset now, bool acceptNotYetValidResponder) =>
        ReferenceEquals(responder, issuer)
        || (IsWithinValidity(responder, now, acceptNotYetValidResponder) && IsDelegatedBy(responder, issuer));

    /// <summary>Not past <c>notAfter</c>, and not before <c>notBefore</c> unless that is accepted; no leeway, as OpenSSL's chain check has none.</summary>
    private static bool IsWithinValidity(OcspCertificateFields responder, DateTimeOffset now, bool acceptNotYetValidResponder) =>
        responder.NotAfter >= now && (acceptNotYetValidResponder || responder.NotBefore <= now);

    private static bool IsDelegatedBy(OcspCertificateFields responder, OcspCertificateFields issuer) =>
        responder.CanSignOcspResponses
        && responder.Issuer.AsSpan().SequenceEqual(issuer.Subject)
        && Verifies(issuer.PublicKey, responder.SignatureAlgorithm, responder.TbsCertificate, responder.Signature);

    private static bool Verifies(TlsCertificatePublicKey signer, byte[] algorithm, byte[] content, byte[] signature) =>
        OcspSignatureAlgorithm.FindRule(algorithm, signer) is { } rule && signer.VerifySignature(rule, content, signature) is null;

    /// <summary>Only a SHA-1 <c>CertID</c> names the leaf: curl asks by SHA-1 and takes no answer under another hash (ADR-1431).</summary>
    private static bool NamesCertificate(OcspSingleResponse response, OcspCertificateFields leaf, OcspCertificateFields issuer) =>
        OcspSignatureAlgorithm.FindHash(response.HashAlgorithmOid) == HashAlgorithmName.SHA1
        && response.SerialNumber.AsSpan().SequenceEqual(leaf.SerialNumber)
        && response.IssuerNameHash.AsSpan().SequenceEqual(SHA1.HashData(issuer.Subject))
        && response.IssuerKeyHash.AsSpan().SequenceEqual(SHA1.HashData(issuer.PublicKey.KeyBits));

    private static bool IsCurrent(OcspSingleResponse response, DateTimeOffset now) =>
        response.ThisUpdate <= now + Leeway
        && (response.NextUpdate is not { } nextUpdate || (nextUpdate >= now - Leeway && nextUpdate >= response.ThisUpdate));
}
