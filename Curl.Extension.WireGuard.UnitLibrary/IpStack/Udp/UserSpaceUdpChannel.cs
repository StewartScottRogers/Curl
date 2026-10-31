using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.IpStack.Udp;

/// <summary>
/// A UDP socket in the user-space IP stack: each datagram sent is built into a UDP datagram (RFC 768) inside an IP
/// packet from <paramref name="local" />, and each packet received is kept only when it is a sound UDP datagram
/// addressed to <paramref name="local" />; any other packet is dropped, as a socket would never see it.
/// </summary>
/// <param name="packets">Where the IP packets go and come from: the inside of the tunnel.</param>
/// <param name="local">The socket's own address inside the tunnel and its port.</param>
/// <param name="server">Where the first datagram goes.</param>
/// <param name="owner">What closing the socket closes too: the tunnel the packets travel through.</param>
public sealed class UserSpaceUdpChannel(IIpPacketChannel packets, IPEndPoint local, IPEndPoint server, IAsyncDisposable owner) : IDatagramChannel
{
    /// <inheritdoc />
    public EndPoint ServerEndPoint => server;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => local;

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        if (destination is not IPEndPoint remote || remote.AddressFamily != local.AddressFamily)
        {
            throw new ArgumentException($"A datagram inside the tunnel goes to an address of the family of {local}.", nameof(destination));
        }

        byte[] payload = UdpDatagramCodec.Build(local, remote, datagram.Span);
        return packets.SendAsync(IpPacketCodec.Build(new IpPacket(local.Address, remote.Address, UdpDatagramCodec.UdpProtocol, payload)), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadOnlyMemory<byte> bytes = await packets.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (TryReadForThisSocket(bytes.Span, out IpPacket? packet, out UdpDatagram? datagram))
            {
                int length = Math.Min(datagram!.Payload.Length, buffer.Length);
                datagram.Payload.AsSpan(0, length).CopyTo(buffer.Span);
                return new DatagramReceived(length, new IPEndPoint(packet!.Source, datagram.SourcePort));
            }
        }
    }

    /// <summary>Closes the tunnel the socket's packets travel through.</summary>
    /// <returns>A task that completes when it is closed.</returns>
    public ValueTask DisposeAsync() => owner.DisposeAsync();

    private bool TryReadForThisSocket(ReadOnlySpan<byte> bytes, out IpPacket? packet, out UdpDatagram? datagram)
    {
        datagram = null;
        return IpPacketCodec.TryParse(bytes, out packet)
            && packet!.Destination.Equals(local.Address)
            && UdpDatagramCodec.TryParse(packet, out datagram)
            && datagram!.DestinationPort == local.Port;
    }
}
