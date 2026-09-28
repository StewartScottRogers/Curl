using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="ITcpDialer" />: connects a TCP <see cref="Socket" /> with the
/// <see cref="TcpSocketOptions" /> it was created with and returns it as a
/// <see cref="StreamConnection" /> over a <see cref="NetworkStream" /> that owns the socket,
/// with the local end point the socket was bound to.
/// </summary>
/// <param name="socketOptions">The options set on every socket before it connects.</param>
public sealed class TcpDialer(TcpSocketOptions socketOptions) : ITcpDialer
{
    /// <summary>
    /// Creates a dialer with curl's default socket options: <c>TCP_NODELAY</c> and
    /// <c>SO_KEEPALIVE</c> both on.
    /// </summary>
    public TcpDialer()
        : this(new TcpSocketOptions())
    {
    }

    /// <summary>Gets the options set on every socket before it connects.</summary>
    public TcpSocketOptions SocketOptions { get; } = socketOptions ?? throw new ArgumentNullException(nameof(socketOptions));

    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083: every line after the argument check needs a
    /// connected TCP socket, so the loopback test in the Integration run measures it. The
    /// options it sets are <see cref="ApplySocketOptions" />'s, which unit tests measure.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            ApplySocketOptions(socket);
            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        var localEndPoint = (IPEndPoint)socket.LocalEndPoint!;

        return new DialedTcpConnection(
            new StreamConnection(new NetworkStream(socket, ownsSocket: true), endPoint, localEndPoint),
            localEndPoint);
    }

    /// <summary>
    /// Sets <see cref="SocketOptions" /> on <paramref name="socket" />: <see cref="Socket.NoDelay" />
    /// from <see cref="TcpSocketOptions.NoDelay" />, and <c>SO_KEEPALIVE</c> from
    /// <see cref="TcpSocketOptions.KeepAlive" />, with the probe time and interval
    /// <see cref="TcpSocketOptions.KeepAliveSeconds" /> when it is on.
    /// </summary>
    /// <param name="socket">A TCP socket not yet connected.</param>
    internal void ApplySocketOptions(Socket socket)
    {
        socket.NoDelay = SocketOptions.NoDelay;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, SocketOptions.KeepAlive);
        if (!SocketOptions.KeepAlive)
        {
            return;
        }

        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, TcpSocketOptions.KeepAliveSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, TcpSocketOptions.KeepAliveSeconds);
    }
}
