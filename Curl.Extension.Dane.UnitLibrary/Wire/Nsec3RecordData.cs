namespace Curl.Extension.Dane.Wire;

/// <summary>
/// NSEC3 RDATA (RFC 5155 section 3.2): the hash parameters, the next hashed owner name and the types the owner has.
/// </summary>
/// <param name="HashAlgorithm">The hash algorithm (section 3.1.1), 1 SHA-1.</param>
/// <param name="Flags">The flags field (section 3.1.2): bit 0 Opt-Out.</param>
/// <param name="Iterations">How many more times the hash is applied (section 3.1.3).</param>
/// <param name="Salt">The salt (section 3.1.5), empty when its length is 0.</param>
/// <param name="NextHashedOwnerName">The next hashed owner name, unencoded binary (section 3.1.7).</param>
/// <param name="Types">The types the type bit maps field (section 3.1.8) lists, in ascending order.</param>
public sealed record Nsec3RecordData(
    byte HashAlgorithm,
    byte Flags,
    ushort Iterations,
    byte[] Salt,
    byte[] NextHashedOwnerName,
    IReadOnlyList<DnssecRecordType> Types) : DnsRecordData;
