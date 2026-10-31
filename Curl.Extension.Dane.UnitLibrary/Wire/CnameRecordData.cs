namespace Curl.Extension.Dane.Wire;

/// <summary>
/// CNAME RDATA (RFC 1035 section 3.3.1): the canonical name the owner is an alias of, decompressed.
/// </summary>
/// <param name="CanonicalName">The name the owner is an alias of.</param>
public sealed record CnameRecordData(DnsName CanonicalName) : DnsRecordData;
