using Curl.Testing;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

/// <summary>
/// Checks <see cref="QuicPacketCodec" /> reads the unprotected sample packets of RFC 9001
/// Appendix A, round-trips every packet type, and rejects each malformed packet as a
/// <see cref="QuicTransportErrorCode.ProtocolViolation" />.
/// </summary>
[TestClass]
public sealed class QuicPacketCodecTests
{
    /// <summary>RFC 9001 Appendix A.2: the client Initial's unprotected header.</summary>
    private const string ClientInitialHeader = "c300000001088394c8f03e5157080000449e00000002";

    /// <summary>RFC 9001 Appendix A.2: the CRYPTO frame carrying the ClientHello.</summary>
    private const string ClientInitialCryptoFrame =
        """
        060040f1010000ed0303ebf8fa56f129 39b9584a3896472ec40bb863cfd3e868
        04fe3a47f06a2b69484c000004130113 02010000c000000010000e00000b6578
        616d706c652e636f6dff01000100000a 00080006001d00170018001000070005
        04616c706e0005000501000000000033 00260024001d00209370b2c9caa47fba
        baf4559fedba753de171fa71f50f1ce1 5d43e994ec74d748002b000302030400
        0d0010000e0403050306030203080408 050806002d00020101001c0002400100
        3900320408ffffffffffffffff050480 00ffff07048000ffff08011001048000
        75300901100f088394c8f03e51570806 048000ffff
        """;

    /// <summary>RFC 9001 Appendix A.3: the server Initial's unprotected header.</summary>
    private const string ServerInitialHeader = "c1000000010008f067a5502a4262b50040750001";

    /// <summary>RFC 9001 Appendix A.3: the server Initial's ACK and CRYPTO frames.</summary>
    private const string ServerInitialFrames =
        """
        02000000000600405a020000560303ee fce7f7b37ba1d1632e96677825ddf739
        88cfc79825df566dc5430b9a045a1200 130100002e00330024001d00209d3c94
        0d89690b84d08a60993c144eca684d10 81287c834d5311bcf32bb9da1a002b00
        020304
        """;

    /// <summary>RFC 9001 Appendix A.4: the Retry packet.</summary>
    private const string RetryPacket = "ff000000010008f067a5502a4262b5746f6b656e04a265ba2eff4d829058fb3f0f2496ba";

    private const int AuthenticationTagLength = 16;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Decode_Rfc9001AppendixA2ClientInitial_ReadsTheHeaderAndTheCryptoFrame()
    {
        var crypto = Hex(ClientInitialCryptoFrame);
        var datagram = Hex(ClientInitialHeader).Concat(crypto).Concat(new byte[1162 - crypto.Length + AuthenticationTagLength]).ToArray();
        Diagnostics.Arrange("RFC 9001 A.2 header hex", ClientInitialHeader);
        Diagnostics.Bytes("client Initial datagram (header, CRYPTO frame, zero padding and tag)", datagram);

        var decoded = QuicPacketCodec.Decode(datagram, 0);

        var packet = (QuicLongHeaderPacket)decoded.Packet;
        Diagnostics.Act("packet type", packet.Type);
        Diagnostics.Act("version", packet.Version);
        Diagnostics.Act("destination connection ID", HexOf(packet.DestinationConnectionId));
        Diagnostics.Act("source connection ID length", packet.SourceConnectionId.Length);
        Diagnostics.Act("token length", packet.Token.Length);
        Diagnostics.Act("packet number length", packet.PacketNumberLength);
        Diagnostics.Act("truncated packet number", packet.TruncatedPacketNumber);
        Diagnostics.Act("reserved bits", packet.ReservedBits);
        Diagnostics.Act("payload length", packet.Payload.Length);
        Diagnostics.Act("header length", decoded.HeaderLength);
        Diagnostics.Act("packet length", decoded.Length);
        Diagnostics.Assert("packet type", QuicPacketType.Initial, packet.Type);
        Assert.AreEqual(QuicPacketType.Initial, packet.Type);
        Diagnostics.Assert("version", QuicPacketCodec.Version1, packet.Version);
        Assert.AreEqual(QuicPacketCodec.Version1, packet.Version);
        Diagnostics.Assert("destination connection ID", "8394c8f03e515708", HexOf(packet.DestinationConnectionId));
        Assert.AreEqual("8394c8f03e515708", HexOf(packet.DestinationConnectionId));
        Diagnostics.Assert("source connection ID is empty", true, packet.SourceConnectionId.IsEmpty);
        Assert.IsTrue(packet.SourceConnectionId.IsEmpty);
        Diagnostics.Assert("token is empty", true, packet.Token.IsEmpty);
        Assert.IsTrue(packet.Token.IsEmpty);
        Diagnostics.Assert("packet number length", 4, packet.PacketNumberLength);
        Assert.AreEqual(4, packet.PacketNumberLength);
        Diagnostics.Assert("truncated packet number", 2u, packet.TruncatedPacketNumber);
        Assert.AreEqual(2u, packet.TruncatedPacketNumber);
        Diagnostics.Assert("reserved bits", 0, packet.ReservedBits);
        Assert.AreEqual(0, packet.ReservedBits);
        Diagnostics.Assert("payload length", 1182 - 4, packet.Payload.Length);
        Assert.AreEqual(1182 - 4, packet.Payload.Length);
        Diagnostics.Assert("header length", 22, decoded.HeaderLength);
        Assert.AreEqual(22, decoded.HeaderLength);
        Diagnostics.Assert("packet length", 1200, decoded.Length);
        Assert.AreEqual(1200, decoded.Length);
        var reencoded = QuicPacketCodec.Encode(packet);
        Diagnostics.Diff("re-encoded datagram", datagram, reencoded);
        CollectionAssert.AreEqual(datagram, reencoded);

        var frames = QuicFrameCodec.Decode(packet.Payload[..1162], packet.Type);
        var cryptoFrame = (QuicCryptoFrame)frames[0];
        Diagnostics.Act("CRYPTO frame offset", cryptoFrame.Offset);
        Diagnostics.Act("CRYPTO frame data length", cryptoFrame.Data.Length);
        Diagnostics.Act("CRYPTO frame data start", HexOf(cryptoFrame.Data[..6]));
        Diagnostics.Assert("CRYPTO frame offset", 0UL, cryptoFrame.Offset);
        Assert.AreEqual(0UL, cryptoFrame.Offset);
        Diagnostics.Assert("CRYPTO frame data length", 241, cryptoFrame.Data.Length);
        Assert.AreEqual(241, cryptoFrame.Data.Length);
        Diagnostics.Assert("CRYPTO frame data start", "010000ed0303", HexOf(cryptoFrame.Data[..6]));
        Assert.AreEqual("010000ed0303", HexOf(cryptoFrame.Data[..6]));
        Diagnostics.Assert("padding frame", new QuicPaddingFrame(1162 - crypto.Length), frames[1]);
        Assert.AreEqual(new QuicPaddingFrame(1162 - crypto.Length), frames[1]);
        var reencodedFrames = QuicFrameCodec.Encode(frames);
        Diagnostics.Diff("re-encoded frames", packet.Payload[..1162].ToArray(), reencodedFrames);
        CollectionAssert.AreEqual(packet.Payload[..1162].ToArray(), reencodedFrames);
    }

    [TestMethod]
    public void Decode_Rfc9001AppendixA3ServerInitial_ReadsTheHeaderAckAndCryptoFrame()
    {
        var frameBytes = Hex(ServerInitialFrames);
        var datagram = Hex(ServerInitialHeader).Concat(frameBytes).Concat(new byte[AuthenticationTagLength]).ToArray();
        Diagnostics.Arrange("RFC 9001 A.3 header hex", ServerInitialHeader);
        Diagnostics.Bytes("server Initial datagram (header, ACK and CRYPTO frames, zero tag)", datagram);

        var decoded = QuicPacketCodec.Decode(datagram, 0);

        var packet = (QuicLongHeaderPacket)decoded.Packet;
        var packetNumber = QuicPacketNumber.Decode(0, packet.TruncatedPacketNumber, packet.PacketNumberLength);
        Diagnostics.Act("packet type", packet.Type);
        Diagnostics.Act("destination connection ID length", packet.DestinationConnectionId.Length);
        Diagnostics.Act("source connection ID", HexOf(packet.SourceConnectionId));
        Diagnostics.Act("packet number length", packet.PacketNumberLength);
        Diagnostics.Act("decoded packet number", packetNumber);
        Diagnostics.Act("header length", decoded.HeaderLength);
        Diagnostics.Assert("packet type", QuicPacketType.Initial, packet.Type);
        Assert.AreEqual(QuicPacketType.Initial, packet.Type);
        Diagnostics.Assert("destination connection ID is empty", true, packet.DestinationConnectionId.IsEmpty);
        Assert.IsTrue(packet.DestinationConnectionId.IsEmpty);
        Diagnostics.Assert("source connection ID", "f067a5502a4262b5", HexOf(packet.SourceConnectionId));
        Assert.AreEqual("f067a5502a4262b5", HexOf(packet.SourceConnectionId));
        Diagnostics.Assert("packet number length", 2, packet.PacketNumberLength);
        Assert.AreEqual(2, packet.PacketNumberLength);
        Diagnostics.Assert("decoded packet number", 1UL, packetNumber);
        Assert.AreEqual(1UL, QuicPacketNumber.Decode(0, packet.TruncatedPacketNumber, packet.PacketNumberLength));
        Diagnostics.Assert("header length", 20, decoded.HeaderLength);
        Assert.AreEqual(20, decoded.HeaderLength);
        var reencoded = QuicPacketCodec.Encode(packet);
        Diagnostics.Diff("re-encoded datagram", datagram, reencoded);
        CollectionAssert.AreEqual(datagram, reencoded);

        var frames = QuicFrameCodec.Decode(packet.Payload[..frameBytes.Length], packet.Type);
        var ack = (QuicAckFrame)frames[0];
        Diagnostics.Act("ACK largest acknowledged", ack.LargestAcknowledged);
        Diagnostics.Act("ACK first range", ack.FirstAckRange);
        Diagnostics.Act("ACK has ECN counts", ack.EcnCounts is not null);
        Diagnostics.Act("CRYPTO frame data length", ((QuicCryptoFrame)frames[1]).Data.Length);
        Diagnostics.Assert("ACK largest acknowledged", 0UL, ack.LargestAcknowledged);
        Assert.AreEqual(0UL, ack.LargestAcknowledged);
        Diagnostics.Assert("ACK first range", 0UL, ack.FirstAckRange);
        Assert.AreEqual(0UL, ack.FirstAckRange);
        Assert.IsNull(ack.EcnCounts);
        Diagnostics.Assert("CRYPTO frame data length", 90, ((QuicCryptoFrame)frames[1]).Data.Length);
        Assert.AreEqual(90, ((QuicCryptoFrame)frames[1]).Data.Length);
        var reencodedFrames = QuicFrameCodec.Encode(frames);
        Diagnostics.Diff("re-encoded frames", frameBytes, reencodedFrames);
        CollectionAssert.AreEqual(frameBytes, reencodedFrames);
    }

    [TestMethod]
    public void Decode_Rfc9001AppendixA4Retry_ReadsTheTokenAndIntegrityTag()
    {
        var datagram = Hex(RetryPacket);
        Diagnostics.Arrange("RFC 9001 A.4 Retry packet hex", RetryPacket);
        Diagnostics.Bytes("Retry datagram", datagram);

        var decoded = QuicPacketCodec.Decode(datagram, 0);

        var packet = (QuicRetryPacket)decoded.Packet;
        var token = System.Text.Encoding.ASCII.GetString(packet.RetryToken.Span);
        Diagnostics.Act("version", packet.Version);
        Diagnostics.Act("destination connection ID length", packet.DestinationConnectionId.Length);
        Diagnostics.Act("source connection ID", HexOf(packet.SourceConnectionId));
        Diagnostics.Act("retry token", token);
        Diagnostics.Act("retry integrity tag", HexOf(packet.RetryIntegrityTag));
        Diagnostics.Act("unused bits", packet.UnusedBits);
        Diagnostics.Act("packet length", decoded.Length);
        Diagnostics.Act("header length", decoded.HeaderLength);
        Diagnostics.Assert("version", QuicPacketCodec.Version1, packet.Version);
        Assert.AreEqual(QuicPacketCodec.Version1, packet.Version);
        Diagnostics.Assert("destination connection ID is empty", true, packet.DestinationConnectionId.IsEmpty);
        Assert.IsTrue(packet.DestinationConnectionId.IsEmpty);
        Diagnostics.Assert("source connection ID", "f067a5502a4262b5", HexOf(packet.SourceConnectionId));
        Assert.AreEqual("f067a5502a4262b5", HexOf(packet.SourceConnectionId));
        Diagnostics.Assert("retry token", "token", token);
        Assert.AreEqual("token", token);
        Diagnostics.Diff("retry integrity tag", "04a265ba2eff4d829058fb3f0f2496ba", HexOf(packet.RetryIntegrityTag));
        Assert.AreEqual("04a265ba2eff4d829058fb3f0f2496ba", HexOf(packet.RetryIntegrityTag));
        Diagnostics.Assert("unused bits", 0x0f, packet.UnusedBits);
        Assert.AreEqual(0x0f, packet.UnusedBits);
        Diagnostics.Assert("packet length", datagram.Length, decoded.Length);
        Assert.AreEqual(datagram.Length, decoded.Length);
        Diagnostics.Assert("header length", datagram.Length, decoded.HeaderLength);
        Assert.AreEqual(datagram.Length, decoded.HeaderLength);
        var reencoded = QuicPacketCodec.Encode(packet);
        Diagnostics.Diff("re-encoded datagram", datagram, reencoded);
        CollectionAssert.AreEqual(datagram, reencoded);
    }

    [TestMethod]
    public void Decode_Rfc9001AppendixA5ShortHeader_ReadsThePacketNumberAndPing()
    {
        var datagram = Hex("4200bff4" + "01");
        Diagnostics.Arrange("RFC 9001 A.5 short header datagram hex", "4200bff401");
        Diagnostics.Bytes("short header datagram", datagram);

        var decoded = QuicPacketCodec.Decode(datagram, 0);

        var packet = (QuicShortHeaderPacket)decoded.Packet;
        var packetNumber = QuicPacketNumber.Decode(654360563, packet.TruncatedPacketNumber, packet.PacketNumberLength);
        var frame = QuicFrameCodec.Decode(packet.Payload, QuicPacketType.OneRtt).Single();
        Diagnostics.Act("packet number length", packet.PacketNumberLength);
        Diagnostics.Act("decoded packet number", packetNumber);
        Diagnostics.Act("spin bit", packet.SpinBit);
        Diagnostics.Act("key phase", packet.KeyPhase);
        Diagnostics.Act("header length", decoded.HeaderLength);
        Diagnostics.Act("payload frame", frame.Type);
        Diagnostics.Assert("packet number length", 3, packet.PacketNumberLength);
        Assert.AreEqual(3, packet.PacketNumberLength);
        Diagnostics.Assert("decoded packet number", 654360564UL, packetNumber);
        Assert.AreEqual(654360564UL, QuicPacketNumber.Decode(654360563, packet.TruncatedPacketNumber, packet.PacketNumberLength));
        Diagnostics.Assert("spin bit", false, packet.SpinBit);
        Assert.IsFalse(packet.SpinBit);
        Diagnostics.Assert("key phase", false, packet.KeyPhase);
        Assert.IsFalse(packet.KeyPhase);
        Diagnostics.Assert("header length", 4, decoded.HeaderLength);
        Assert.AreEqual(4, decoded.HeaderLength);
        Diagnostics.Assert("payload frame is ping", true, frame is QuicPingFrame);
        Assert.IsInstanceOfType<QuicPingFrame>(QuicFrameCodec.Decode(packet.Payload, QuicPacketType.OneRtt).Single());
        var reencoded = QuicPacketCodec.Encode(packet);
        Diagnostics.Diff("re-encoded datagram", datagram, reencoded);
        CollectionAssert.AreEqual(datagram, reencoded);
    }

    public static IEnumerable<object[]> EveryPacket =>
    [
        [new QuicLongHeaderPacket(QuicPacketType.Initial, 1, Hex("0102"), Hex("03"), Hex("aabb"), 1, 7, Hex("01"), ReservedBits: 2), "c8000000010201020103" + "02aabb" + "4002" + "07" + "01"],
        [new QuicLongHeaderPacket(QuicPacketType.ZeroRtt, 1, Hex("0102"), Hex("03"), ReadOnlyMemory<byte>.Empty, 2, 7, Hex("01")), "d10000000102010201034003000701"],
        [new QuicLongHeaderPacket(QuicPacketType.Handshake, 1, Hex("0102"), Hex("03"), ReadOnlyMemory<byte>.Empty, 1, 7, new byte[16384]), "e00000000102010201038000400107" + new string('0', 32768)],
        [new QuicRetryPacket(1, Hex("01"), Hex("02"), Hex("aa"), new byte[16]), "f00000000101010102aa" + new string('0', 32)],
        [new QuicVersionNegotiationPacket(Hex("01"), new byte[21], [1, 0x6b3343cf]), "c0" + "00000000" + "0101" + "15" + new string('0', 42) + "00000001" + "6b3343cf"],
        [new QuicShortHeaderPacket(Hex("0102"), 2, 0x1234, Hex("01"), SpinBit: true, KeyPhase: true, ReservedBits: 1), "6d0102123401"],
    ];

    [TestMethod]
    [DynamicData(nameof(EveryPacket))]
    public void EncodeThenDecode_EveryPacketType_WritesTheRfcLayoutAndReadsItBack(QuicPacket packet, string expectedHex)
    {
        Diagnostics.Arrange("packet type", packet.GetType().Name);
        Diagnostics.Arrange("expected hex length", expectedHex.Length);

        var encoded = QuicPacketCodec.Encode(packet);

        Diagnostics.Bytes($"encoded {packet.GetType().Name}", encoded);
        Diagnostics.Diff("encoded hex", expectedHex, HexOf(encoded));
        Assert.AreEqual(expectedHex, HexOf(encoded));
        var decoded = QuicPacketCodec.Decode(encoded, 2);
        Diagnostics.Act("decoded packet type", decoded.Packet.GetType().Name);
        Diagnostics.Act("decoded length", decoded.Length);
        Diagnostics.Assert("decoded packet type", packet.GetType(), decoded.Packet.GetType());
        Assert.AreEqual(packet.GetType(), decoded.Packet.GetType());
        Diagnostics.Assert("decoded length", encoded.Length, decoded.Length);
        Assert.AreEqual(encoded.Length, decoded.Length);
        var reencodedHex = HexOf(QuicPacketCodec.Encode(decoded.Packet));
        Diagnostics.Diff("re-encoded hex", expectedHex, reencodedHex);
        Assert.AreEqual(expectedHex, reencodedHex);
    }

    [TestMethod]
    public void Decode_CoalescedPackets_StopsAtTheFirstOnesLength()
    {
        var first = QuicPacketCodec.Encode(new QuicLongHeaderPacket(QuicPacketType.Handshake, 1, Hex("01"), Hex("02"), ReadOnlyMemory<byte>.Empty, 1, 0, Hex("01")));
        var datagram = first.Concat(Hex("4001" + "01")).ToArray();
        Diagnostics.Arrange("first packet type", QuicPacketType.Handshake);
        Diagnostics.Arrange("short header connection ID length", 1);
        Diagnostics.Bytes("coalesced datagram", datagram);

        var decoded = QuicPacketCodec.Decode(datagram, 1);

        var second = QuicPacketCodec.Decode(datagram.AsMemory(decoded.Length), 1).Packet;
        Diagnostics.Act("first packet length", decoded.Length);
        Diagnostics.Act("second packet type", second.GetType().Name);
        Diagnostics.Assert("first packet length", first.Length, decoded.Length);
        Assert.AreEqual(first.Length, decoded.Length);
        Diagnostics.Assert("second packet is short header", true, second is QuicShortHeaderPacket);
        Assert.IsInstanceOfType<QuicShortHeaderPacket>(QuicPacketCodec.Decode(datagram.AsMemory(decoded.Length), 1).Packet);
    }

    [TestMethod]
    public void Decode_VersionNegotiation_ReadsTheVersions()
    {
        Diagnostics.Arrange("Version Negotiation datagram hex", "80" + "00000000" + "0100" + "00" + "00000001" + "6b3343cf");
        Diagnostics.Bytes("Version Negotiation datagram", Hex("80" + "00000000" + "0100" + "00" + "00000001" + "6b3343cf"));

        var packet = (QuicVersionNegotiationPacket)QuicPacketCodec.Decode(Hex("80" + "00000000" + "0100" + "00" + "00000001" + "6b3343cf"), 0).Packet;

        var versions = string.Join(", ", packet.SupportedVersions.ToArray().Select(version => version.ToString("x8", System.Globalization.CultureInfo.InvariantCulture)));
        Diagnostics.Act("supported versions", versions);
        Diagnostics.Act("unused bits", packet.UnusedBits);
        Diagnostics.Assert("supported versions", "00000001, 6b3343cf", versions);
        CollectionAssert.AreEqual(new uint[] { 1, 0x6b3343cf }, packet.SupportedVersions.ToArray());
        Diagnostics.Assert("unused bits", 0, packet.UnusedBits);
        Assert.AreEqual(0, packet.UnusedBits);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty datagram")]
    [DataRow("0200bff401", DisplayName = "short header with the fixed bit clear")]
    [DataRow("4200", DisplayName = "short header ending in its packet number")]
    [DataRow("c3000000", DisplayName = "long header ending in its version")]
    [DataRow("8300000001080102030405060708000000", DisplayName = "long header with the fixed bit clear")]
    [DataRow("c3000000020000000100", DisplayName = "version 2")]
    [DataRow("c30000000115", DisplayName = "21-byte destination connection ID")]
    [DataRow("c3000000010015", DisplayName = "21-byte source connection ID")]
    [DataRow("c300000001010100", DisplayName = "Initial ending before its token length")]
    [DataRow("c30000000100000003000000", DisplayName = "Length shorter than the packet number")]
    [DataRow("c3000000010000000900000000", DisplayName = "Length beyond the datagram")]
    [DataRow("f0000000010000112233445566778899aabbccddeeff00", DisplayName = "Retry with an empty token")]
    [DataRow("80000000000000000001", DisplayName = "Version Negotiation with a partial version")]
    [DataRow("800000000005", DisplayName = "Version Negotiation ending in a connection ID")]
    public void Decode_MalformedPacket_IsAProtocolViolation(string hex)
    {
        Diagnostics.Arrange("datagram hex", hex);
        Diagnostics.Bytes("datagram", Hex(hex));

        var error = ErrorOf(() => QuicPacketCodec.Decode(Hex(hex), 0));

        Diagnostics.Act("transport error code", error);
        Diagnostics.Assert("transport error code", QuicTransportErrorCode.ProtocolViolation, error);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, error);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(21)]
    public void Decode_ShortHeaderConnectionIdLengthOutOfRange_Throws(int length)
    {
        Diagnostics.Arrange("datagram hex", "40");
        Diagnostics.Arrange("short header connection ID length", length);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketCodec.Decode(Hex("40"), length));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void Encode_Null_Throws()
    {
        Diagnostics.Arrange("packet", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => QuicPacketCodec.Encode(null!));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    public static IEnumerable<object[]> UnwritablePackets =>
    [
        [new QuicLongHeaderPacket(QuicPacketType.Retry, 1, Hex("01"), Hex("02"), ReadOnlyMemory<byte>.Empty, 1, 0, Hex("01"))],
        [new QuicLongHeaderPacket(QuicPacketType.Handshake, 1, Hex("01"), Hex("02"), Hex("aa"), 1, 0, Hex("01"))],
        [new QuicLongHeaderPacket(QuicPacketType.Initial, 1, Hex("01"), Hex("02"), ReadOnlyMemory<byte>.Empty, 0, 0, Hex("01"))],
        [new QuicLongHeaderPacket(QuicPacketType.Initial, 1, Hex("01"), Hex("02"), ReadOnlyMemory<byte>.Empty, 5, 0, Hex("01"))],
        [new QuicLongHeaderPacket(QuicPacketType.Initial, 1, new byte[21], Hex("02"), ReadOnlyMemory<byte>.Empty, 1, 0, Hex("01"))],
        [new QuicRetryPacket(1, Hex("01"), Hex("02"), Hex("aa"), new byte[15])],
        [new QuicVersionNegotiationPacket(new byte[256], Hex("02"), [1])],
        [new QuicShortHeaderPacket(new byte[21], 1, 0, Hex("01"))],
        [new QuicShortHeaderPacket(Hex("01"), 0, 0, Hex("01"))],
        [new QuicShortHeaderPacket(Hex("01"), 5, 0, Hex("01"))],
    ];

    [TestMethod]
    [DynamicData(nameof(UnwritablePackets))]
    public void Encode_FieldOutOfRangeForTheHeader_ThrowsArgumentException(QuicPacket packet)
    {
        Diagnostics.Arrange("packet type", packet.GetType().Name);

        var exception = Assert.Throws<ArgumentException>(() => QuicPacketCodec.Encode(packet));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.ParamName}");
        Diagnostics.Assert("exception is ArgumentException", true, exception is ArgumentException);
    }
}
