namespace Curl.Extension.Dane.Wire;

/// <summary>
/// The DNS record types DANE and DNSSEC work with, by their IANA TYPE values. A query may name any other value by a
/// cast, and a decoded record keeps whatever value its TYPE field holds.
/// </summary>
public enum DnssecRecordType : ushort
{
    /// <summary>An IPv4 address (RFC 1035 section 3.4.1).</summary>
    A = 1,

    /// <summary>The canonical name of an alias (RFC 1035 section 3.3.1).</summary>
    Cname = 5,

    /// <summary>An IPv6 address (RFC 3596 section 2.1).</summary>
    Aaaa = 28,

    /// <summary>The EDNS0 pseudo-record (RFC 6891 section 6.1.1).</summary>
    Opt = 41,

    /// <summary>A delegation signer (RFC 4034 section 5).</summary>
    Ds = 43,

    /// <summary>A resource record set's signature (RFC 4034 section 3).</summary>
    Rrsig = 46,

    /// <summary>The next secure name, for authenticated denial (RFC 4034 section 4).</summary>
    Nsec = 47,

    /// <summary>A zone's public key (RFC 4034 section 2).</summary>
    Dnskey = 48,

    /// <summary>The next hashed owner name, for authenticated denial (RFC 5155 section 3).</summary>
    Nsec3 = 50,

    /// <summary>A TLS certificate association (RFC 6698 section 2).</summary>
    Tlsa = 52,
}
