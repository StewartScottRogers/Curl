namespace Curl.Http2;

/// <summary>
/// Thrown when the peer sends GOAWAY with an error code, or while a stream it will not
/// process is still open (RFC 9113 section 6.8).
/// </summary>
public sealed class Http2GoAwayException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Http2GoAwayException" /> class.
    /// </summary>
    /// <param name="goAway">The GOAWAY the peer sent.</param>
    public Http2GoAwayException(Http2GoAwayPayload goAway)
        : base($"HTTP/2 GOAWAY from the peer: {goAway.ErrorCode}, last stream {goAway.LastStreamId}.")
    {
        GoAway = goAway;
    }

    /// <summary>Gets the GOAWAY the peer sent.</summary>
    public Http2GoAwayPayload GoAway { get; }
}
