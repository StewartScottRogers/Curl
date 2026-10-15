using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// Builds the stand-in for upstream's <c>http/2</c> test server - nghttpx serving h2c in front of
/// <c>sws</c> (ADR-0491) - so the case runner puts it on <c>%HTTP2PORT</c> in one line (BL-2230).
/// </summary>
public static class UpstreamHttp2ServerFactory
{
    /// <summary>
    /// Creates an h2c server that relays each HTTP/2 stream to <paramref name="backend"/> as an
    /// HTTP/1.1 request on a new connection.
    /// </summary>
    /// <param name="backend">The HTTP/1.1 server, the case's sws emulation.</param>
    /// <returns>The server, as a connector whose every connection is served in memory.</returns>
    public static IConnector Create(IConnector backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        return new Http2ServerConnector(backend);
    }
}
