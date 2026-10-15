using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// An <see cref="IDatagramConnector" /> whose every open is a fresh HTTP/3 stand-in - a new
/// <see cref="Http3ServerConnection" /> and <see cref="Http3ServerSession" /> - relaying to
/// <paramref name="backend" />, whatever the port; routing a port to it is the caller's. No
/// socket is opened.
/// </summary>
/// <param name="backend">The HTTP/1.1 server each request is relayed to, the case's sws emulation.</param>
/// <param name="tlsSettings">The certificate, key and preferences of each connection's TLS handshake.</param>
/// <param name="random">The source of each connection's connection ID, server random and key share.</param>
internal sealed class Http3ServerConnector(IConnector backend, Http3ServerTlsSettings tlsSettings, ITlsRandomSource random) : IDatagramConnector
{
    /// <summary>
    /// Opens a channel to a new stand-in; its backend connections go to <paramref name="host" />
    /// at <paramref name="port" />.
    /// </summary>
    /// <param name="host">The host from the URL; an IP literal is the server's address, any other name loopback.</param>
    /// <param name="port">The UDP port.</param>
    /// <param name="cancellationToken">Not used: the open completes at once.</param>
    /// <returns>The open channel.</returns>
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        IPAddress address = IPAddress.TryParse(host, out IPAddress? parsed) ? parsed : IPAddress.Loopback;
        var session = new Http3ServerSession(new Http3ServerConnection(tlsSettings, random), backend, new ConnectTarget(host, port, UseTls: false));
        return ValueTask.FromResult(DatagramOpenResult.Opened(new Http3ServerChannel(session, new IPEndPoint(address, port))));
    }
}
