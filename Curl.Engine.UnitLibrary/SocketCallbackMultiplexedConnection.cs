using System.Net;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A QUIC connection <see cref="SocketCallbackConnector" /> opened, carrying the opaque <see cref="CurlSocket" /> the
/// socket callbacks saw for its UDP socket. It opens streams and answers as <paramref name="inner" /> does; disposing
/// it closes <paramref name="inner" /> and then calls <c>CURLOPT_CLOSESOCKETFUNCTION</c> with the socket, once, as
/// libcurl's <c>Curl_socket_close</c> does for <c>cf-ngtcp2</c>'s socket (ADR-0610, BL-2474).
/// </summary>
/// <param name="inner">The QUIC connection opened.</param>
/// <param name="closer">Calls the close-socket callback once for the socket.</param>
internal sealed class SocketCallbackMultiplexedConnection(IMultiplexedConnection inner, CurlSocketCloser closer) : IMultiplexedConnection
{
    /// <summary>Gets the socket the socket callbacks saw for this connection.</summary>
    internal CurlSocket Socket => closer.Socket;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => inner.LocalEndPoint;

    /// <inheritdoc />
    public string ApplicationProtocol => inner.ApplicationProtocol;

    /// <inheritdoc />
    public long? BidirectionalStreamLimit => inner.BidirectionalStreamLimit;

    /// <inheritdoc />
    public TimeSpan? PeerIdleTimeout => inner.PeerIdleTimeout;

    /// <inheritdoc />
    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) => inner.OpenBidirectionalStreamAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) => inner.OpenUnidirectionalStreamAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) => inner.AcceptUnidirectionalStreamAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken) => inner.CloseAsync(applicationErrorCode, cancellationToken);

    /// <summary>Closes the inner connection, then calls the close-socket callback once.</summary>
    /// <returns>A task that completes once both are done.</returns>
    public async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        closer.Close();
    }
}
