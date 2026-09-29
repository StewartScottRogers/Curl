namespace Curl.Quic;

/// <summary>
/// A packet read from the front of a datagram with its protection removed, or the reason
/// it was dropped.
/// </summary>
/// <param name="Status">Whether the packet was unprotected or dropped, and why.</param>
/// <param name="Packet">The packet with its header in the clear and its payload the plaintext frames, tag removed; <see langword="null" /> when dropped.</param>
/// <param name="PacketNumber">The full packet number; 0 when dropped.</param>
/// <param name="Length">The bytes the packet took in the datagram, dropped or not, so the packets coalesced after it can still be read.</param>
/// <param name="KeyPhaseChanged">Whether the packet carried the next key phase and moved this direction to it (RFC 9001 section 6.2), so the sending direction must update too.</param>
public sealed record QuicUnprotectResult(QuicUnprotectStatus Status, QuicPacket? Packet, ulong PacketNumber, int Length, bool KeyPhaseChanged);
