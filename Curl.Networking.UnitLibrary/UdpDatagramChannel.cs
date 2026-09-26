using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IDatagramChannel" />: one UDP <see cref="Socket" />, bound to
/// an ephemeral local port, that sends each datagram to the endpoint it is given and
/// reports the source of each datagram it receives.
/// </summary>
/// <remarks>
/// The socket is not connected, so a TFTP handler can send its first request to
/// <see cref="ServerEndPoint" /> and then accept the reply from the server's new port, its
/// transfer identifier (RFC 1350 section 4). The channel owns the socket; disposing the
/// channel closes it.
/// </remarks>
public sealed class UdpDatagramChannel : IDatagramChannel
{
    private readonly Socket _socket;

    /// <summary>
    /// Opens a UDP socket of <paramref name="serverEndPoint" />'s address family, bound to
    /// any local address on an ephemeral port.
    /// </summary>
    /// <param name="serverEndPoint">The resolved endpoint the first datagram goes to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serverEndPoint" /> is <see langword="null" />.</exception>
    /// <exception cref="SocketException">The socket could not be opened or bound.</exception>
    public UdpDatagramChannel(IPEndPoint serverEndPoint)
        : this(serverEndPoint, static (socket, localEndPoint) => socket.Bind(localEndPoint))
    {
    }

    /// <summary>
    /// Opens a UDP socket of <paramref name="serverEndPoint" />'s address family and binds
    /// it through <paramref name="bind" />, so a test can make the bind fail.
    /// </summary>
    /// <param name="serverEndPoint">The resolved endpoint the first datagram goes to.</param>
    /// <param name="bind">Binds the socket to the local endpoint it is given.</param>
    internal UdpDatagramChannel(IPEndPoint serverEndPoint, Action<Socket, EndPoint> bind)
    {
        ArgumentNullException.ThrowIfNull(serverEndPoint);

        _socket = new Socket(serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            bind(_socket, new IPEndPoint(AnyAddressOf(serverEndPoint.AddressFamily), 0));
        }
        catch
        {
            _socket.Dispose();
            throw;
        }

        ServerEndPoint = serverEndPoint;
    }

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; }

    /// <summary>
    /// Gets the local endpoint the socket is bound to, the client's transfer identifier.
    /// </summary>
    public EndPoint LocalEndPoint => _socket.LocalEndPoint!;

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        await _socket.SendToAsync(datagram, SocketFlags.None, destination, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var anySource = new IPEndPoint(AnyAddressOf(_socket.AddressFamily), 0);
        var received = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, anySource, cancellationToken).ConfigureAwait(false);

        return new DatagramReceived(received.ReceivedBytes, received.RemoteEndPoint);
    }

    /// <summary>
    /// Closes the socket.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }

    private static IPAddress AnyAddressOf(AddressFamily addressFamily) =>
        addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
}
