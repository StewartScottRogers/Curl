using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// What the case runner needs of the stand-in for upstream's <c>http/2</c> server, nghttpx serving
/// h2c in front of <c>sws</c>, which lives in <c>Curl.Conformance.Http2Server.UnitLibrary</c> because
/// it reuses the HTTP/2 frame codec and HPACK this library does not reference (ADR-0491): the caller
/// builds it from there and hands it to <see cref="UpstreamCaseRunner"/> (BL-2230), as it does
/// <see cref="UpstreamSshServer"/>.
/// </summary>
/// <param name="CreateServer">
/// Makes a fresh server for one case in front of the given backend, the case's sws emulation, to
/// which it relays each HTTP/2 stream as HTTP/1.1; connections to <see cref="Http2Port"/> reach it.
/// </param>
public sealed record UpstreamHttp2Server(Func<IConnector, IConnector> CreateServer)
{
    /// <summary>The value of <c>%HTTP2PORT</c>.</summary>
    public const int Http2Port = 9009;

    /// <summary>
    /// Puts a fresh server on <see cref="Http2Port"/>, relaying to <paramref name="backend"/>, in front
    /// of the case's other servers.
    /// </summary>
    /// <param name="otherPorts">Where every connection to another port goes.</param>
    /// <param name="backend">The HTTP/1.1 server the stand-in relays to, the case's sws emulation.</param>
    /// <returns>A connector routing by port.</returns>
    public IConnector InFrontOf(IConnector otherPorts, IConnector backend) => new Http2PortConnector(CreateServer(backend), otherPorts);

    private sealed class Http2PortConnector(IConnector http2Server, IConnector otherPorts) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            (target.Port == Http2Port ? http2Server : otherPorts).ConnectAsync(target, cancellationToken);
    }
}
