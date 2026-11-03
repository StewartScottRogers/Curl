using Curl.Conformance.Extended.DnsQuery.Reference;

namespace Curl.Conformance.Extended.DnsQuery.Encrypted;

/// <summary>One DNS query a <see cref="DoqReferenceServer" /> read from a client stream up to its FIN.</summary>
/// <param name="StreamId">The QUIC stream the query arrived on.</param>
/// <param name="StreamBytes">Every octet the client sent on the stream: the two-octet length prefix and the DNS message.</param>
/// <param name="Query">The DNS message behind the length prefix, decoded.</param>
public sealed record DoqReceivedQuery(ulong StreamId, byte[] StreamBytes, DnsReferenceQuery Query);
