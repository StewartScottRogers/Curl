using static Curl.Quic.QuicTest;

namespace Curl.Quic;

/// <summary>
/// Checks <see cref="QuicFrameCodec" /> round-trips every frame type of RFC 9000 section 19
/// and rejects each malformed or forbidden frame with the transport error the RFC names.
/// </summary>
[TestClass]
public sealed class QuicFrameCodecTests
{
    private static readonly byte[] ConnectionId = Hex("0102030405060708");
    private static readonly byte[] ResetToken = Hex("00112233445566778899aabbccddeeff");
    private static readonly byte[] EightBytes = Hex("a1a2a3a4a5a6a7a8");

    public static IEnumerable<object[]> EveryFrame =>
    [
        [new QuicPaddingFrame(3), "000000"],
        [new QuicPingFrame(), "01"],
        [new QuicAckFrame(10, 3, 2, [], null), "020a030002"],
        [new QuicAckFrame(10, 3, 2, [new QuicAckRange(1, 2)], new QuicEcnCounts(4, 5, 6)), "030a03010201020405 06".Replace(" ", string.Empty, StringComparison.Ordinal)],
        [new QuicResetStreamFrame(4, 7, 100), "0404074064"],
        [new QuicStopSendingFrame(4, 7), "050407"],
        [new QuicCryptoFrame(64, Hex("aabb")), "06404002aabb"],
        [new QuicNewTokenFrame(Hex("cc")), "0701cc"],
        [new QuicStreamFrame(4, 0, Hex("dd"), IsFin: false, HasLength: false), "0804dd"],
        [new QuicStreamFrame(4, 0, Hex("dd"), IsFin: true, HasLength: false), "0904dd"],
        [new QuicStreamFrame(4, 0, Hex("dd"), IsFin: false), "0a0401dd"],
        [new QuicStreamFrame(4, 0, Hex("dd"), IsFin: true), "0b0401dd"],
        [new QuicStreamFrame(4, 9, Hex("dd"), IsFin: false, HasLength: false), "0c0409dd"],
        [new QuicStreamFrame(4, 9, Hex("dd"), IsFin: true, HasLength: false), "0d0409dd"],
        [new QuicStreamFrame(4, 9, Hex("dd"), IsFin: false), "0e040901dd"],
        [new QuicStreamFrame(4, 9, Hex("dd"), IsFin: true), "0f040901dd"],
        [new QuicMaxDataFrame(1000), "1043e8"],
        [new QuicMaxStreamDataFrame(4, 1000), "110443e8"],
        [new QuicMaxStreamsFrame(IsUnidirectional: false, 100), "124064"],
        [new QuicMaxStreamsFrame(IsUnidirectional: true, 3), "1303"],
        [new QuicDataBlockedFrame(1000), "1443e8"],
        [new QuicStreamDataBlockedFrame(4, 1000), "150443e8"],
        [new QuicStreamsBlockedFrame(IsUnidirectional: false, 100), "164064"],
        [new QuicStreamsBlockedFrame(IsUnidirectional: true, 3), "1703"],
        [new QuicNewConnectionIdFrame(2, 1, ConnectionId, ResetToken), "18020108" + HexOf(ConnectionId) + HexOf(ResetToken)],
        [new QuicRetireConnectionIdFrame(2), "1902"],
        [new QuicPathChallengeFrame(EightBytes), "1a" + HexOf(EightBytes)],
        [new QuicPathResponseFrame(EightBytes), "1b" + HexOf(EightBytes)],
        [new QuicConnectionCloseFrame((ulong)QuicTransportErrorCode.InternalError, 0, ReadOnlyMemory<byte>.Empty), "1c010000"],
        [new QuicConnectionCloseFrame(0x100, null, "bye"u8.ToArray()), "1d410003627965"],
        [new QuicHandshakeDoneFrame(), "1e"],
    ];

    [TestMethod]
    [DynamicData(nameof(EveryFrame))]
    public void EncodeThenDecode_EveryFrameType_WritesTheRfcLayoutAndReadsItBack(QuicFrame frame, string expectedHex)
    {
        var encoded = QuicFrameCodec.Encode([frame]);

        Assert.AreEqual(expectedHex, HexOf(encoded));
        var decoded = QuicFrameCodec.Decode(encoded, QuicPacketType.OneRtt);
        Assert.HasCount(1, decoded);
        Assert.AreEqual(frame.Type, decoded[0].Type);
        Assert.AreEqual(expectedHex, HexOf(QuicFrameCodec.Encode(decoded)));
    }

    [TestMethod]
    public void Decode_AckWithRangesAndEcnCounts_ReadsEveryField()
    {
        var ack = (QuicAckFrame)QuicFrameCodec.Decode(Hex("030a0301020102040506"), QuicPacketType.Initial).Single();

        Assert.AreEqual(10UL, ack.LargestAcknowledged);
        Assert.AreEqual(3UL, ack.AckDelay);
        Assert.AreEqual(2UL, ack.FirstAckRange);
        Assert.AreEqual(new QuicAckRange(1, 2), ack.AckRanges.Single());
        Assert.AreEqual(new QuicEcnCounts(4, 5, 6), ack.EcnCounts);
    }

    [TestMethod]
    public void Decode_StreamWithoutLength_TakesTheRestOfThePayload()
    {
        var stream = (QuicStreamFrame)QuicFrameCodec.Decode(Hex("0f040901dd" + "0c0409aabbcc"), QuicPacketType.OneRtt)[1];

        Assert.AreEqual(9UL, stream.Offset);
        Assert.AreEqual("aabbcc", HexOf(stream.Data));
        Assert.IsFalse(stream.HasLength);
        Assert.IsFalse(stream.IsFin);
    }

    [TestMethod]
    public void Decode_PaddingThenAFrame_ReadsTheRunAsOneFrame()
    {
        var frames = QuicFrameCodec.Decode(Hex("0000000001"), QuicPacketType.Initial);

        Assert.AreEqual(new QuicPaddingFrame(4), frames[0]);
        Assert.IsInstanceOfType<QuicPingFrame>(frames[1]);
    }

    [TestMethod]
    public void Decode_ConnectionCloseReason_ReadsTheBytes()
    {
        var close = (QuicConnectionCloseFrame)QuicFrameCodec.Decode(Hex("1c0a0603627965"), QuicPacketType.Handshake).Single();

        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Assert.AreEqual(6UL, close.FrameType);
        Assert.AreEqual("bye", System.Text.Encoding.UTF8.GetString(close.ReasonPhrase.Span));
    }

    [TestMethod]
    public void Decode_EmptyPayload_IsAProtocolViolation()
    {
        var exception = Assert.ThrowsExactly<QuicTransportException>(() => QuicFrameCodec.Decode(ReadOnlyMemory<byte>.Empty, QuicPacketType.Initial));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Assert.AreEqual("QUIC ProtocolViolation: The Initial packet carries no frames.", exception.Message);
    }

    [TestMethod]
    [DataRow("1f", DisplayName = "unknown frame type")]
    [DataRow("4040", DisplayName = "unknown two-byte frame type")]
    [DataRow("06", DisplayName = "CRYPTO ends before its offset")]
    [DataRow("060005aa", DisplayName = "CRYPTO shorter than its length")]
    [DataRow("06ffffffffffffffff01aa", DisplayName = "CRYPTO ending beyond 2^62 - 1")]
    [DataRow("0e04ffffffffffffffff01aa", DisplayName = "STREAM ending beyond 2^62 - 1")]
    [DataRow("0700", DisplayName = "NEW_TOKEN with an empty token")]
    [DataRow("02030005", DisplayName = "ACK first range below packet 0")]
    [DataRow("02050001040400", DisplayName = "ACK gap below packet 0")]
    [DataRow("020a0001030205", DisplayName = "ACK range length below packet 0")]
    [DataRow("12d000000000000001", DisplayName = "MAX_STREAMS above 2^60")]
    [DataRow("17d000000000000001", DisplayName = "STREAMS_BLOCKED above 2^60")]
    [DataRow("18010200", DisplayName = "NEW_CONNECTION_ID retiring beyond its own sequence number")]
    [DataRow("18010000", DisplayName = "NEW_CONNECTION_ID with an empty connection ID")]
    [DataRow("18010015", DisplayName = "NEW_CONNECTION_ID with a 21-byte connection ID")]
    [DataRow("180100", DisplayName = "NEW_CONNECTION_ID ending before its length")]
    [DataRow("1aa1a2a3", DisplayName = "PATH_CHALLENGE shorter than eight bytes")]
    public void Decode_MalformedFrame_IsAFrameEncodingError(string hex) =>
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, ErrorOf(() => QuicFrameCodec.Decode(Hex(hex), QuicPacketType.OneRtt)));

    [TestMethod]
    public void Decode_FrameTypeNotInItsShortestEncoding_IsAProtocolViolation() =>
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, ErrorOf(() => QuicFrameCodec.Decode(Hex("4001"), QuicPacketType.OneRtt)));

    [TestMethod]
    [DataRow("0a0401dd", QuicPacketType.Initial, DisplayName = "STREAM in Initial")]
    [DataRow("0a0401dd", QuicPacketType.Handshake, DisplayName = "STREAM in Handshake")]
    [DataRow("020a030002", QuicPacketType.ZeroRtt, DisplayName = "ACK in 0-RTT")]
    [DataRow("1b" + "a1a2a3a4a5a6a7a8", QuicPacketType.ZeroRtt, DisplayName = "PATH_RESPONSE in 0-RTT")]
    [DataRow("1d0000", QuicPacketType.Initial, DisplayName = "application CONNECTION_CLOSE in Initial")]
    [DataRow("1e", QuicPacketType.Handshake, DisplayName = "HANDSHAKE_DONE in Handshake")]
    [DataRow("01", QuicPacketType.Retry, DisplayName = "PING in Retry")]
    [DataRow("01", QuicPacketType.VersionNegotiation, DisplayName = "PING in Version Negotiation")]
    [DataRow("01", (QuicPacketType)99, DisplayName = "PING in an undefined packet type")]
    public void Decode_FrameThePacketTypeMayNotCarry_IsAProtocolViolation(string hex, QuicPacketType packetType) =>
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, ErrorOf(() => QuicFrameCodec.Decode(Hex(hex), packetType)));

    [TestMethod]
    [DataRow("1d0000", QuicPacketType.ZeroRtt)]
    [DataRow("1c000000", QuicPacketType.Handshake)]
    [DataRow("0a0401dd", QuicPacketType.ZeroRtt)]
    public void Decode_FrameThePacketTypeMayCarry_IsRead(string hex, QuicPacketType packetType) =>
        Assert.HasCount(1, QuicFrameCodec.Decode(Hex(hex), packetType));

    [TestMethod]
    public void Encode_NullFrames_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicFrameCodec.Encode(null!));
}
