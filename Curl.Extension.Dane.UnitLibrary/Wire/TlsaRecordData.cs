namespace Curl.Extension.Dane.Wire;

/// <summary>
/// TLSA RDATA (RFC 6698 section 2.1): which certificate to match, which part of it, how, and the data to match.
/// </summary>
/// <param name="CertificateUsage">The certificate usage field (section 2.1.1): 0 to 3, PKIX-TA, PKIX-EE, DANE-TA, DANE-EE.</param>
/// <param name="Selector">The selector field (section 2.1.2): 0 the full certificate, 1 its SubjectPublicKeyInfo.</param>
/// <param name="MatchingType">The matching type field (section 2.1.3): 0 exact, 1 SHA-256, 2 SHA-512.</param>
/// <param name="CertificateAssociationData">The certificate association data (section 2.1.4).</param>
public sealed record TlsaRecordData(byte CertificateUsage, byte Selector, byte MatchingType, byte[] CertificateAssociationData)
    : DnsRecordData;
