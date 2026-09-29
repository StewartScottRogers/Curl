namespace Curl.Quic;

/// <summary>
/// The frame types of RFC 9000 section 19 (table 3 in section 12.4). STREAM frames use the
/// eight types from <see cref="Stream" /> to 0x0f, their low three bits being the OFF, LEN
/// and FIN flags.
/// </summary>
public enum QuicFrameType : ulong
{
    /// <summary>PADDING (section 19.1).</summary>
    Padding = 0x00,

    /// <summary>PING (section 19.2).</summary>
    Ping = 0x01,

    /// <summary>ACK without ECN counts (section 19.3).</summary>
    Ack = 0x02,

    /// <summary>ACK with ECN counts (section 19.3).</summary>
    AckWithEcnCounts = 0x03,

    /// <summary>RESET_STREAM (section 19.4).</summary>
    ResetStream = 0x04,

    /// <summary>STOP_SENDING (section 19.5).</summary>
    StopSending = 0x05,

    /// <summary>CRYPTO (section 19.6).</summary>
    Crypto = 0x06,

    /// <summary>NEW_TOKEN (section 19.7).</summary>
    NewToken = 0x07,

    /// <summary>The first STREAM type, with no flags set (section 19.8).</summary>
    Stream = 0x08,

    /// <summary>MAX_DATA (section 19.9).</summary>
    MaxData = 0x10,

    /// <summary>MAX_STREAM_DATA (section 19.10).</summary>
    MaxStreamData = 0x11,

    /// <summary>MAX_STREAMS for bidirectional streams (section 19.11).</summary>
    MaxStreamsBidirectional = 0x12,

    /// <summary>MAX_STREAMS for unidirectional streams (section 19.11).</summary>
    MaxStreamsUnidirectional = 0x13,

    /// <summary>DATA_BLOCKED (section 19.12).</summary>
    DataBlocked = 0x14,

    /// <summary>STREAM_DATA_BLOCKED (section 19.13).</summary>
    StreamDataBlocked = 0x15,

    /// <summary>STREAMS_BLOCKED for bidirectional streams (section 19.14).</summary>
    StreamsBlockedBidirectional = 0x16,

    /// <summary>STREAMS_BLOCKED for unidirectional streams (section 19.14).</summary>
    StreamsBlockedUnidirectional = 0x17,

    /// <summary>NEW_CONNECTION_ID (section 19.15).</summary>
    NewConnectionId = 0x18,

    /// <summary>RETIRE_CONNECTION_ID (section 19.16).</summary>
    RetireConnectionId = 0x19,

    /// <summary>PATH_CHALLENGE (section 19.17).</summary>
    PathChallenge = 0x1a,

    /// <summary>PATH_RESPONSE (section 19.18).</summary>
    PathResponse = 0x1b,

    /// <summary>CONNECTION_CLOSE carrying a transport error (section 19.19).</summary>
    ConnectionClose = 0x1c,

    /// <summary>CONNECTION_CLOSE carrying an application error (section 19.19).</summary>
    ConnectionCloseApplication = 0x1d,

    /// <summary>HANDSHAKE_DONE (section 19.20).</summary>
    HandshakeDone = 0x1e,
}
