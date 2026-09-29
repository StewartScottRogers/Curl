namespace Curl.Quic;

/// <summary>
/// What one ACK frame did to loss recovery: the packets it newly acknowledged and the
/// packets that, as a result, are declared lost and whose frames' data must go again.
/// </summary>
/// <param name="Acknowledged">The newly acknowledged packets.</param>
/// <param name="Lost">The packets declared lost.</param>
public sealed record QuicAcknowledgementOutcome(IReadOnlyList<QuicSentPacket> Acknowledged, IReadOnlyList<QuicSentPacket> Lost);
