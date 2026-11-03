using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>A query a <see cref="DnsReferenceServer" /> received.</summary>
/// <param name="Transport">How it came: <see cref="ReferenceTransport.Udp" /> as one datagram, <see cref="ReferenceTransport.Tcp" /> after its two-octet length prefix.</param>
/// <param name="Query">The decoded query, with its bytes.</param>
public sealed record DnsReceivedQuery(ReferenceTransport Transport, DnsReferenceQuery Query);
