namespace Curl.Quic;

/// <summary>
/// Reads and writes the frames of a QUIC packet's payload (RFC 9000 sections 12.4 and 19).
/// A malformed frame is a <see cref="QuicTransportException" /> with
/// <see cref="QuicTransportErrorCode.FrameEncodingError" />; an empty payload, a frame type
/// not in its shortest encoding, or a frame the packet type may not carry is one with
/// <see cref="QuicTransportErrorCode.ProtocolViolation" />.
/// </summary>
public static class QuicFrameCodec
{
    /// <summary>The largest stream count MAX_STREAMS and STREAMS_BLOCKED may carry, 2^60 (section 19.11).</summary>
    public const ulong MaximumStreamCount = 1UL << 60;

    /// <summary>The longest connection ID QUIC version 1 allows (section 17.2).</summary>
    public const int MaximumConnectionIdLength = 20;

    private static readonly FrameRule[] Rules =
    [
        new(ReadPadding, PacketSpaces.All),
        new(ReadPing, PacketSpaces.All),
        new(ReadAck, PacketSpaces.AllButZeroRtt),
        new(ReadAck, PacketSpaces.AllButZeroRtt),
        new(ReadResetStream, PacketSpaces.ApplicationData),
        new(ReadStopSending, PacketSpaces.ApplicationData),
        new(ReadCrypto, PacketSpaces.AllButZeroRtt),
        new(ReadNewToken, PacketSpaces.OneRtt),
        .. Enumerable.Repeat(new FrameRule(ReadStream, PacketSpaces.ApplicationData), 8),
        new(ReadMaxData, PacketSpaces.ApplicationData),
        new(ReadMaxStreamData, PacketSpaces.ApplicationData),
        new(ReadMaxStreams, PacketSpaces.ApplicationData),
        new(ReadMaxStreams, PacketSpaces.ApplicationData),
        new(ReadDataBlocked, PacketSpaces.ApplicationData),
        new(ReadStreamDataBlocked, PacketSpaces.ApplicationData),
        new(ReadStreamsBlocked, PacketSpaces.ApplicationData),
        new(ReadStreamsBlocked, PacketSpaces.ApplicationData),
        new(ReadNewConnectionId, PacketSpaces.ApplicationData),
        new(ReadRetireConnectionId, PacketSpaces.ApplicationData),
        new(ReadPathChallenge, PacketSpaces.ApplicationData),
        new(ReadPathResponse, PacketSpaces.OneRtt),
        new(ReadConnectionClose, PacketSpaces.All),
        new(ReadConnectionClose, PacketSpaces.ApplicationData),
        new(ReadHandshakeDone, PacketSpaces.OneRtt),
    ];

    private delegate QuicFrame FrameReader(QuicReader reader, ulong type);

    /// <summary>
    /// The packet types a frame type may appear in (the Pkts column of table 3 in section 12.4).
    /// </summary>
    [Flags]
    private enum PacketSpaces
    {
        None = 0,
        Initial = 1,
        ZeroRtt = 2,
        Handshake = 4,
        OneRtt = 8,
        ApplicationData = ZeroRtt | OneRtt,
        AllButZeroRtt = Initial | Handshake | OneRtt,
        All = Initial | ZeroRtt | Handshake | OneRtt,
    }

    /// <summary>
    /// Writes frames one after another, as a packet's payload.
    /// </summary>
    /// <param name="frames">The frames, in order.</param>
    /// <returns>The payload bytes.</returns>
    public static byte[] Encode(IEnumerable<QuicFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var writer = new QuicWriter();
        foreach (var frame in frames)
        {
            frame.WriteTo(writer);
        }

        return writer.ToArray();
    }

    /// <summary>
    /// Reads every frame of a packet's payload. A run of PADDING frames is read as one
    /// <see cref="QuicPaddingFrame" />.
    /// </summary>
    /// <param name="payload">The payload, with packet protection removed.</param>
    /// <param name="packetType">The type of the packet it came in, which limits the frame types allowed (section 12.4).</param>
    /// <returns>The frames, in order.</returns>
    /// <exception cref="QuicTransportException">The payload is empty or holds a malformed or forbidden frame.</exception>
    public static IReadOnlyList<QuicFrame> Decode(ReadOnlyMemory<byte> payload, QuicPacketType packetType)
    {
        if (payload.IsEmpty)
        {
            throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, $"The {packetType} packet carries no frames.");
        }

        var reader = new QuicReader(payload, QuicTransportErrorCode.FrameEncodingError);
        var frames = new List<QuicFrame>();
        while (reader.Remaining > 0)
        {
            frames.Add(ReadFrame(reader, packetType));
        }

        return frames;
    }

    private static QuicFrame ReadFrame(QuicReader reader, QuicPacketType packetType)
    {
        var type = reader.ReadVariableLengthInteger(out var encodedLength);
        if (type >= (ulong)Rules.Length)
        {
            throw reader.Fail($"Unknown frame type 0x{type:x}.");
        }

        if (encodedLength != QuicVariableLengthInteger.GetEncodedLength(type))
        {
            throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, $"Frame type 0x{type:x} is encoded in {encodedLength} bytes, not the shortest encoding.");
        }

        var rule = Rules[type];
        if ((rule.PermittedIn & SpaceOf(packetType)) == PacketSpaces.None)
        {
            throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, $"Frame type 0x{type:x} is not allowed in a {packetType} packet.");
        }

        return rule.Read(reader, type);
    }

    private static PacketSpaces SpaceOf(QuicPacketType packetType) => packetType switch
    {
        QuicPacketType.Initial => PacketSpaces.Initial,
        QuicPacketType.ZeroRtt => PacketSpaces.ZeroRtt,
        QuicPacketType.Handshake => PacketSpaces.Handshake,
        QuicPacketType.OneRtt => PacketSpaces.OneRtt,
        _ => PacketSpaces.None,
    };

    private static QuicPaddingFrame ReadPadding(QuicReader reader, ulong type)
    {
        var nonZero = reader.Unread.IndexOfAnyExcept((byte)0);
        var zeros = nonZero < 0 ? reader.Remaining : nonZero;
        reader.ReadBytes((ulong)zeros);
        return new QuicPaddingFrame(zeros + 1);
    }

    private static QuicPingFrame ReadPing(QuicReader reader, ulong type) => new();

    private static QuicAckFrame ReadAck(QuicReader reader, ulong type)
    {
        var largest = reader.ReadVariableLengthInteger();
        var delay = reader.ReadVariableLengthInteger();
        var rangeCount = reader.ReadVariableLengthInteger();
        var firstRange = reader.ReadVariableLengthInteger();
        var smallest = RequireNotBelowZero(reader, largest, firstRange);
        var ranges = new List<QuicAckRange>();
        for (var index = 0UL; index < rangeCount; index++)
        {
            var gap = reader.ReadVariableLengthInteger();
            var length = reader.ReadVariableLengthInteger();
            smallest = RequireNotBelowZero(reader, RequireNotBelowZero(reader, smallest, gap + 2), length);
            ranges.Add(new QuicAckRange(gap, length));
        }

        QuicEcnCounts? ecnCounts = type == (ulong)QuicFrameType.AckWithEcnCounts
            ? new QuicEcnCounts(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger())
            : null;
        return new QuicAckFrame(largest, delay, firstRange, ranges, ecnCounts);
    }

    private static ulong RequireNotBelowZero(QuicReader reader, ulong packetNumber, ulong subtrahend) =>
        subtrahend <= packetNumber ? packetNumber - subtrahend : throw reader.Fail("An ACK range reaches below packet number 0.");

    private static QuicResetStreamFrame ReadResetStream(QuicReader reader, ulong type) =>
        new(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());

    private static QuicStopSendingFrame ReadStopSending(QuicReader reader, ulong type) =>
        new(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());

    private static QuicCryptoFrame ReadCrypto(QuicReader reader, ulong type)
    {
        var offset = reader.ReadVariableLengthInteger();
        var data = reader.ReadLengthPrefixedBytes();
        RequireEndWithinLimit(reader, offset, data);
        return new QuicCryptoFrame(offset, data);
    }

    private static QuicNewTokenFrame ReadNewToken(QuicReader reader, ulong type)
    {
        var token = reader.ReadLengthPrefixedBytes();
        return token.IsEmpty ? throw reader.Fail("A NEW_TOKEN frame carries an empty token.") : new QuicNewTokenFrame(token);
    }

    private static QuicStreamFrame ReadStream(QuicReader reader, ulong type)
    {
        var streamId = reader.ReadVariableLengthInteger();
        var offset = (type & QuicStreamFrame.OffsetBit) == 0 ? 0 : reader.ReadVariableLengthInteger();
        var hasLength = (type & QuicStreamFrame.LengthBit) != 0;
        var data = hasLength ? reader.ReadLengthPrefixedBytes() : reader.ReadBytes((ulong)reader.Remaining);
        RequireEndWithinLimit(reader, offset, data);
        return new QuicStreamFrame(streamId, offset, data, (type & QuicStreamFrame.FinBit) != 0, hasLength);
    }

    private static void RequireEndWithinLimit(QuicReader reader, ulong offset, ReadOnlyMemory<byte> data)
    {
        if (offset + (ulong)data.Length > QuicVariableLengthInteger.MaximumValue)
        {
            throw reader.Fail($"Data at offset {offset} of length {data.Length} ends beyond 2^62 - 1.");
        }
    }

    private static QuicMaxDataFrame ReadMaxData(QuicReader reader, ulong type) => new(reader.ReadVariableLengthInteger());

    private static QuicMaxStreamDataFrame ReadMaxStreamData(QuicReader reader, ulong type) =>
        new(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());

    private static QuicMaxStreamsFrame ReadMaxStreams(QuicReader reader, ulong type) =>
        new(type == (ulong)QuicFrameType.MaxStreamsUnidirectional, ReadStreamCount(reader));

    private static QuicDataBlockedFrame ReadDataBlocked(QuicReader reader, ulong type) => new(reader.ReadVariableLengthInteger());

    private static QuicStreamDataBlockedFrame ReadStreamDataBlocked(QuicReader reader, ulong type) =>
        new(reader.ReadVariableLengthInteger(), reader.ReadVariableLengthInteger());

    private static QuicStreamsBlockedFrame ReadStreamsBlocked(QuicReader reader, ulong type) =>
        new(type == (ulong)QuicFrameType.StreamsBlockedUnidirectional, ReadStreamCount(reader));

    private static ulong ReadStreamCount(QuicReader reader)
    {
        var count = reader.ReadVariableLengthInteger();
        return count <= MaximumStreamCount ? count : throw reader.Fail($"A stream count of {count} is above 2^60.");
    }

    private static QuicNewConnectionIdFrame ReadNewConnectionId(QuicReader reader, ulong type)
    {
        var sequenceNumber = reader.ReadVariableLengthInteger();
        var retirePriorTo = reader.ReadVariableLengthInteger();
        if (retirePriorTo > sequenceNumber)
        {
            throw reader.Fail($"Retire Prior To {retirePriorTo} is above Sequence Number {sequenceNumber}.");
        }

        var length = reader.ReadByte();
        if (length is < 1 or > MaximumConnectionIdLength)
        {
            throw reader.Fail($"A NEW_CONNECTION_ID frame carries a {length}-byte connection ID.");
        }

        return new QuicNewConnectionIdFrame(sequenceNumber, retirePriorTo, reader.ReadBytes(length), reader.ReadBytes(QuicNewConnectionIdFrame.StatelessResetTokenLength));
    }

    private static QuicRetireConnectionIdFrame ReadRetireConnectionId(QuicReader reader, ulong type) => new(reader.ReadVariableLengthInteger());

    private static QuicPathChallengeFrame ReadPathChallenge(QuicReader reader, ulong type) => new(reader.ReadBytes(QuicPathChallengeFrame.DataLength));

    private static QuicPathResponseFrame ReadPathResponse(QuicReader reader, ulong type) => new(reader.ReadBytes(QuicPathChallengeFrame.DataLength));

    private static QuicConnectionCloseFrame ReadConnectionClose(QuicReader reader, ulong type)
    {
        var errorCode = reader.ReadVariableLengthInteger();
        ulong? frameType = type == (ulong)QuicFrameType.ConnectionClose ? reader.ReadVariableLengthInteger() : null;
        return new QuicConnectionCloseFrame(errorCode, frameType, reader.ReadLengthPrefixedBytes());
    }

    private static QuicHandshakeDoneFrame ReadHandshakeDone(QuicReader reader, ulong type) => new();

    private readonly record struct FrameRule(FrameReader Read, PacketSpaces PermittedIn);
}
