namespace Curl.Extension.EncryptedDns.Wire;

/// <summary>
/// The resource record types a lookup asks for: the TYPE and QTYPE values of RFC 1035 section 3.2.2 and
/// RFC 3596 section 2.1.
/// </summary>
public enum DnsRecordType
{
    /// <summary>A host's IPv4 address (RFC 1035 section 3.4.1).</summary>
    A = 1,

    /// <summary>A host's IPv6 address (RFC 3596 section 2.1).</summary>
    Aaaa = 28,
}
