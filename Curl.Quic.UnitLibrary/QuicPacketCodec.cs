namespace Curl.Quic;

/// <summary>
/// Reads and writes QUIC version 1 packets with header protection removed (RFC 9000
/// section 17). A packet that cannot be read is a <see cref="QuicTransportException" />
/// with <see cref="QuicTransportErrorCode.ProtocolViolation" />, never an out-of-range read.
/// </summary>
public static class QuicPacketCodec
{
    /// <summary>QUIC version 1 (RFC 9000).</summary>
    public const uint Version1 = 1;

    private const byte LongHeaderBit = 0x80;

    private const byte FixedBit = 0x40;

    /// <summary>
    /// Writes a packet.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>Its bytes.</returns>
    /// <exception cref="ArgumentException">A field is out of range for the header form: a connection ID too long, a packet number length not 1 to 4, a Retry Integrity Tag not 16 bytes, or a token on a packet that carries none.</exception>
    public static byte[] Encode(QuicPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var writer = new QuicWriter();
        packet.WriteTo(writer);
        return writer.ToArray();
    }

    /// <summary>
    /// Reads the packet at the front of a datagram.
    /// </summary>
    /// <param name="datagram">The datagram, or what is left of it after the packets before.</param>
    /// <param name="shortHeaderConnectionIdLength">The length of the connection IDs this endpoint issued, which a short header carries without a length field; 0 to 20.</param>
    /// <returns>The packet and where its parts lie.</returns>
    /// <exception cref="QuicTransportException">The packet is truncated, its fixed bit is clear, its version is neither 0 nor 1, a connection ID is longer than 20 bytes, or a field is inconsistent with its length.</exception>
    public static QuicDecodedPacket Decode(ReadOnlyMemory<byte> datagram, int shortHeaderConnectionIdLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shortHeaderConnectionIdLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shortHeaderConnectionIdLength, QuicFrameCodec.MaximumConnectionIdLength);
        var reader = new QuicReader(datagram, QuicTransportErrorCode.ProtocolViolation);
        var firstByte = reader.ReadByte();
        return (firstByte & LongHeaderBit) == 0
            ? DecodeShortHeader(reader, firstByte, shortHeaderConnectionIdLength)
            : DecodeLongHeader(reader, firstByte);
    }

    private static QuicDecodedPacket DecodeShortHeader(QuicReader reader, byte firstByte, int connectionIdLength)
    {
        RequireFixedBit(reader, firstByte);
        var destinationConnectionId = reader.ReadBytes((ulong)connectionIdLength);
        var packetNumberLength = (firstByte & 0x03) + 1;
        var packetNumber = reader.ReadUInt(packetNumberLength);
        var headerLength = reader.Position;
        var packet = new QuicShortHeaderPacket(
            destinationConnectionId,
            packetNumberLength,
            packetNumber,
            reader.ReadBytes((ulong)reader.Remaining),
            SpinBit: (firstByte & 0x20) != 0,
            KeyPhase: (firstByte & 0x04) != 0,
            ReservedBits: (byte)(firstByte >> 3 & 0x03));
        return new QuicDecodedPacket(packet, headerLength, reader.Position);
    }

    private static QuicDecodedPacket DecodeLongHeader(QuicReader reader, byte firstByte)
    {
        var version = reader.ReadUInt(4);
        if (version == 0)
        {
            return DecodeVersionNegotiation(reader, firstByte);
        }

        RequireFixedBit(reader, firstByte);
        if (version != Version1)
        {
            throw reader.Fail($"Version 0x{version:x8} is not QUIC version 1.");
        }

        var destinationConnectionId = ReadConnectionId(reader);
        var sourceConnectionId = ReadConnectionId(reader);
        var type = (QuicPacketType)(firstByte >> 4 & 0x03);
        return type == QuicPacketType.Retry
            ? DecodeRetry(reader, firstByte, destinationConnectionId, sourceConnectionId)
            : DecodeWithPacketNumber(reader, firstByte, type, destinationConnectionId, sourceConnectionId);
    }

    private static QuicDecodedPacket DecodeVersionNegotiation(QuicReader reader, byte firstByte)
    {
        var destinationConnectionId = reader.ReadBytes(reader.ReadByte());
        var sourceConnectionId = reader.ReadBytes(reader.ReadByte());
        if (reader.Remaining % 4 != 0)
        {
            throw reader.Fail($"A Version Negotiation packet's version list is {reader.Remaining} bytes, not a multiple of 4.");
        }

        var versions = new List<uint>();
        while (reader.Remaining > 0)
        {
            versions.Add(reader.ReadUInt(4));
        }

        var packet = new QuicVersionNegotiationPacket(destinationConnectionId, sourceConnectionId, versions, (byte)(firstByte & 0x7f));
        return new QuicDecodedPacket(packet, reader.Position, reader.Position);
    }

    private static QuicDecodedPacket DecodeRetry(QuicReader reader, byte firstByte, ReadOnlyMemory<byte> destinationConnectionId, ReadOnlyMemory<byte> sourceConnectionId)
    {
        if (reader.Remaining <= QuicRetryPacket.RetryIntegrityTagLength)
        {
            throw reader.Fail($"A Retry packet has {reader.Remaining} bytes after its connection IDs: no room for a token and a {QuicRetryPacket.RetryIntegrityTagLength}-byte integrity tag.");
        }

        var token = reader.ReadBytes((ulong)(reader.Remaining - QuicRetryPacket.RetryIntegrityTagLength));
        var tag = reader.ReadBytes(QuicRetryPacket.RetryIntegrityTagLength);
        var packet = new QuicRetryPacket(Version1, destinationConnectionId, sourceConnectionId, token, tag, (byte)(firstByte & 0x0f));
        return new QuicDecodedPacket(packet, reader.Position, reader.Position);
    }

    private static QuicDecodedPacket DecodeWithPacketNumber(QuicReader reader, byte firstByte, QuicPacketType type, ReadOnlyMemory<byte> destinationConnectionId, ReadOnlyMemory<byte> sourceConnectionId)
    {
        var token = type == QuicPacketType.Initial ? reader.ReadLengthPrefixedBytes() : ReadOnlyMemory<byte>.Empty;
        var length = reader.ReadVariableLengthInteger();
        var packetNumberLength = (firstByte & 0x03) + 1;
        if (length < (ulong)packetNumberLength || length > (ulong)reader.Remaining)
        {
            throw reader.Fail($"A Length of {length} does not fit a {packetNumberLength}-byte packet number and the {reader.Remaining} bytes that follow.");
        }

        var packetNumber = reader.ReadUInt(packetNumberLength);
        var headerLength = reader.Position;
        var payload = reader.ReadBytes(length - (ulong)packetNumberLength);
        var packet = new QuicLongHeaderPacket(type, Version1, destinationConnectionId, sourceConnectionId, token, packetNumberLength, packetNumber, payload, (byte)(firstByte >> 2 & 0x03));
        return new QuicDecodedPacket(packet, headerLength, reader.Position);
    }

    private static ReadOnlyMemory<byte> ReadConnectionId(QuicReader reader)
    {
        var length = reader.ReadByte();
        return length <= QuicFrameCodec.MaximumConnectionIdLength
            ? reader.ReadBytes(length)
            : throw reader.Fail($"A {length}-byte connection ID is longer than QUIC version 1 allows.");
    }

    private static void RequireFixedBit(QuicReader reader, byte firstByte)
    {
        if ((firstByte & FixedBit) == 0)
        {
            throw reader.Fail("The packet's fixed bit is 0.");
        }
    }
}
