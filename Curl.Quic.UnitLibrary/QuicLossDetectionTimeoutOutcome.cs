namespace Curl.Quic;

/// <summary>
/// What the loss detection timer did when it fired (RFC 9002 section A.9): declared packets
/// lost by the time threshold, or asked for a probe packet in a space.
/// </summary>
/// <param name="Space">The space the lost packets were in, or the space to probe.</param>
/// <param name="Lost">The packets declared lost; empty when the timer was the probe timeout.</param>
/// <param name="ProbeRequired">Whether one ack-eliciting probe packet must go in <paramref name="Space" />, outside the congestion window.</param>
public sealed record QuicLossDetectionTimeoutOutcome(QuicPacketNumberSpaceId Space, IReadOnlyList<QuicSentPacket> Lost, bool ProbeRequired);
