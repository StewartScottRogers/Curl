using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// Looks host names up for curl's connector under <c>--wireguard</c> by DNS over TCP through the tunnel (ADR-1161),
/// so the name of an HTTP/3 transfer, which curl's connector resolves before it dials QUIC, never reaches the system
/// resolver (BL-4392). An address literal is its own answer. A name the tunnel's DNS server cannot answer, or cannot be reached for, gives no address.
/// </summary>
/// <param name="tunnel">The tunnel's connector, whose DNS through the tunnel answers.</param>
internal sealed class WireGuardTunnelDnsResolver(WireGuardConnector tunnel) : IDnsResolver
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal))
        {
            return [literal];
        }

        TunnelResolution resolution = await tunnel.ResolveThroughTunnelAsync(host, cancellationToken).ConfigureAwait(false);
        return resolution.Address is { } address ? [address] : [];
    }
}
