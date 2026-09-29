using System.Formats.Asn1;

namespace Curl.Tls;

/// <summary>One DER <c>SingleResponse</c> of an OCSP response (RFC 6960 section 4.2.1): which certificate it is about, its status and when it holds.</summary>
/// <param name="HashAlgorithmOid">The OID of the hash the <c>CertID</c> was made with.</param>
/// <param name="IssuerNameHash">The hash of the issuer's DER subject name.</param>
/// <param name="IssuerKeyHash">The hash of the issuer's <c>subjectPublicKey</c> bits.</param>
/// <param name="SerialNumber">The contents of the certificate's <c>serialNumber</c> integer.</param>
/// <param name="Status"><see cref="OcspStapleStatus.Good" />, <see cref="OcspStapleStatus.Revoked" /> or <see cref="OcspStapleStatus.Unknown" />.</param>
/// <param name="RevocationReason">The CRL reason of a revoked certificate, -1 when none was given; 0 otherwise.</param>
/// <param name="ThisUpdate">When the status was known to be correct.</param>
/// <param name="NextUpdate">When newer information will be available, or <see langword="null" /> when the responder did not say.</param>
internal sealed record OcspSingleResponse(
    string HashAlgorithmOid,
    byte[] IssuerNameHash,
    byte[] IssuerKeyHash,
    byte[] SerialNumber,
    OcspStapleStatus Status,
    int RevocationReason,
    DateTimeOffset ThisUpdate,
    DateTimeOffset? NextUpdate)
{
    private const int NoRevocationReason = -1;

    private static readonly Asn1Tag GoodTag = new(TagClass.ContextSpecific, 0);
    private static readonly Asn1Tag RevokedTag = new(TagClass.ContextSpecific, 1, true);
    private static readonly Asn1Tag UnknownTag = new(TagClass.ContextSpecific, 2);
    private static readonly Asn1Tag RevocationReasonTag = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag NextUpdateTag = new(TagClass.ContextSpecific, 0, true);

    /// <summary>Reads the next <c>SingleResponse</c> from <paramref name="responses" />.</summary>
    /// <param name="responses">The reader over the <c>responses</c> list.</param>
    /// <returns>The single response.</returns>
    /// <exception cref="AsnContentException">The next value is not a DER <c>SingleResponse</c>.</exception>
    public static OcspSingleResponse Read(AsnReader responses)
    {
        AsnReader single = responses.ReadSequence();
        AsnReader certId = single.ReadSequence();
        string hashAlgorithmOid = certId.ReadSequence().ReadObjectIdentifier();
        byte[] issuerNameHash = certId.ReadOctetString();
        byte[] issuerKeyHash = certId.ReadOctetString();
        byte[] serialNumber = certId.ReadIntegerBytes().ToArray();
        certId.ThrowIfNotEmpty();
        (OcspStapleStatus status, int reason) = ReadCertStatus(single);
        DateTimeOffset thisUpdate = single.ReadGeneralizedTime();
        DateTimeOffset? nextUpdate = single.HasData && single.PeekTag().HasSameClassAndValue(NextUpdateTag)
            ? single.ReadSequence(NextUpdateTag).ReadGeneralizedTime()
            : null;
        return new OcspSingleResponse(hashAlgorithmOid, issuerNameHash, issuerKeyHash, serialNumber, status, reason, thisUpdate, nextUpdate);
    }

    private static (OcspStapleStatus Status, int Reason) ReadCertStatus(AsnReader single)
    {
        Asn1Tag tag = single.PeekTag();
        if (tag.HasSameClassAndValue(GoodTag))
        {
            single.ReadNull(GoodTag);
            return (OcspStapleStatus.Good, 0);
        }

        if (tag.HasSameClassAndValue(UnknownTag))
        {
            single.ReadNull(UnknownTag);
            return (OcspStapleStatus.Unknown, 0);
        }

        AsnReader revoked = single.ReadSequence(RevokedTag);
        revoked.ReadGeneralizedTime();
        int reason = revoked.HasData ? OcspBasicResponse.ReadEnumerated(revoked.ReadSequence(RevocationReasonTag)) : NoRevocationReason;
        revoked.ThrowIfNotEmpty();
        return (OcspStapleStatus.Revoked, reason);
    }
}
