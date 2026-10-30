namespace Curl.Extension.WireGuard.IpStack;

/// <summary>Where the IP stack sends and receives raw IP packets: the inside of the tunnel.</summary>
public interface IIpPacketChannel
{
    /// <summary>Sends one IP packet.</summary>
    /// <param name="packet">The packet's bytes, header first.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the packet is handed on.</returns>
    ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken);

    /// <summary>Waits for the next IP packet, throwing when the channel is closed.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The packet's bytes, header first.</returns>
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);
}
