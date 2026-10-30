using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>What one tunnel holds open: the UDP socket to the peer and the transport keys, closed together.</summary>
/// <param name="udp">The UDP socket to the peer.</param>
/// <param name="session">The transport keys.</param>
internal sealed class WireGuardTunnelLifetime(IWireGuardUdpChannel udp, WireGuardTransportSession session) : IAsyncDisposable
{
    /// <summary>Closes the UDP socket, then zeroes the keys.</summary>
    /// <returns>A task that completes when both are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        await udp.DisposeAsync().ConfigureAwait(false);
        session.Dispose();
    }
}
