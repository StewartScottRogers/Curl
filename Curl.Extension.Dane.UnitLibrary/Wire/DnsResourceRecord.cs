namespace Curl.Extension.Dane.Wire;

/// <summary>
/// One resource record of a response (RFC 1035 section 4.1.3).
/// </summary>
/// <param name="Owner">The owner name, decompressed.</param>
/// <param name="Type">The TYPE field.</param>
/// <param name="Class">The CLASS field, 1 for IN.</param>
/// <param name="TimeToLive">The TTL field, in seconds.</param>
/// <param name="RawData">
/// The RDATA octets exactly as the message carried them, for canonical-form signing (RFC 4034 section 6.2). Of the
/// types decoded here only CNAME's may hold a compression pointer; <see cref="CnameRecordData" /> has its name
/// decompressed.
/// </param>
/// <param name="Data">The RDATA's fields for CNAME, TLSA, DNSKEY, RRSIG, DS, NSEC and NSEC3; null for any other type.</param>
public sealed record DnsResourceRecord(
    DnsName Owner,
    DnssecRecordType Type,
    ushort Class,
    uint TimeToLive,
    byte[] RawData,
    DnsRecordData? Data);
