namespace Curl.Extension.WireGuard.Noise;

/// <summary>
/// The datagram path to one WireGuard peer, usually a UDP socket connected to its endpoint.
/// The handshake is handed one, so it is tested without a network.
/// </summary>
public interface IWireGuardDatagramChannel
{
    /// <summary>Sends one datagram to the peer.</summary>
    ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken);

    /// <summary>Waits for the next datagram from the peer.</summary>
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);
}
