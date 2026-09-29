using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Opens the sockets <see cref="DnsServerResolver" /> sends its queries on: the only step of a
/// DNS lookup that touches a socket, kept behind this seam as <see cref="ITcpDialer" /> is
/// (ADR-0083), so the resolver is tested without a network.
/// </summary>
public interface IDnsSocketOpener
{
    /// <summary>Opens a UDP socket bound to <paramref name="localAddress" /> on an ephemeral port.</summary>
    /// <param name="server">The DNS server the queries go to.</param>
    /// <param name="localAddress">The local address to bind.</param>
    /// <returns>The channel.</returns>
    /// <exception cref="SocketException">The socket could not be opened or bound.</exception>
    IDatagramChannel OpenDatagramChannel(IPEndPoint server, IPAddress localAddress);

    /// <summary>Connects a TCP socket bound to <paramref name="localAddress" /> to <paramref name="server" />.</summary>
    /// <param name="server">The DNS server to connect to.</param>
    /// <param name="localAddress">The local address to bind.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connected stream, which the caller disposes.</returns>
    /// <exception cref="SocketException">The socket could not be bound or connected.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    ValueTask<Stream> ConnectStreamAsync(IPEndPoint server, IPAddress localAddress, CancellationToken cancellationToken);
}
