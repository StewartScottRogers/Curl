namespace Curl.Extension.Protocol.DnsQuery.Wire;

/// <summary>One entry of a DNS message's question section (RFC 1035 section 4.1.2).</summary>
/// <param name="Name">The QNAME in presentation format, ending in a dot.</param>
/// <param name="Type">The QTYPE.</param>
/// <param name="Class">The QCLASS.</param>
internal sealed record DnsQuestion(string Name, ushort Type, ushort Class);
