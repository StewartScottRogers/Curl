namespace Curl.Quic;

/// <summary>
/// The kinds of QUIC version 1 packet (RFC 9000 section 17). The first four values are the
/// long header's two Long Packet Type bits (table 5 in section 17.2).
/// </summary>
public enum QuicPacketType
{
    /// <summary>An Initial packet (section 17.2.2), long header type 0x00.</summary>
    Initial = 0,

    /// <summary>A 0-RTT packet (section 17.2.3), long header type 0x01.</summary>
    ZeroRtt = 1,

    /// <summary>A Handshake packet (section 17.2.4), long header type 0x02.</summary>
    Handshake = 2,

    /// <summary>A Retry packet (section 17.2.5), long header type 0x03.</summary>
    Retry = 3,

    /// <summary>A Version Negotiation packet (section 17.2.1): a long header with version 0.</summary>
    VersionNegotiation = 4,

    /// <summary>A 1-RTT packet (section 17.3.1): the short header.</summary>
    OneRtt = 5,
}
