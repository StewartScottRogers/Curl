namespace Curl.Extension.Dane.Wire;

/// <summary>
/// RRSIG RDATA (RFC 4034 section 3.1): one resource record set's signature.
/// </summary>
/// <param name="TypeCovered">The type of the record set signed (section 3.1.1).</param>
/// <param name="Algorithm">The algorithm number (section 3.1.2).</param>
/// <param name="Labels">The owner name's label count, without a leading wildcard (section 3.1.3).</param>
/// <param name="OriginalTtl">The TTL the record set has in its zone (section 3.1.4).</param>
/// <param name="SignatureExpiration">When the signature expires, in seconds since 1970 modulo 2^32 (section 3.1.5).</param>
/// <param name="SignatureInception">When the signature starts, in seconds since 1970 modulo 2^32 (section 3.1.5).</param>
/// <param name="KeyTag">The key tag of the DNSKEY that signed it (section 3.1.6).</param>
/// <param name="SignerName">The zone that signed it, never compressed (section 3.1.7).</param>
/// <param name="Signature">The signature (section 3.1.8).</param>
public sealed record RrsigRecordData(
    DnssecRecordType TypeCovered,
    byte Algorithm,
    byte Labels,
    uint OriginalTtl,
    uint SignatureExpiration,
    uint SignatureInception,
    ushort KeyTag,
    DnsName SignerName,
    byte[] Signature) : DnsRecordData;
