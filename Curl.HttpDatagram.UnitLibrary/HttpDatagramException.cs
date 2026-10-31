namespace Curl.HttpDatagram;

/// <summary>
/// Thrown when a peer breaks RFC 9297: a datagram, a capsule or a setting it sent is not
/// allowed. <see cref="ErrorCode" /> says which error to close with.
/// </summary>
public sealed class HttpDatagramException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpDatagramException" /> class.
    /// </summary>
    /// <param name="errorCode">The error the input calls for.</param>
    /// <param name="reason">Which rule the input broke.</param>
    public HttpDatagramException(HttpDatagramErrorCode errorCode, string reason)
        : base($"HTTP Datagram {errorCode}: {reason}.")
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the error the input calls for.
    /// </summary>
    public HttpDatagramErrorCode ErrorCode { get; }
}
