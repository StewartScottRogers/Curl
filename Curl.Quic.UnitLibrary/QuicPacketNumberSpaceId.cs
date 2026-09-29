namespace Curl.Quic;

/// <summary>
/// The three packet number spaces loss recovery keeps apart (RFC 9002 section A.2,
/// <c>kPacketNumberSpace</c>): each has its own sent packets, acknowledgements and timers.
/// </summary>
public enum QuicPacketNumberSpaceId
{
    /// <summary>Initial packets.</summary>
    Initial,

    /// <summary>Handshake packets.</summary>
    Handshake,

    /// <summary>0-RTT and 1-RTT packets.</summary>
    ApplicationData,
}
