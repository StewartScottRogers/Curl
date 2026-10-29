namespace Curl.Http2;

/// <summary>
/// Thrown when the peer broke RFC 9113 in a way that ends the connection: a connection
/// error (RFC 9113 section 5.4.1). <see cref="Http2Connection" /> has already sent GOAWAY
/// with <see cref="ErrorCode" /> when it throws this.
/// </summary>
public sealed class Http2ProtocolException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Http2ProtocolException" /> class.
    /// </summary>
    /// <param name="errorCode">The error code the violation calls for.</param>
    /// <param name="detail">What the peer did.</param>
    public Http2ProtocolException(Http2ErrorCode errorCode, string detail)
        : base($"HTTP/2 {errorCode}: {detail}.")
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the error code the violation calls for.</summary>
    public Http2ErrorCode ErrorCode { get; }

    /// <summary>Gets the opaque debug data the GOAWAY sent for the violation carries; empty for most.</summary>
    public ReadOnlyMemory<byte> GoAwayDebugData { get; init; }

    /// <summary>
    /// Gets the frame nghttp2 reports as an invalid frame received - a PUSH_PROMISE on a
    /// connection with push disabled - or <see langword="null" /> for any other violation.
    /// </summary>
    public Http2Frame? InvalidFrame { get; init; }
}
