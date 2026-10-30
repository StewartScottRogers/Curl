using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// Builds and reads the IP packets a CONNECT-IP tunnel carries (RFC 9484 section 6) when they
/// hold one UDP datagram: an IPv4 header (RFC 791) or an IPv6 header (RFC 8200), then the UDP
/// header (RFC 768) with its checksum over the pseudo-header.
/// </summary>
internal static class IpUdpPacket
{
    /// <summary>The IP protocol number of UDP.</summary>
    public const byte UdpProtocol = 17;

    private const byte HopLimit = 64;
    private const int IPv4HeaderLength = 20;
    private const int IPv6HeaderLength = 40;
    private const int UdpHeaderLength = 8;

    /// <summary>Builds the IP packet that carries a UDP datagram, with a hop limit of 64.</summary>
    /// <param name="datagram">The datagram.</param>
    /// <returns>The packet's bytes.</returns>
    /// <exception cref="ArgumentException">The endpoints are of different families, or the payload does not fit one packet.</exception>
    public static byte[] Build(IpUdpDatagram datagram)
    {
        ArgumentNullException.ThrowIfNull(datagram);
        var isIPv4 = datagram.Source.AddressFamily == AddressFamily.InterNetwork;
        if (datagram.Source.AddressFamily != datagram.Destination.AddressFamily)
        {
            throw new ArgumentException("The source and destination are of different address families.", nameof(datagram));
        }

        var headerLength = isIPv4 ? IPv4HeaderLength : IPv6HeaderLength;
        var udpLength = UdpHeaderLength + datagram.Payload.Length;
        if (udpLength > ushort.MaxValue - (isIPv4 ? IPv4HeaderLength : 0))
        {
            throw new ArgumentException("The payload does not fit one IP packet.", nameof(datagram));
        }

        var packet = new byte[headerLength + udpLength];
        if (isIPv4)
        {
            WriteIPv4Header(packet, datagram);
        }
        else
        {
            WriteIPv6Header(packet, datagram);
        }

        WriteUdp(packet.AsSpan(headerLength), datagram);
        return packet;
    }

    /// <summary>
    /// Reads the UDP datagram an incoming packet carries. A packet of another protocol, a
    /// fragment, an IPv6 packet with extension headers and a truncated packet are ignored.
    /// </summary>
    /// <param name="packet">The IP packet.</param>
    /// <param name="datagram">The datagram, or <see langword="null" /> when the packet is ignored.</param>
    /// <returns><see langword="true" /> when the packet carried a UDP datagram.</returns>
    public static bool TryRead(ReadOnlySpan<byte> packet, out IpUdpDatagram? datagram)
    {
        datagram = null;
        return !packet.IsEmpty && (packet[0] >> 4) switch
        {
            4 => TryReadIPv4(packet, out datagram),
            6 => TryReadIPv6(packet, out datagram),
            _ => false,
        };
    }

    private static void WriteIPv4Header(Span<byte> packet, IpUdpDatagram datagram)
    {
        packet[0] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(packet[2..], (ushort)packet.Length);
        packet[6] = 0x40; // Don't Fragment
        packet[8] = HopLimit;
        packet[9] = UdpProtocol;
        datagram.Source.Address.TryWriteBytes(packet[12..], out _);
        datagram.Destination.Address.TryWriteBytes(packet[16..], out _);
        BinaryPrimitives.WriteUInt16BigEndian(packet[10..], Fold(Sum(packet[..IPv4HeaderLength], 0)));
    }

    private static void WriteIPv6Header(Span<byte> packet, IpUdpDatagram datagram)
    {
        packet[0] = 0x60;
        BinaryPrimitives.WriteUInt16BigEndian(packet[4..], (ushort)(packet.Length - IPv6HeaderLength));
        packet[6] = UdpProtocol;
        packet[7] = HopLimit;
        datagram.Source.Address.TryWriteBytes(packet[8..], out _);
        datagram.Destination.Address.TryWriteBytes(packet[24..], out _);
    }

    // The UDP header and payload; the checksum covers the pseudo-header, whose sum is the same
    // for IPv4 and IPv6: both addresses, the protocol and the UDP length.
    private static void WriteUdp(Span<byte> segment, IpUdpDatagram datagram)
    {
        BinaryPrimitives.WriteUInt16BigEndian(segment, (ushort)datagram.Source.Port);
        BinaryPrimitives.WriteUInt16BigEndian(segment[2..], (ushort)datagram.Destination.Port);
        BinaryPrimitives.WriteUInt16BigEndian(segment[4..], (ushort)segment.Length);
        datagram.Payload.Span.CopyTo(segment[UdpHeaderLength..]);
        var pseudoHeader = Sum(datagram.Source.Address.GetAddressBytes(), 0) + Sum(datagram.Destination.Address.GetAddressBytes(), 0) + UdpProtocol + (uint)segment.Length;
        var checksum = Fold(Sum(segment, pseudoHeader));
        BinaryPrimitives.WriteUInt16BigEndian(segment[6..], checksum == 0 ? ushort.MaxValue : checksum);
    }

    private static bool TryReadIPv4(ReadOnlySpan<byte> packet, out IpUdpDatagram? datagram)
    {
        datagram = null;
        if (packet.Length < IPv4HeaderLength)
        {
            return false;
        }

        var headerLength = (packet[0] & 0x0F) * 4;
        var totalLength = BinaryPrimitives.ReadUInt16BigEndian(packet[2..]);
        var isFragment = (BinaryPrimitives.ReadUInt16BigEndian(packet[6..]) & 0x3FFF) != 0;
        if (headerLength < IPv4HeaderLength || totalLength < headerLength || totalLength > packet.Length || packet[9] != UdpProtocol || isFragment)
        {
            return false;
        }

        return TryReadUdp(packet[headerLength..totalLength], new IPAddress(packet[12..16]), new IPAddress(packet[16..20]), out datagram);
    }

    private static bool TryReadIPv6(ReadOnlySpan<byte> packet, out IpUdpDatagram? datagram)
    {
        datagram = null;
        if (packet.Length < IPv6HeaderLength)
        {
            return false;
        }

        var end = IPv6HeaderLength + BinaryPrimitives.ReadUInt16BigEndian(packet[4..]);
        if (end > packet.Length || packet[6] != UdpProtocol)
        {
            return false;
        }

        return TryReadUdp(packet[IPv6HeaderLength..end], new IPAddress(packet[8..24]), new IPAddress(packet[24..40]), out datagram);
    }

    private static bool TryReadUdp(ReadOnlySpan<byte> segment, IPAddress source, IPAddress destination, out IpUdpDatagram? datagram)
    {
        datagram = null;
        var udpLength = segment.Length < UdpHeaderLength ? 0 : BinaryPrimitives.ReadUInt16BigEndian(segment[4..]);
        if (udpLength < UdpHeaderLength || udpLength > segment.Length)
        {
            return false;
        }

        datagram = new(
            new IPEndPoint(source, BinaryPrimitives.ReadUInt16BigEndian(segment)),
            new IPEndPoint(destination, BinaryPrimitives.ReadUInt16BigEndian(segment[2..])),
            segment[UdpHeaderLength..udpLength].ToArray());
        return true;
    }

    // The 16-bit one's complement sum of RFC 1071, not yet folded.
    private static uint Sum(ReadOnlySpan<byte> bytes, uint sum)
    {
        var index = 0;
        for (; index + 1 < bytes.Length; index += 2)
        {
            sum += BinaryPrimitives.ReadUInt16BigEndian(bytes[index..]);
        }

        return index < bytes.Length ? sum + ((uint)bytes[index] << 8) : sum;
    }

    private static ushort Fold(uint sum)
    {
        while (sum > ushort.MaxValue)
        {
            sum = (sum & ushort.MaxValue) + (sum >> 16);
        }

        return (ushort)~sum;
    }
}
