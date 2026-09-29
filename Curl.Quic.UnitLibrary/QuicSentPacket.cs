namespace Curl.Quic;

/// <summary>
/// One packet as loss recovery remembers it once sent (RFC 9002 section A.1.1): its number,
/// when it went, whether it asks for an acknowledgement and counts towards bytes in flight,
/// its size, the frames it carried so that what they held can be sent again if it is lost,
/// and whether the sender had run out of data to send when it went.
/// </summary>
/// <param name="PacketNumber">The full packet number.</param>
/// <param name="TimeSent">When it was sent, on the connection's clock.</param>
/// <param name="IsAckEliciting">Whether it carries a frame other than ACK, PADDING or CONNECTION_CLOSE.</param>
/// <param name="IsInFlight">Whether it counts towards bytes in flight: ack-eliciting or padded (RFC 9002 section 2).</param>
/// <param name="SentBytes">Its size on the wire, header and tag included.</param>
/// <param name="Frames">The frames it carried.</param>
/// <param name="IsApplicationLimited">Whether it went while the application or flow control, not the congestion window, limited sending, so its acknowledgement does not grow the window (RFC 9002 section 7.8).</param>
public sealed record QuicSentPacket(ulong PacketNumber, TimeSpan TimeSent, bool IsAckEliciting, bool IsInFlight, int SentBytes, IReadOnlyList<QuicFrame> Frames, bool IsApplicationLimited = false);
