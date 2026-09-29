namespace Curl.Quic;

/// <summary>
/// A packet read from the front of a datagram, with where its parts lie in it.
/// </summary>
/// <param name="Packet">The packet.</param>
/// <param name="HeaderLength">The bytes before the payload, the packet number included: the associated data packet protection authenticates (RFC 9001 section 5.3). For Retry and Version Negotiation packets, which have no payload, the whole packet.</param>
/// <param name="Length">The bytes the packet takes. A long-header packet other than Retry and Version Negotiation may be followed by another in the same datagram (RFC 9000 section 12.2).</param>
public sealed record QuicDecodedPacket(QuicPacket Packet, int HeaderLength, int Length);
