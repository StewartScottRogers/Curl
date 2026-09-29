namespace Curl.Quic;

/// <summary>
/// A QUIC transport error code (RFC 9000 section 20.1), carried in a CONNECTION_CLOSE
/// frame of type 0x1c. Codes 0x0100 to 0x01ff are TLS alerts (<c>CRYPTO_ERROR</c>): add
/// the alert number to <see cref="CryptoErrorBase" />.
/// </summary>
public enum QuicTransportErrorCode : ulong
{
    /// <summary>The connection is closing without an error.</summary>
    NoError = 0x00,

    /// <summary>The endpoint met an internal error and cannot continue.</summary>
    InternalError = 0x01,

    /// <summary>The server refused the connection.</summary>
    ConnectionRefused = 0x02,

    /// <summary>The peer sent more data than its flow control limits allowed.</summary>
    FlowControlError = 0x03,

    /// <summary>The peer opened more streams than it was allowed.</summary>
    StreamLimitError = 0x04,

    /// <summary>A frame arrived for a stream in a state that does not permit it.</summary>
    StreamStateError = 0x05,

    /// <summary>A stream's final size was violated.</summary>
    FinalSizeError = 0x06,

    /// <summary>A frame was badly formatted: an unknown type, or a field out of range or too short.</summary>
    FrameEncodingError = 0x07,

    /// <summary>The transport parameters were malformed or invalid.</summary>
    TransportParameterError = 0x08,

    /// <summary>The peer supplied more connection IDs than advertised.</summary>
    ConnectionIdLimitError = 0x09,

    /// <summary>The peer broke a protocol rule not covered by a more specific code.</summary>
    ProtocolViolation = 0x0a,

    /// <summary>The server received a client Initial with an invalid token.</summary>
    InvalidToken = 0x0b,

    /// <summary>The application or application protocol caused the close.</summary>
    ApplicationError = 0x0c,

    /// <summary>More data was received in CRYPTO frames than can be buffered.</summary>
    CryptoBufferExceeded = 0x0d,

    /// <summary>A key update was performed wrongly.</summary>
    KeyUpdateError = 0x0e,

    /// <summary>The AEAD confidentiality or integrity limit was reached.</summary>
    AeadLimitReached = 0x0f,

    /// <summary>No network path can carry the connection.</summary>
    NoViablePath = 0x10,

    /// <summary>Version negotiation failed: the peer's <c>version_information</c> chose another version (RFC 9368 section 10.2).</summary>
    VersionNegotiationError = 0x11,

    /// <summary>The first <c>CRYPTO_ERROR</c> code; a TLS alert <c>n</c> is this plus <c>n</c>.</summary>
    CryptoErrorBase = 0x0100,
}
