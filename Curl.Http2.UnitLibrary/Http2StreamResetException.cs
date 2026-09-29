namespace Curl.Http2;

/// <summary>
/// Thrown when the peer ends a stream with RST_STREAM (RFC 9113 section 6.4). The
/// connection itself stays usable.
/// </summary>
public sealed class Http2StreamResetException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Http2StreamResetException" /> class.
    /// </summary>
    /// <param name="streamId">The stream the peer reset.</param>
    /// <param name="errorCode">The error code it gave.</param>
    public Http2StreamResetException(int streamId, Http2ErrorCode errorCode)
        : base($"HTTP/2 stream {streamId} was reset by the peer: {errorCode}.")
    {
        StreamId = streamId;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the stream the peer reset.</summary>
    public int StreamId { get; }

    /// <summary>Gets the error code the peer gave.</summary>
    public Http2ErrorCode ErrorCode { get; }
}
