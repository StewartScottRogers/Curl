using System.Net;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A connection <see cref="SocketCallbackConnector" /> opened, carrying the opaque <see cref="CurlSocket" /> the
/// socket callbacks saw for it. It reads, writes and answers as <paramref name="inner" /> does; disposing it
/// closes <paramref name="inner" /> and then calls <c>CURLOPT_CLOSESOCKETFUNCTION</c> with the socket, once,
/// as libcurl's <c>Curl_socket_close</c> calls it in place of <c>close()</c> (ADR-0610), or first, while it is
/// open, when the socket is the engine's own operating system socket (CGR-129, ADR-0842).
/// </summary>
/// <param name="inner">The connection opened.</param>
/// <param name="closer">Calls the close-socket callback once for the socket, shared with any connection <see cref="ClearTlsAsync" /> gives.</param>
internal sealed class SocketCallbackConnection(IConnection inner, CurlSocketCloser closer) : IConnection
{
    /// <summary>Gets the socket the socket callbacks saw for this connection.</summary>
    internal CurlSocket Socket => closer.Socket;

    /// <inheritdoc />
    public bool IsSecure => inner.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => inner.LocalEndPoint;

    /// <inheritdoc />
    public nint? SocketHandle => inner.SocketHandle;

    /// <inheritdoc />
    public IConnectionSession? Session => inner.Session;

    /// <inheritdoc />
    public bool IsSharedWithAnotherTransfer => inner.IsSharedWithAnotherTransfer;

    /// <inheritdoc />
    public bool HasPeerClosed => inner.HasPeerClosed;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public void MarkReusable() => inner.MarkReusable();

    /// <inheritdoc />
    public bool TryHoldSession(IConnectionSession session) => inner.TryHoldSession(session);

    /// <summary>
    /// Ends TLS as the inner connection does; the plain connection it gives is the same socket, so it is
    /// wrapped again with the same <see cref="CurlSocketCloser" />.
    /// </summary>
    /// <param name="sendCloseNotifyFirst">Whether to send TLS close_notify first.</param>
    /// <param name="cancellationToken">Cancels the shutdown.</param>
    /// <returns>The plain connection, or <see langword="null" /> when the inner connection gives none.</returns>
    public async ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken) =>
        await inner.ClearTlsAsync(sendCloseNotifyFirst, cancellationToken).ConfigureAwait(false) is { } plain
            ? new SocketCallbackConnection(plain, closer)
            : null;

    /// <summary>
    /// Closes the inner connection, then calls the close-socket callback once; the callback runs first when the
    /// socket is the engine's own operating system socket, so it sees the socket open (CGR-129, ADR-0842).
    /// </summary>
    /// <returns>A task that completes once both are done.</returns>
    public async ValueTask DisposeAsync()
    {
        if (closer.IsTheConnectionsOwnSocket)
        {
            closer.Close();
        }

        await inner.DisposeAsync().ConfigureAwait(false);
        closer.Close();
    }
}
