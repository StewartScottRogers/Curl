using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Arrange("frame type", frame.Type);
        Diagnostics.Arrange("expected hex", expectedHex);

        var encoded = QuicFrameCodec.Encode([frame]);

        Diagnostics.Bytes($"encoded {frame.Type} frame", encoded);
        Diagnostics.Diff("encoded hex", expectedHex, HexOf(encoded));
        Assert.AreEqual(expectedHex, HexOf(encoded));
        var decoded = QuicFrameCodec.Decode(encoded, QuicPacketType.OneRtt);
        Diagnostics.Act("decoded frame count", decoded.Count);
        Diagnostics.Assert("decoded frame count", 1, decoded.Count);
        Assert.HasCount(1, decoded);
        Diagnostics.Assert("decoded frame type", frame.Type, decoded[0].Type);
        Assert.AreEqual(frame.Type, decoded[0].Type);
        var reencoded = HexOf(QuicFrameCodec.Encode(decoded));
        Diagnostics.Diff("re-encoded hex", expectedHex, reencoded);
        Assert.AreEqual(expectedHex, reencoded);
    }

    [TestMethod]
    public void Decode_AckWithRangesAndEcnCounts_ReadsEveryField()
    {
        Diagnostics.Arrange("payload hex (Initial)", "030a0301020102040506");
        Diagnostics.Bytes("payload", Hex("030a0301020102040506"));

        var ack = (QuicAckFrame)QuicFrameCodec.Decode(Hex("030a0301020102040506"), QuicPacketType.Initial).Single();

        Diagnostics.Act("largest acknowledged", ack.LargestAcknowledged);
        Diagnostics.Act("ack delay", ack.AckDelay);
        Diagnostics.Act("first ack range", ack.FirstAckRange);
        Diagnostics.Act("ack range", ack.AckRanges.Single());
        Diagnostics.Act("ecn counts", ack.EcnCounts);
        Diagnostics.Assert("largest acknowledged", 10UL, ack.LargestAcknowledged);
        Assert.AreEqual(10UL, ack.LargestAcknowledged);
        Diagnostics.Assert("ack delay", 3UL, ack.AckDelay);
        Assert.AreEqual(3UL, ack.AckDelay);
        Diagnostics.Assert("first ack range", 2UL, ack.FirstAckRange);
        Assert.AreEqual(2UL, ack.FirstAckRange);
        Diagnostics.Assert("ack range", new QuicAckRange(1, 2), ack.AckRanges.Single());
        Assert.AreEqual(new QuicAckRange(1, 2), ack.AckRanges.Single());
        Diagnostics.Assert("ecn counts", new QuicEcnCounts(4, 5, 6), ack.EcnCounts);
        Assert.AreEqual(new QuicEcnCounts(4, 5, 6), ack.EcnCounts);
    }

    [TestMethod]
    public void Decode_StreamWithoutLength_TakesTheRestOfThePayload()
    {
        Diagnostics.Arrange("payload hex (OneRtt)", "0f040901dd" + "0c0409aabbcc");
        Diagnostics.Bytes("payload", Hex("0f040901dd" + "0c0409aabbcc"));

        var stream = (QuicStreamFrame)QuicFrameCodec.Decode(Hex("0f040901dd" + "0c0409aabbcc"), QuicPacketType.OneRtt)[1];

        Diagnostics.Act("offset", stream.Offset);
        Diagnostics.Bytes("stream data", stream.Data.Span);
        Diagnostics.Act("has length", stream.HasLength);
        Diagnostics.Act("is fin", stream.IsFin);
        Diagnostics.Assert("offset", 9UL, stream.Offset);
        Assert.AreEqual(9UL, stream.Offset);
        Diagnostics.Diff("data hex", "aabbcc", HexOf(stream.Data));
        Assert.AreEqual("aabbcc", HexOf(stream.Data));
        Diagnostics.Assert("has length", false, stream.HasLength);
        Assert.IsFalse(stream.HasLength);
        Diagnostics.Assert("is fin", false, stream.IsFin);
        Assert.IsFalse(stream.IsFin);
    }

    [TestMethod]
    public void Decode_PaddingThenAFrame_ReadsTheRunAsOneFrame()
    {
        Diagnostics.Arrange("payload hex (Initial)", "0000000001");

        var frames = QuicFrameCodec.Decode(Hex("0000000001"), QuicPacketType.Initial);

        Diagnostics.Act("frames", string.Join(", ", frames.Select(frame => frame.Type)));
        Diagnostics.Assert("first frame", new QuicPaddingFrame(4), frames[0]);
        Assert.AreEqual(new QuicPaddingFrame(4), frames[0]);
        Diagnostics.Assert("second frame is ping", true, frames[1] is QuicPingFrame);
        Assert.IsInstanceOfType<QuicPingFrame>(frames[1]);
    }

    [TestMethod]
    public void Decode_ConnectionCloseReason_ReadsTheBytes()
    {
        Diagnostics.Arrange("payload hex (Handshake)", "1c0a0603627965");

        var close = (QuicConnectionCloseFrame)QuicFrameCodec.Decode(Hex("1c0a0603627965"), QuicPacketType.Handshake).Single();

        var reason = System.Text.Encoding.UTF8.GetString(close.ReasonPhrase.Span);
        Diagnostics.Act("error code", close.ErrorCode);
        Diagnostics.Act("frame type", close.FrameType);
        Diagnostics.Act("reason phrase", reason);
        Diagnostics.Assert("error code", (ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Diagnostics.Assert("frame type", 6UL, close.FrameType);
        Assert.AreEqual(6UL, close.FrameType);
        Diagnostics.Assert("reason phrase", "bye", reason);
        Assert.AreEqual("bye", reason);
    }

    [TestMethod]
    public void Decode_EmptyPayload_IsAProtocolViolation()
    {
        Diagnostics.Arrange("payload hex (Initial)", "(empty)");

        var exception = Assert.ThrowsExactly<QuicTransportException>(() => QuicFrameCodec.Decode(ReadOnlyMemory<byte>.Empty, QuicPacketType.Initial));

        Diagnostics.Act("exception", $"{exception.GetType().Name} {exception.ErrorCode}: {exception.Message}");
        Diagnostics.Assert("error code", QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, exception.ErrorCode);
        Diagnostics.Diff("message", "QUIC ProtocolViolation: The Initial packet carries no frames.", exception.Message);
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
    public void Decode_MalformedFrame_IsAFrameEncodingError(string hex)
    {
        Diagnostics.Arrange("payload hex (OneRtt)", hex);
        Diagnostics.Bytes("payload", Hex(hex));

        var error = ErrorOf(() => QuicFrameCodec.Decode(Hex(hex), QuicPacketType.OneRtt));

        Diagnostics.Act("transport error code", error);
        Diagnostics.Assert("transport error code", QuicTransportErrorCode.FrameEncodingError, error);
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, error);
    }

    [TestMethod]
    public void Decode_FrameTypeNotInItsShortestEncoding_IsAProtocolViolation()
    {
        Diagnostics.Arrange("payload hex (OneRtt)", "4001");

        var error = ErrorOf(() => QuicFrameCodec.Decode(Hex("4001"), QuicPacketType.OneRtt));

        Diagnostics.Act("transport error code", error);
        Diagnostics.Assert("transport error code", QuicTransportErrorCode.ProtocolViolation, error);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, error);
    }

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
    public void Decode_FrameThePacketTypeMayNotCarry_IsAProtocolViolation(string hex, QuicPacketType packetType)
    {
        Diagnostics.Arrange("payload hex", hex);
        Diagnostics.Arrange("packet type", packetType);
        Diagnostics.Bytes("payload", Hex(hex));

        var error = ErrorOf(() => QuicFrameCodec.Decode(Hex(hex), packetType));

        Diagnostics.Act("transport error code", error);
        Diagnostics.Assert("transport error code", QuicTransportErrorCode.ProtocolViolation, error);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, error);
    }

    [TestMethod]
    [DataRow("1d0000", QuicPacketType.ZeroRtt)]
    [DataRow("1c000000", QuicPacketType.Handshake)]
    [DataRow("0a0401dd", QuicPacketType.ZeroRtt)]
    public void Decode_FrameThePacketTypeMayCarry_IsRead(string hex, QuicPacketType packetType)
    {
        Diagnostics.Arrange("payload hex", hex);
        Diagnostics.Arrange("packet type", packetType);
        Diagnostics.Bytes("payload", Hex(hex));

        var frames = QuicFrameCodec.Decode(Hex(hex), packetType);

        Diagnostics.Act("decoded frame count", frames.Count);
        Diagnostics.Assert("decoded frame count", 1, frames.Count);
        Assert.HasCount(1, frames);
    }

    [TestMethod]
    public void Encode_NullFrames_Throws()
    {
        Diagnostics.Arrange("frames", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => QuicFrameCodec.Encode(null!));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
