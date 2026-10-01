using System.Diagnostics.CodeAnalysis;
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
    private readonly IPEndPoint _serverEndPoint;
    private readonly Action<Socket, EndPoint> _connectRouteProbe;

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
    /// Opens a UDP socket of <paramref name="serverEndPoint" />'s address family, bound to
    /// <paramref name="localAddress" /> on an ephemeral port, as a DNS query's socket is bound
    /// under <c>--dns-ipv4-addr</c>, <c>--dns-ipv6-addr</c> or <c>--dns-interface</c> (BL-694).
    /// </summary>
    /// <param name="serverEndPoint">The resolved endpoint the first datagram goes to.</param>
    /// <param name="localAddress">The local address to bind.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serverEndPoint" /> is <see langword="null" />.</exception>
    /// <exception cref="SocketException">The socket could not be opened or bound.</exception>
    public UdpDatagramChannel(IPEndPoint serverEndPoint, IPAddress localAddress)
        : this(serverEndPoint, static (socket, localEndPoint) => socket.Bind(localEndPoint), localAddress)
    {
    }

    /// <summary>
    /// Opens a UDP socket of <paramref name="serverEndPoint" />'s address family, bound to
    /// <paramref name="localAddress" /> (any address of the family when <see langword="null" />)
    /// on <paramref name="localPort" /> (an ephemeral port when <c>0</c>), as a QUIC connection's
    /// socket is bound under <c>--interface</c> and <c>--local-port</c> (BL-728).
    /// </summary>
    /// <param name="serverEndPoint">The resolved endpoint the first datagram goes to.</param>
    /// <param name="localAddress">The local address to bind, or <see langword="null" /> for any.</param>
    /// <param name="localPort">The local port to bind, or <c>0</c> for an ephemeral one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serverEndPoint" /> is <see langword="null" />.</exception>
    /// <exception cref="SocketException">The socket could not be opened or bound.</exception>
    public UdpDatagramChannel(IPEndPoint serverEndPoint, IPAddress? localAddress, int localPort)
        : this(serverEndPoint, static (socket, localEndPoint) => socket.Bind(localEndPoint), localAddress, localPort)
    {
    }

    /// <summary>
    /// Opens a UDP socket of <paramref name="serverEndPoint" />'s address family and binds
    /// it through <paramref name="bind" />, so a test can make the bind fail.
    /// </summary>
    /// <param name="serverEndPoint">The resolved endpoint the first datagram goes to.</param>
    /// <param name="bind">Binds the socket to the local endpoint it is given.</param>
    /// <param name="localAddress">The local address to bind; <see langword="null" /> for any address of the family.</param>
    /// <param name="localPort">The local port to bind; <c>0</c> for an ephemeral one.</param>
    /// <param name="connectRouteProbe">Connects the throwaway socket <see cref="LocalEndPoint" /> asks the kernel's route with; <see langword="null" /> for <see cref="Socket.Connect(EndPoint)" />.</param>
    internal UdpDatagramChannel(
        IPEndPoint serverEndPoint,
        Action<Socket, EndPoint> bind,
        IPAddress? localAddress = null,
        int localPort = 0,
        Action<Socket, EndPoint>? connectRouteProbe = null)
    {
        ArgumentNullException.ThrowIfNull(serverEndPoint);

        _connectRouteProbe = connectRouteProbe ?? (static (socket, remoteEndPoint) => socket.Connect(remoteEndPoint));
        _serverEndPoint = serverEndPoint;
        _socket = new Socket(serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            bind(_socket, new IPEndPoint(localAddress ?? AnyAddressOf(serverEndPoint.AddressFamily), localPort));
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
    /// Gets the local endpoint the socket's datagrams to <see cref="ServerEndPoint" /> leave
    /// from, the client's transfer identifier: the bound endpoint, except that a socket bound to
    /// any address reports the address the kernel's route to the server sends from, as curl's
    /// connected UDP socket reports it through <c>getsockname</c> (BL-1051). When the kernel
    /// has no route, the any address stays.
    /// </summary>
    /// <remarks>
    /// The channel's own socket stays unconnected, so a TFTP reply from a new port is still
    /// received; the route is asked with a throwaway socket that is connected, which sends
    /// nothing, and closed.
    /// </remarks>
    public EndPoint LocalEndPoint
    {
        get
        {
            var bound = (IPEndPoint)_socket.LocalEndPoint!;
            return bound.Address.Equals(AnyAddressOf(bound.AddressFamily))
                ? new IPEndPoint(RouteSourceAddress() ?? bound.Address, bound.Port)
                : bound;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083: sending puts a datagram on a socket, so the
    /// loopback round trip in the Integration run measures it.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        await _socket.SendToAsync(datagram, SocketFlags.None, destination, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    /// <remarks>
    /// Excluded from coverage per ADR-0083: returning a datagram needs one to arrive on the
    /// socket, so the loopback round trip in the Integration run measures it.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
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

    // The source address the kernel picks for the server: a connected UDP socket's local
    // address. Connecting a UDP socket sends nothing; no route to the server is null.
    private IPAddress? RouteSourceAddress()
    {
        using var probe = new Socket(_serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            _connectRouteProbe(probe, _serverEndPoint);
            return ((IPEndPoint)probe.LocalEndPoint!).Address;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static IPAddress AnyAddressOf(AddressFamily addressFamily) =>
        addressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
}
