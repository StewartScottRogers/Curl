using System.Net;
using System.Net.Sockets;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// Dials each TCP connection to the TCP or TLS route on its port, starting the route's server on
/// the other end of an in-memory pair; a port no such route answers is refused, as a real one is.
/// </summary>
internal sealed class ReferenceTcpDialer(IReadOnlyList<ReferenceRoute> routes, ReferenceRun run) : ITcpDialer
{
    public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ReferenceRoute route = routes.FirstOrDefault(candidate => candidate.Port == endPoint.Port && candidate.Transport != ReferenceTransport.Udp)
            ?? throw new SocketException((int)SocketError.ConnectionRefused);
        (InMemoryDuplexStream clientEnd, InMemoryDuplexStream serverEnd) = InMemoryDuplexStream.CreatePair();
        WireExchange exchange = run.Open(route);
        run.Serve(ServeAsync(route, serverEnd, exchange, run.Stopping));
        ReferenceClientConnection connection = new(clientEnd, endPoint);
        return ValueTask.FromResult(new DialedTcpConnection(connection, (IPEndPoint)connection.LocalEndPoint!));
    }

    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        DialAsync(endPoint, cancellationToken);

    public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
        throw new SocketException((int)SocketError.ConnectionRefused);

    // Serves the connection, through a TLS server handshake on a TLS route, and closes the server's
    // end when done. It yields first so curl's dial returns before the handshake waits for its hello.
    private static async Task ServeAsync(ReferenceRoute route, InMemoryDuplexStream serverEnd, WireExchange exchange, CancellationToken cancellationToken)
    {
        await Task.Yield();
        await using InMemoryDuplexStream closing = serverEnd;
        if (route.Certificate is null)
        {
            await route.StreamServer!.ServeAsync(new ReferenceConnection(serverEnd, exchange), cancellationToken).ConfigureAwait(false);
            return;
        }

        TlsServerOptions options = new(route.Certificate, [], RequestClientCertificate: false);
        await using Stream tls = await TlsServerStream.AcceptAsync(serverEnd, options, TlsServerStream.PlatformServesTls13, cancellationToken).ConfigureAwait(false);
        await route.StreamServer!.ServeAsync(new ReferenceConnection(tls, exchange), cancellationToken).ConfigureAwait(false);
    }
}
