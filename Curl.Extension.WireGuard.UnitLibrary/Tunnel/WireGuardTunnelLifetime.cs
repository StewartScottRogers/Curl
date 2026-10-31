namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>What one tunnel holds open: the UDP socket to the peer and the tunnel's keys and timers, closed together.</summary>
/// <param name="udp">The UDP socket to the peer.</param>
/// <param name="tunnel">The tunnel, holding the transport keys and the keepalive timers.</param>
internal sealed class WireGuardTunnelLifetime(IWireGuardUdpChannel udp, WireGuardPacketTunnel tunnel) : IAsyncDisposable
{
    /// <summary>Closes the UDP socket, then stops the tunnel's timers and zeroes its keys.</summary>
    /// <returns>A task that completes when both are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        await udp.DisposeAsync().ConfigureAwait(false);
        tunnel.Dispose();
    }
}
