namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>Gives a <see cref="DnsReferenceServer" />'s replies to one query curl sent.</summary>
/// <param name="query">The query.</param>
/// <param name="index">How many queries curl sent before it, over either transport, from 0.</param>
/// <returns>The replies to send, in order (each its own datagram, or its own length-prefixed message over TCP); none to stay silent.</returns>
public delegate IReadOnlyList<DnsReferenceReply> DnsReferenceResponder(DnsReferenceQuery query, int index);
