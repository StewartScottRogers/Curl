namespace Curl.Http2;

/// <summary>
/// The frame types of RFC 9113 section 6, by their wire value. A frame whose type is not
/// one of these keeps its raw value and is ignored (RFC 9113 section 5.5).
/// </summary>
public enum Http2FrameType : byte
{
    /// <summary>DATA (RFC 9113 section 6.1): a stream's body bytes.</summary>
    Data = 0x0,

    /// <summary>HEADERS (RFC 9113 section 6.2): opens a stream and carries a header block fragment.</summary>
    Headers = 0x1,

    /// <summary>PRIORITY (RFC 9113 section 6.3): deprecated stream priority, validated and ignored.</summary>
    Priority = 0x2,

    /// <summary>RST_STREAM (RFC 9113 section 6.4): ends one stream with an error code.</summary>
    RstStream = 0x3,

    /// <summary>SETTINGS (RFC 9113 section 6.5): configuration for the connection.</summary>
    Settings = 0x4,

    /// <summary>PUSH_PROMISE (RFC 9113 section 6.6): server push, refused by a client that sent ENABLE_PUSH 0.</summary>
    PushPromise = 0x5,

    /// <summary>PING (RFC 9113 section 6.7): eight opaque bytes, echoed back with ACK.</summary>
    Ping = 0x6,

    /// <summary>GOAWAY (RFC 9113 section 6.8): the sender is shutting the connection down.</summary>
    GoAway = 0x7,

    /// <summary>WINDOW_UPDATE (RFC 9113 section 6.9): grows a flow-control window.</summary>
    WindowUpdate = 0x8,

    /// <summary>CONTINUATION (RFC 9113 section 6.10): the rest of a header block.</summary>
    Continuation = 0x9,
}
