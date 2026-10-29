using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITcpDialer" /> that asks a connect's <see cref="ITcpSocketOpener" /> before each
/// attempt how its socket opens, as curl 8.22.0's <c>cf_socket_open</c> calls
/// <c>CURLOPT_OPENSOCKETFUNCTION</c> before the connect, and tells it when the attempt's socket closes
/// because the connect failed (ADR-0903, CGR-036).
/// </summary>
/// <remarks>
/// A refused attempt fails with a <see cref="SocketException" /> and no socket, so with every address
/// refused the race ends with exit 7. A supplied handle is adopted without ownership and connected
/// unless it already is.
/// </remarks>
/// <param name="inner">Opens and connects the socket when the opener proceeds.</param>
/// <param name="opener">Decides how each attempt's socket opens.</param>
internal sealed class SocketOpenerTcpDialer(ITcpDialer inner, ITcpSocketOpener opener) : ITcpDialer
{
    /// <inheritdoc />
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        var decision = opener.Open(endPoint);
        if (decision.IsRefused)
        {
            throw new SocketException((int)SocketError.SocketError);
        }

        try
        {
            return decision.SuppliedHandle is { } handle
                ? await TcpDialer.DialSuppliedSocketAsync(handle, decision.AlreadyConnected, endPoint, cancellationToken).ConfigureAwait(false)
                : await inner.DialAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            opener.SocketClosed(endPoint);
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        inner.DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
        inner.DialUnixSocketAsync(address, cancellationToken);

    /// <inheritdoc />
    public SocketException? FailureToOpenSocket(AddressFamily family) => inner.FailureToOpenSocket(family);
}
