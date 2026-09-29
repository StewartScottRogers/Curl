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
}
