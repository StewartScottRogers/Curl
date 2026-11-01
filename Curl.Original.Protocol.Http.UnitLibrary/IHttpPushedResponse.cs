namespace Curl.Original.Protocol.Http;

/// <summary>
/// The response a server pushed on an HTTP/2 stream it promised (BL-2516), offered to an
/// <see cref="IHttp2PushSink" />.
/// </summary>
public interface IHttpPushedResponse
{
    /// <summary>Gets the stream the server promised for the push: an even identifier.</summary>
    int StreamId { get; }

    /// <summary>
    /// Gets the pushed response as curl's HTTP/2 layer writes a response: <c>HTTP/2 200 \r\n</c>,
    /// the headers as received, the empty line, then the DATA, ending once the server ends the
    /// stream. It is read asynchronously only and cannot be written.
    /// </summary>
    Stream Content { get; }
}
