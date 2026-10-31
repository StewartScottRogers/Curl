using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Curl.Extension.WireGuard.IpStack.Udp;

/// <summary>
/// Builds and reads UDP datagrams (RFC 768): an 8-byte header of source port, destination port, length and the
/// Internet checksum over the pseudo-header, header and data. A computed checksum of zero is sent as all ones; a
/// received zero checksum means none was computed, which IPv4 allows and IPv6 does not (RFC 8200 section 8.1).
/// </summary>
public static class UdpDatagramCodec
{
    /// <summary>The IP protocol number of UDP.</summary>
    public const byte UdpProtocol = 17;

    /// <summary>The length of the UDP header.</summary>
    public const int HeaderLength = 8;

    /// <summary>Writes a UDP datagram from <paramref name="source" /> to <paramref name="destination" /> carrying <paramref name="payload" />.</summary>
    /// <param name="source">The sender's address and port.</param>
    /// <param name="destination">The receiver's address and port, whose address the checksum's pseudo-header covers.</param>
    /// <param name="payload">The data.</param>
    /// <returns>The datagram's bytes, header first, ready to be an IP packet's payload.</returns>
    public static byte[] Build(IPEndPoint source, IPEndPoint destination, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        var bytes = new byte[HeaderLength + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)source.Port);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), (ushort)destination.Port);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)bytes.Length);
        payload.CopyTo(bytes.AsSpan(HeaderLength));
        var checksum = InternetChecksum.Finish(InternetChecksum.Add(IpPacketCodec.PseudoHeaderSum(source.Address, destination.Address, UdpProtocol, bytes.Length), bytes));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), checksum == 0 ? (ushort)0xFFFF : checksum);
        return bytes;
    }

    /// <summary>Reads the UDP datagram <paramref name="packet" /> carries, refusing one that is not UDP, is truncated or fails its checksum.</summary>
    /// <param name="packet">The IP packet.</param>
    /// <param name="datagram">The datagram read, or <see langword="null" /> when it is refused.</param>
    /// <returns>Whether the datagram was read.</returns>
    public static bool TryParse(IpPacket packet, out UdpDatagram? datagram)
    {
        ArgumentNullException.ThrowIfNull(packet);
        datagram = null;
        byte[] bytes = packet.Payload;
        if (packet.Protocol != UdpProtocol || bytes.Length < HeaderLength)
        {
            return false;
        }

        int length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4));
        if (length < HeaderLength || length > bytes.Length || !IsChecksumSound(packet, bytes.AsSpan(0, length)))
        {
            return false;
        }

        datagram = new UdpDatagram(
            BinaryPrimitives.ReadUInt16BigEndian(bytes),
            BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(2)),
            bytes[HeaderLength..length]);
        return true;
    }

    private static bool IsChecksumSound(IpPacket packet, ReadOnlySpan<byte> datagram) =>
        BinaryPrimitives.ReadUInt16BigEndian(datagram[6..]) == 0
            ? packet.Source.AddressFamily == AddressFamily.InterNetwork
            : InternetChecksum.Finish(InternetChecksum.Add(IpPacketCodec.PseudoHeaderSum(packet.Source, packet.Destination, UdpProtocol, datagram.Length), datagram)) == 0;
}
