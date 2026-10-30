using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Curl.Extension.WireGuard.IpStack;

/// <summary>
/// Builds and parses IPv4 (RFC 791) and IPv6 (RFC 8200) packets. Packets it builds are never fragmented: IPv4 ones
/// carry Don't Fragment. Parsing refuses fragments, skips IPv4 options and does not follow IPv6 extension headers
/// (ADR-1121).
/// </summary>
public static class IpPacketCodec
{
    /// <summary>The IP protocol number of TCP.</summary>
    public const byte TcpProtocol = 6;

    /// <summary>The hop limit (IPv4 time to live) of every packet built.</summary>
    public const byte HopLimit = 64;

    private const int IPv4HeaderLength = 20;
    private const int IPv6HeaderLength = 40;

    /// <summary>Writes <paramref name="packet" /> as an IPv4 packet when its addresses are IPv4, otherwise IPv6.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The packet's bytes, header first.</returns>
    public static byte[] Build(IpPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Source.AddressFamily != packet.Destination.AddressFamily)
        {
            throw new ArgumentException("The source and destination addresses are of different families.", nameof(packet));
        }

        return packet.Source.AddressFamily == AddressFamily.InterNetwork ? BuildIPv4(packet) : BuildIPv6(packet);
    }

    /// <summary>Reads an IPv4 or IPv6 packet, refusing one that is truncated, has a wrong IPv4 header checksum or is a fragment.</summary>
    /// <param name="bytes">The packet's bytes.</param>
    /// <param name="packet">The packet read, or <see langword="null" /> when it is refused.</param>
    /// <returns>Whether the packet was read.</returns>
    public static bool TryParse(ReadOnlySpan<byte> bytes, out IpPacket? packet)
    {
        packet = null;
        if (bytes.IsEmpty)
        {
            return false;
        }

        return (bytes[0] >> 4) switch
        {
            4 => TryParseIPv4(bytes, out packet),
            6 => TryParseIPv6(bytes, out packet),
            _ => false,
        };
    }

    /// <summary>The unfolded sum of the pseudo-header the TCP checksum covers (RFC 9293 section 3.1, RFC 8200 section 8.1).</summary>
    /// <param name="source">The source address.</param>
    /// <param name="destination">The destination address.</param>
    /// <param name="protocol">The upper-layer protocol number.</param>
    /// <param name="length">The upper-layer length: header and data.</param>
    /// <returns>The sum, to pass to <see cref="InternetChecksum.Add" />.</returns>
    public static uint PseudoHeaderSum(IPAddress source, IPAddress destination, byte protocol, int length)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        var sum = InternetChecksum.Add(0, source.GetAddressBytes());
        sum = InternetChecksum.Add(sum, destination.GetAddressBytes());
        return sum + protocol + (uint)(length >> 16) + (uint)(length & 0xFFFF);
    }

    private static byte[] BuildIPv4(IpPacket packet)
    {
        var bytes = new byte[IPv4HeaderLength + packet.Payload.Length];
        bytes[0] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), 0x4000);
        bytes[8] = HopLimit;
        bytes[9] = packet.Protocol;
        packet.Source.TryWriteBytes(bytes.AsSpan(12, 4), out _);
        packet.Destination.TryWriteBytes(bytes.AsSpan(16, 4), out _);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(10), InternetChecksum.Compute(bytes.AsSpan(0, IPv4HeaderLength)));
        packet.Payload.CopyTo(bytes, IPv4HeaderLength);
        return bytes;
    }

    private static byte[] BuildIPv6(IpPacket packet)
    {
        var bytes = new byte[IPv6HeaderLength + packet.Payload.Length];
        bytes[0] = 0x60;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)packet.Payload.Length);
        bytes[6] = packet.Protocol;
        bytes[7] = HopLimit;
        packet.Source.TryWriteBytes(bytes.AsSpan(8, 16), out _);
        packet.Destination.TryWriteBytes(bytes.AsSpan(24, 16), out _);
        packet.Payload.CopyTo(bytes, IPv6HeaderLength);
        return bytes;
    }

    private static bool TryParseIPv4(ReadOnlySpan<byte> bytes, out IpPacket? packet)
    {
        packet = null;
        if (bytes.Length < IPv4HeaderLength)
        {
            return false;
        }

        var headerLength = (bytes[0] & 0x0F) * 4;
        int totalLength = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
        if (!IsIPv4HeaderSound(bytes, headerLength, totalLength))
        {
            return false;
        }

        packet = new IpPacket(
            new IPAddress(bytes.Slice(12, 4)),
            new IPAddress(bytes.Slice(16, 4)),
            bytes[9],
            bytes[headerLength..totalLength].ToArray());
        return true;
    }

    private static bool IsIPv4HeaderSound(ReadOnlySpan<byte> bytes, int headerLength, int totalLength)
    {
        var isFragment = (BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]) & 0x3FFF) != 0;
        return headerLength >= IPv4HeaderLength
            && totalLength >= headerLength
            && totalLength <= bytes.Length
            && !isFragment
            && InternetChecksum.Compute(bytes[..headerLength]) == 0;
    }

    private static bool TryParseIPv6(ReadOnlySpan<byte> bytes, out IpPacket? packet)
    {
        packet = null;
        if (bytes.Length < IPv6HeaderLength)
        {
            return false;
        }

        var end = IPv6HeaderLength + BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]);
        if (end > bytes.Length)
        {
            return false;
        }

        packet = new IpPacket(
            new IPAddress(bytes.Slice(8, 16)),
            new IPAddress(bytes.Slice(24, 16)),
            bytes[6],
            bytes[IPv6HeaderLength..end].ToArray());
        return true;
    }
}
