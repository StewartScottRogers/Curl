namespace Curl.Http2;

/// <summary>
/// The error codes of RFC 9113 section 7, carried by RST_STREAM and GOAWAY. A peer may send
/// a value not listed here; it keeps its raw number.
/// </summary>
public enum Http2ErrorCode : uint
{
    /// <summary>NO_ERROR: a graceful shutdown.</summary>
    NoError = 0x0,

    /// <summary>PROTOCOL_ERROR: an unspecific protocol violation.</summary>
    ProtocolError = 0x1,

    /// <summary>INTERNAL_ERROR: the endpoint failed internally.</summary>
    InternalError = 0x2,

    /// <summary>FLOW_CONTROL_ERROR: a flow-control window was overrun or overflowed.</summary>
    FlowControlError = 0x3,

    /// <summary>SETTINGS_TIMEOUT: SETTINGS went unacknowledged too long.</summary>
    SettingsTimeout = 0x4,

    /// <summary>STREAM_CLOSED: a frame arrived on a stream the sender had already ended.</summary>
    StreamClosed = 0x5,

    /// <summary>FRAME_SIZE_ERROR: a frame had an invalid size.</summary>
    FrameSizeError = 0x6,

    /// <summary>REFUSED_STREAM: the stream was refused before any processing.</summary>
    RefusedStream = 0x7,

    /// <summary>CANCEL: the stream is no longer needed.</summary>
    Cancel = 0x8,

    /// <summary>COMPRESSION_ERROR: the header compression context cannot be kept.</summary>
    CompressionError = 0x9,

    /// <summary>CONNECT_ERROR: a CONNECT tunnel's connection failed.</summary>
    ConnectError = 0xa,

    /// <summary>ENHANCE_YOUR_CALM: the peer is generating excessive load.</summary>
    EnhanceYourCalm = 0xb,

    /// <summary>INADEQUATE_SECURITY: the transport's security properties are insufficient.</summary>
    InadequateSecurity = 0xc,

    /// <summary>HTTP_1_1_REQUIRED: the peer wants HTTP/1.1 for this request.</summary>
    Http11Required = 0xd,
}
