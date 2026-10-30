using System.Buffers.Binary;
using System.Net;

namespace Curl.Extension.WireGuard.IpStack;

/// <summary>Builds and parses TCP segments (RFC 9293) with the checksum over the IPv4 or IPv6 pseudo-header.</summary>
public static class TcpSegmentCodec
{
    private const int HeaderLength = 20;
    private const byte EndOfOptions = 0;
    private const byte NoOperation = 1;
    private const byte MaximumSegmentSizeKind = 2;
    private const byte MaximumSegmentSizeLength = 4;

    /// <summary>Writes <paramref name="segment" />, with its checksum, as sent from <paramref name="source" /> to <paramref name="destination" />.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="source">The source address the pseudo-header holds.</param>
    /// <param name="destination">The destination address the pseudo-header holds.</param>
    /// <returns>The segment's bytes.</returns>
    public static byte[] Build(TcpSegment segment, IPAddress source, IPAddress destination)
    {
        ArgumentNullException.ThrowIfNull(segment);
        var optionsLength = segment.MaximumSegmentSize is null ? 0 : MaximumSegmentSizeLength;
        var bytes = new byte[HeaderLength + optionsLength + segment.Payload.Length];
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt16BigEndian(span, segment.SourcePort);
        BinaryPrimitives.WriteUInt16BigEndian(span[2..], segment.DestinationPort);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..], segment.SequenceNumber);
        BinaryPrimitives.WriteUInt32BigEndian(span[8..], segment.AcknowledgementNumber);
        span[12] = (byte)((HeaderLength + optionsLength) / 4 << 4);
        span[13] = (byte)segment.Flags;
        BinaryPrimitives.WriteUInt16BigEndian(span[14..], segment.Window);
        if (segment.MaximumSegmentSize is { } maximumSegmentSize)
        {
            span[20] = MaximumSegmentSizeKind;
            span[21] = MaximumSegmentSizeLength;
            BinaryPrimitives.WriteUInt16BigEndian(span[22..], maximumSegmentSize);
        }

        segment.Payload.CopyTo(span[(HeaderLength + optionsLength)..]);
        BinaryPrimitives.WriteUInt16BigEndian(span[16..], Checksum(bytes, source, destination));
        return bytes;
    }

    /// <summary>Reads a TCP segment, refusing one that is truncated or whose checksum is wrong.</summary>
    /// <param name="bytes">The segment's bytes: the IP packet's payload.</param>
    /// <param name="source">The source address of the IP packet.</param>
    /// <param name="destination">The destination address of the IP packet.</param>
    /// <param name="segment">The segment read, or <see langword="null" /> when it is refused.</param>
    /// <returns>Whether the segment was read.</returns>
    public static bool TryParse(ReadOnlySpan<byte> bytes, IPAddress source, IPAddress destination, out TcpSegment? segment)
    {
        segment = null;
        if (bytes.Length < HeaderLength)
        {
            return false;
        }

        var dataOffset = (bytes[12] >> 4) * 4;
        if (dataOffset < HeaderLength || dataOffset > bytes.Length || Checksum(bytes, source, destination) != 0)
        {
            return false;
        }

        segment = new TcpSegment(
            BinaryPrimitives.ReadUInt16BigEndian(bytes),
            BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]),
            BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]),
            BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]),
            (TcpFlags)(bytes[13] & 0x1F),
            BinaryPrimitives.ReadUInt16BigEndian(bytes[14..]),
            ReadMaximumSegmentSize(bytes[HeaderLength..dataOffset]),
            bytes[dataOffset..].ToArray());
        return true;
    }

    private static ushort Checksum(ReadOnlySpan<byte> bytes, IPAddress source, IPAddress destination) =>
        InternetChecksum.Finish(InternetChecksum.Add(
            IpPacketCodec.PseudoHeaderSum(source, destination, IpPacketCodec.TcpProtocol, bytes.Length),
            bytes));

    private static ushort? ReadMaximumSegmentSize(ReadOnlySpan<byte> options)
    {
        var length = OptionLength(options);
        while (length > 0)
        {
            if (options[0] == MaximumSegmentSizeKind && length == MaximumSegmentSizeLength)
            {
                return BinaryPrimitives.ReadUInt16BigEndian(options[2..]);
            }

            options = options[length..];
            length = OptionLength(options);
        }

        return null;
    }

    /// <summary>The length of the first option, or zero at the end of the options or when the option is malformed.</summary>
    private static int OptionLength(ReadOnlySpan<byte> options)
    {
        if (options.IsEmpty || options[0] == EndOfOptions)
        {
            return 0;
        }

        return options[0] == NoOperation ? 1 : DeclaredOptionLength(options);
    }

    private static int DeclaredOptionLength(ReadOnlySpan<byte> options) =>
        options.Length < 2 || options[1] < 2 || options[1] > options.Length ? 0 : options[1];
}
