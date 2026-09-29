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
}
