namespace Curl.Extension.Dane.Wire;

/// <summary>
/// NSEC RDATA (RFC 4034 section 4.1): the next owner name in the zone and the types the owner has.
/// </summary>
/// <param name="NextDomainName">The next owner name in canonical order, never compressed (section 4.1.1).</param>
/// <param name="Types">The types the type bit maps field (section 4.1.2) lists, in ascending order.</param>
public sealed record NsecRecordData(DnsName NextDomainName, IReadOnlyList<DnssecRecordType> Types) : DnsRecordData;
