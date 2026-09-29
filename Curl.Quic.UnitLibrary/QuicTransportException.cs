namespace Curl.Quic;

/// <summary>
/// Thrown when bytes received from the peer break a rule of RFC 9000. <see cref="ErrorCode" />
/// is the transport error the connection closes with: <see cref="QuicTransportErrorCode.FrameEncodingError" />
/// for a malformed frame, <see cref="QuicTransportErrorCode.ProtocolViolation" /> for a malformed
/// packet or a frame the packet may not carry.
/// </summary>
public sealed class QuicTransportException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuicTransportException" /> class.
    /// </summary>
    /// <param name="errorCode">The transport error the connection closes with.</param>
    /// <param name="reason">What in the input broke the rule.</param>
    public QuicTransportException(QuicTransportErrorCode errorCode, string reason)
        : base($"QUIC {errorCode}: {reason}")
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the transport error the connection closes with.
    /// </summary>
    public QuicTransportErrorCode ErrorCode { get; }
}
