namespace Curl.Networking;

/// <summary>
/// The DNS record types curl's DNS-over-HTTPS resolver asks for or follows (RFC 1035,
/// RFC 3596), and the SRV type <see cref="DnsServerResolver" /> asks for (RFC 2782), with
/// their wire values.
/// </summary>
public enum DnsRecordType : ushort
{
    /// <summary>An IPv4 address record.</summary>
    A = 1,

    /// <summary>A canonical-name record: the owner name is an alias of another name.</summary>
    Cname = 5,

    /// <summary>An IPv6 address record.</summary>
    Aaaa = 28,

    /// <summary>A service-location record: priority, weight, port and target host (RFC 2782).</summary>
    Srv = 33,
}
