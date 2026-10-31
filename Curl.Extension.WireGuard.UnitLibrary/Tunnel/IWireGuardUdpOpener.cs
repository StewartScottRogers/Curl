using System.Net.Sockets;
using Curl.Extension.WireGuard.Configuration;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>Opens the UDP socket a tunnel's datagrams travel over, so the connector is tested without a network.</summary>
internal interface IWireGuardUdpOpener
{
    /// <summary>Resolves <paramref name="endpoint" /> outside the tunnel and connects a UDP socket to it.</summary>
    /// <param name="endpoint">The peer's <c>Endpoint</c>.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The connected socket.</returns>
    /// <exception cref="SocketException">The endpoint does not resolve or the socket cannot be opened.</exception>
    ValueTask<IWireGuardUdpChannel> OpenAsync(WireGuardEndpoint endpoint, CancellationToken cancellationToken);
}
