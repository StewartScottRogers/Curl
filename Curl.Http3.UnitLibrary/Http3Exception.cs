namespace Curl.Http3;

/// <summary>
/// Thrown when an HTTP/3 peer breaks RFC 9114: a frame, a stream or a setting it sent is not
/// allowed. <see cref="ErrorCode" /> is the connection error to close the connection with.
/// </summary>
public sealed class Http3Exception : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Http3Exception" /> class.
    /// </summary>
    /// <param name="errorCode">The connection error the input calls for.</param>
    /// <param name="reason">Which rule the input broke.</param>
    public Http3Exception(Http3ErrorCode errorCode, string reason)
        : base($"HTTP/3 {errorCode}: {reason}.")
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the connection error the input calls for.
    /// </summary>
    public Http3ErrorCode ErrorCode { get; }
}
