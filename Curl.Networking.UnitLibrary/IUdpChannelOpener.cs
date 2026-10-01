using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Opens the UDP datagram channel a QUIC connection runs over: the seam that keeps
/// <see cref="QuicDialer" /> off the network in tests, as <see cref="ITcpDialer" /> keeps
/// <see cref="TcpConnector" /> off it (ADR-0083).
/// </summary>
public interface IUdpChannelOpener
{
    /// <summary>
    /// Opens a channel whose datagrams go to <paramref name="serverEndPoint" />.
    /// </summary>
    /// <param name="serverEndPoint">The resolved server endpoint.</param>
    /// <returns>The open channel, which the caller disposes.</returns>
    /// <exception cref="SocketException">The socket could not be opened or bound.</exception>
    IDatagramChannel Open(IPEndPoint serverEndPoint);

    /// <summary>
    /// Opens a channel whose datagrams go to <paramref name="serverEndPoint" />, its socket bound to
    /// <paramref name="localEndPoint" />'s address on the first port from <paramref name="localEndPoint" />'s
    /// port that binds, trying at most <paramref name="localPortCount" /> ports, as libcurl's
    /// <c>bindlocal</c> binds every IP socket (<c>--interface</c>, <c>--local-port</c>, BL-1025).
    /// </summary>
    /// <param name="serverEndPoint">The resolved server endpoint.</param>
    /// <param name="localEndPoint">The local address and the first port; port 0 for an ephemeral one.</param>
    /// <param name="localPortCount">How many ports to try; fewer than 1 is taken as 1.</param>
    /// <returns>The open channel, which the caller disposes.</returns>
    /// <exception cref="LocalBindException">No port of the range bound (<see cref="LocalBindFailure.InterfaceFailed" />).</exception>
    /// <exception cref="SocketException">The socket could not be opened.</exception>
    IDatagramChannel OpenFrom(IPEndPoint serverEndPoint, IPEndPoint localEndPoint, int localPortCount);
}
