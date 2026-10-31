namespace Curl.Extension.Dane.Wire;

/// <summary>
/// DS RDATA (RFC 4034 section 5.1): a parent zone's digest of a child zone's DNSKEY.
/// </summary>
/// <param name="KeyTag">The key tag of the DNSKEY digested (section 5.1.1).</param>
/// <param name="Algorithm">That DNSKEY's algorithm number (section 5.1.2).</param>
/// <param name="DigestType">The digest algorithm (section 5.1.3), such as 2 SHA-256.</param>
/// <param name="Digest">The digest (section 5.1.4).</param>
public sealed record DsRecordData(ushort KeyTag, byte Algorithm, byte DigestType, byte[] Digest) : DnsRecordData;
