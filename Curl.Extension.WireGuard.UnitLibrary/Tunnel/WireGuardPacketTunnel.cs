using Curl.Extension.WireGuard.IpStack;
using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// The inside of the tunnel the IP stack sends and receives over (whitepaper section 5.4.6): each IP packet sent is
/// sealed into a transport data message, and each datagram received is opened, keeping only the packets of this
/// session and dropping keepalives, other messages and anything that fails to open.
/// </summary>
/// <param name="channel">The datagram path to the peer.</param>
/// <param name="session">The transport keys the handshake gave.</param>
internal sealed class WireGuardPacketTunnel(IWireGuardDatagramChannel channel, WireGuardTransportSession session) : IIpPacketChannel
{
    private readonly Lock sealLock = new();

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken)
    {
        byte[] message;
        lock (sealLock)
        {
            message = session.Seal(packet.Span);
        }

        return channel.SendAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadOnlyMemory<byte> datagram = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (session.TryOpen(datagram.Span, out byte[]? packet) && packet.Length > 0)
            {
                return packet;
            }
        }
    }
}
