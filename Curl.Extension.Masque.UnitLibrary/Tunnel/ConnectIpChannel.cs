using System.Net;
using Curl.Extension.Masque.Wire;
using Curl.HttpDatagram;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>
/// A CONNECT-IP tunnel as a UDP datagram channel: each datagram travels as an IP and UDP
/// packet from the address the proxy assigned (RFC 9484 section 4.7) inside a DATAGRAM
/// capsule with Context ID 0 (RFC 9484 section 6). Only UDP packets from the target's
/// address to the channel's own address and port are handed back; every other packet is dropped.
/// </summary>
/// <param name="stream">The tunnel's stream, past the address assignment.</param>
/// <param name="serverEndPoint">The target's address and the port the first datagram goes to.</param>
/// <param name="localEndPoint">The assigned address and the UDP port the channel sends from.</param>
internal sealed class ConnectIpChannel(MasqueTunnelStream stream, IPEndPoint serverEndPoint, IPEndPoint localEndPoint) : IDatagramChannel
{
    /// <inheritdoc />
    public EndPoint ServerEndPoint => serverEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => localEndPoint;

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not an IP address and port.</exception>
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        var ipDestination = destination as IPEndPoint
            ?? throw new ArgumentException("A CONNECT-IP tunnel sends only to an IP address and port.", nameof(destination));
        var packet = IpUdpPacket.Build(new IpUdpDatagram(localEndPoint, ipDestination, datagram));
        return stream.WriteCapsuleAsync(new Capsule(CapsuleType.Datagram, MasqueDatagramPayload.Wrap(packet)), cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>A payload longer than <paramref name="buffer" /> is cut to fit it.</remarks>
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            var packet = await stream.ReadDatagramContentAsync(cancellationToken).ConfigureAwait(false);
            if (IpUdpPacket.TryRead(packet.Span, out var datagram) && IsFromTargetToChannel(datagram!))
            {
                var length = Math.Min(datagram!.Payload.Length, buffer.Length);
                datagram.Payload[..length].CopyTo(buffer);
                return new DatagramReceived(length, datagram.Source);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => stream.DisposeAsync();

    private bool IsFromTargetToChannel(IpUdpDatagram datagram) =>
        datagram.Source.Address.Equals(serverEndPoint.Address) && datagram.Destination.Equals(localEndPoint);
}
