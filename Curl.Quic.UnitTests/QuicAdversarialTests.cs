using Curl.Testing;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

/// <summary>
/// Adversarial black-box tests of <c>Curl.Quic.UnitLibrary</c>'s public codecs and
/// reassembler (BL-1522, by the method in <c>Documentation/Wiki/Adversarial-Testing.md</c>):
/// every limit RFC 9000 sets, on both sides and exactly on it; malformed and truncated
/// frames, packets and transport parameters, which must be refused with a
/// <see cref="QuicTransportException" /> and never another exception; frames in packet types
/// that may not carry them; and CRYPTO data delivered out of order and repeated.
/// </summary>
[TestClass]
public sealed class QuicAdversarialTests
{
    private const ulong Maximum = QuicVariableLengthInteger.MaximumValue;

    /// <summary>RFC 9001 Appendix A.2: the client Initial's unprotected header.</summary>
    private const string ClientInitialHeader = "c300000001088394c8f03e5157080000449e00000002";

    /// <summary>RFC 9001 Appendix A.4: the Retry packet, and the client's original destination connection ID.</summary>
    private const string RetryPacket = "ff000000010008f067a5502a4262b5746f6b656e04a265ba2eff4d829058fb3f0f2496ba";

    private const string RetryOriginalDestinationConnectionId = "8394c8f03e515708";

    /// <summary>A payload of one frame of each kind a 1-RTT packet carries, each with a multi-byte field.</summary>
    private const string EveryKindOfFramePayload =
        "030a03010201020405 06" + "0404074064" + "06404002aabb" + "0701cc" + "0f040901dd" + "1043e8" + "110443e8" +
        "124064" + "18020108 0102030405060708 00112233445566778899aabbccddeeff" + "1902" + "1aa1a2a3a4a5a6a7a8" +
        "1d410003627965" + "1e";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TryRead_EightByteEncodingOfTheMaximum_ReadsTwoToThe62MinusOne()
    {
        Diagnostics.Arrange("encoding", "ffffffffffffffff");

        var read = QuicVariableLengthInteger.TryRead(Hex("ffffffffffffffff"), out var value, out var bytesRead);

        Diagnostics.Act("value", value);
        Assert.IsTrue(read);
        Assert.AreEqual(Maximum, value);
        Assert.AreEqual(8, bytesRead);
    }

    [TestMethod]
    [DataRow(1, DisplayName = "1 of 8 bytes")]
    [DataRow(4, DisplayName = "4 of 8 bytes")]
    [DataRow(7, DisplayName = "7 of 8 bytes")]
    public void TryRead_MaximumCutShortOfItsEightBytes_IsFalse(int length)
    {
        Diagnostics.Arrange("bytes present", length);

        var read = QuicVariableLengthInteger.TryRead(Hex("ffffffffffffffff").AsSpan(0, length), out var value, out var bytesRead);

        Diagnostics.Act("read", read);
        Assert.IsFalse(read);
        Assert.AreEqual(0UL, value);
        Assert.AreEqual(0, bytesRead);
    }

    [TestMethod]
    public void Write_TheMaximum_WritesEightBytesAndOneMoreThrows()
    {
        var destination = new byte[8];

        var written = QuicVariableLengthInteger.Write(Maximum, destination);

        Diagnostics.Bytes("written", destination);
        Assert.AreEqual(8, written);
        Assert.AreEqual("ffffffffffffffff", HexOf(destination));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicVariableLengthInteger.Write(Maximum + 1, destination));
    }

    [TestMethod]
    [DataRow(Maximum, 0, DisplayName = "offset 2^62-1 with no data")]
    [DataRow(Maximum - 1, 1, DisplayName = "offset 2^62-2 with one byte")]
    public void Decode_StreamEndingExactlyAt2To62MinusOne_IsRead(ulong offset, int length)
    {
        var payload = QuicFrameCodec.Encode([new QuicStreamFrame(4, offset, new byte[length], IsFin: true)]);
        Diagnostics.Bytes("payload", payload);

        var stream = (QuicStreamFrame)QuicFrameCodec.Decode(payload, QuicPacketType.OneRtt).Single();

        Diagnostics.Act("offset", stream.Offset);
        Assert.AreEqual(offset, stream.Offset);
        Assert.AreEqual(length, stream.Data.Length);
    }

    [TestMethod]
    [DataRow(Maximum, 1, DisplayName = "offset 2^62-1 with one byte")]
    [DataRow(Maximum - 1, 2, DisplayName = "offset 2^62-2 with two bytes")]
    public void Decode_StreamEndingOneBytePast2To62MinusOne_IsAFrameEncodingError(ulong offset, int length)
    {
        var payload = QuicFrameCodec.Encode([new QuicStreamFrame(4, offset, new byte[length], IsFin: false)]);
        Diagnostics.Bytes("payload", payload);

        var error = ErrorOf(() => QuicFrameCodec.Decode(payload, QuicPacketType.OneRtt));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, error);
    }

    [TestMethod]
    public void Decode_CryptoAtTheOffsetLimit_IsReadAndOneBytePastIsAFrameEncodingError()
    {
        var atLimit = QuicFrameCodec.Encode([new QuicCryptoFrame(Maximum - 1, new byte[1])]);
        var pastLimit = QuicFrameCodec.Encode([new QuicCryptoFrame(Maximum, new byte[1])]);
        Diagnostics.Bytes("at the limit", atLimit);
        Diagnostics.Bytes("one byte past", pastLimit);

        var crypto = (QuicCryptoFrame)QuicFrameCodec.Decode(atLimit, QuicPacketType.Initial).Single();
        var error = ErrorOf(() => QuicFrameCodec.Decode(pastLimit, QuicPacketType.Initial));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(Maximum - 1, crypto.Offset);
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, error);
    }

    [TestMethod]
    [DataRow("020500 0005", 0UL, DisplayName = "first range down to packet 0")]
    [DataRow("020a00 0102 0006", 0UL, DisplayName = "second range down to packet 0")]
    [DataRow("02" + "ffffffffffffffff" + "00 00" + "ffffffffffffffff", 0UL, DisplayName = "largest 2^62-1 acknowledged down to 0")]
    public void Decode_AckRangeReachingExactlyPacketZero_IsRead(string hex, ulong expectedSmallest)
    {
        Diagnostics.Arrange("payload hex", hex);

        var ack = (QuicAckFrame)QuicFrameCodec.Decode(Hex(hex), QuicPacketType.OneRtt).Single();

        var ranges = ack.GetAcknowledgedRanges();
        Diagnostics.Act("ranges", string.Join(", ", ranges));
        Assert.AreEqual(expectedSmallest, ranges[^1].Smallest);
    }

    [TestMethod]
    [DataRow("020500 0006", DisplayName = "first range one below packet 0")]
    [DataRow("020a00 0102 0007", DisplayName = "second range one below packet 0")]
    [DataRow("020100 0100 00", DisplayName = "gap underflowing below packet 0")]
    [DataRow("02" + "00" + "00" + "01" + "00" + "ffffffffffffffff" + "00", DisplayName = "gap of 2^62-1 after packet 0")]
    public void Decode_AckRangeUnderflowingPacketZero_IsAFrameEncodingError(string hex)
    {
        Diagnostics.Arrange("payload hex", hex);

        var error = ErrorOf(() => QuicFrameCodec.Decode(Hex(hex), QuicPacketType.OneRtt));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, error);
    }

    [TestMethod]
    public void Decode_AckClaiming2To62MinusOneRangesWithNoneSent_IsAFrameEncodingErrorWithoutLooping()
    {
        const string Hex62 = "020a00" + "ffffffffffffffff" + "00";
        Diagnostics.Arrange("payload hex", Hex62);

        var error = ErrorOf(() => QuicFrameCodec.Decode(Hex(Hex62), QuicPacketType.OneRtt));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, error);
    }

    [TestMethod]
    [DataRow("1f", DisplayName = "0x1f, one past HANDSHAKE_DONE")]
    [DataRow("30", DisplayName = "0x30, DATAGRAM (RFC 9221), not negotiated")]
    [DataRow("4100", DisplayName = "0x100 in two bytes")]
    [DataRow("bfffffff", DisplayName = "0x3fffffff in four bytes")]
    [DataRow("ffffffffffffffff", DisplayName = "2^62-1 in eight bytes")]
    public void Decode_UnknownFrameType_IsAFrameEncodingError(string hex)
    {
        Diagnostics.Arrange("payload hex", hex);

        var error = ErrorOf(() => QuicFrameCodec.Decode(Hex(hex), QuicPacketType.OneRtt));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, error);
    }

    [TestMethod]
    public void Decode_MaxStreamsAt2To60_IsReadAndOneMoreIsAFrameEncodingError()
    {
        var atLimit = QuicFrameCodec.Encode([new QuicMaxStreamsFrame(IsUnidirectional: false, QuicFrameCodec.MaximumStreamCount)]);
        var pastLimit = QuicFrameCodec.Encode([new QuicStreamsBlockedFrame(IsUnidirectional: true, QuicFrameCodec.MaximumStreamCount + 1)]);
        Diagnostics.Bytes("MAX_STREAMS 2^60", atLimit);
        Diagnostics.Bytes("STREAMS_BLOCKED 2^60+1", pastLimit);

        var maxStreams = (QuicMaxStreamsFrame)QuicFrameCodec.Decode(atLimit, QuicPacketType.OneRtt).Single();
        var error = ErrorOf(() => QuicFrameCodec.Decode(pastLimit, QuicPacketType.OneRtt));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicFrameCodec.MaximumStreamCount, maxStreams.MaximumStreams);
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, error);
    }

    [TestMethod]
    [DataRow(1, DisplayName = "1-byte connection ID")]
    [DataRow(20, DisplayName = "20-byte connection ID")]
    public void Decode_NewConnectionIdAtEachLengthLimit_IsRead(int length)
    {
        var payload = QuicFrameCodec.Encode([new QuicNewConnectionIdFrame(1, 0, new byte[length], new byte[16])]);
        Diagnostics.Bytes("payload", payload);

        var frame = (QuicNewConnectionIdFrame)QuicFrameCodec.Decode(payload, QuicPacketType.OneRtt).Single();

        Diagnostics.Act("connection ID length", frame.ConnectionId.Length);
        Assert.AreEqual(length, frame.ConnectionId.Length);
    }

    [TestMethod]
    [DataRow("18010000" + "00112233445566778899aabbccddeeff", DisplayName = "0-byte connection ID")]
    [DataRow("18010001aa" + "00112233445566778899aabbccddee", DisplayName = "15-byte stateless reset token")]
    [DataRow("180001" + "01aa" + "00112233445566778899aabbccddeeff", DisplayName = "Retire Prior To above Sequence Number")]
    public void Decode_NewConnectionIdOneFieldWrong_IsAFrameEncodingError(string hex)
    {
        Diagnostics.Arrange("payload hex", hex);

        var error = ErrorOf(() => QuicFrameCodec.Decode(Hex(hex), QuicPacketType.OneRtt));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, error);
    }

    [TestMethod]
    [DataRow("0701cc", QuicPacketType.Handshake, DisplayName = "NEW_TOKEN in Handshake")]
    [DataRow("0701cc", QuicPacketType.ZeroRtt, DisplayName = "NEW_TOKEN in 0-RTT")]
    [DataRow("06000101", QuicPacketType.ZeroRtt, DisplayName = "CRYPTO in 0-RTT")]
    [DataRow("0404074064", QuicPacketType.Initial, DisplayName = "RESET_STREAM in Initial")]
    [DataRow("1043e8", QuicPacketType.Handshake, DisplayName = "MAX_DATA in Handshake")]
    [DataRow("1902", QuicPacketType.Initial, DisplayName = "RETIRE_CONNECTION_ID in Initial")]
    public void Decode_FrameInAPacketTypeThatMayNotCarryIt_IsAProtocolViolation(string hex, QuicPacketType packetType)
    {
        Diagnostics.Arrange("payload hex", hex);
        Diagnostics.Arrange("packet type", packetType);

        var error = ErrorOf(() => QuicFrameCodec.Decode(Hex(hex), packetType));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, error);
    }

    [TestMethod]
    public void Decode_EveryProperPrefixOfAPayload_IsReadOrRefusedWithAFrameEncodingError()
    {
        var payload = Hex(EveryKindOfFramePayload);
        var whole = QuicFrameCodec.Decode(payload, QuicPacketType.OneRtt);
        Diagnostics.Arrange("frames in the whole payload", whole.Count);

        for (var length = 1; length < payload.Length; length++)
        {
            try
            {
                QuicFrameCodec.Decode(payload.AsMemory(0, length), QuicPacketType.OneRtt);
            }
            catch (QuicTransportException exception)
            {
                Assert.AreEqual(QuicTransportErrorCode.FrameEncodingError, exception.ErrorCode, $"prefix of {length} bytes");
            }
        }

        Assert.HasCount(13, whole);
    }

    [TestMethod]
    public void Decode_RandomFramePayloads_NeverThrowAnythingButATransportException()
    {
        const int Seed = 1522;
        var random = new Random(Seed);
        Diagnostics.Arrange("seed", Seed);
        QuicPacketType[] packetTypes = [QuicPacketType.Initial, QuicPacketType.ZeroRtt, QuicPacketType.Handshake, QuicPacketType.OneRtt];

        for (var iteration = 0; iteration < 20000; iteration++)
        {
            var payload = new byte[random.Next(1, 48)];
            random.NextBytes(payload);
            // Bias the first byte to a known frame type, so most payloads reach a frame's fields.
            payload[0] = (byte)random.Next(0, 0x20);
            var packetType = packetTypes[random.Next(packetTypes.Length)];
            DecodeExpectingOnlyTransportErrors(() => QuicFrameCodec.Decode(payload, packetType), Seed, iteration, payload);
        }
    }

    [TestMethod]
    [DataRow(0, DisplayName = "0-byte connection IDs")]
    [DataRow(20, DisplayName = "20-byte connection IDs")]
    public void Decode_LongHeaderWithConnectionIdsAtEachLengthLimit_IsRead(int length)
    {
        var packet = new QuicLongHeaderPacket(QuicPacketType.Handshake, QuicPacketCodec.Version1, new byte[length], new byte[length], ReadOnlyMemory<byte>.Empty, 1, 0, Hex("01"), 0);
        var datagram = QuicPacketCodec.Encode(packet);
        Diagnostics.Bytes("datagram", datagram);

        var decoded = (QuicLongHeaderPacket)QuicPacketCodec.Decode(datagram, 0).Packet;

        Diagnostics.Act("destination connection ID length", decoded.DestinationConnectionId.Length);
        Assert.AreEqual(length, decoded.DestinationConnectionId.Length);
        Assert.AreEqual(length, decoded.SourceConnectionId.Length);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "0-byte connection ID")]
    [DataRow(20, DisplayName = "20-byte connection ID")]
    public void Decode_ShortHeaderWithConnectionIdAtEachLengthLimit_IsRead(int length)
    {
        var datagram = Hex("40" + new string('a', 2 * length) + "07" + "01");
        Diagnostics.Bytes("datagram", datagram);

        var decoded = QuicPacketCodec.Decode(datagram, length);

        var packet = (QuicShortHeaderPacket)decoded.Packet;
        Diagnostics.Act("header length", decoded.HeaderLength);
        Assert.AreEqual(length, packet.DestinationConnectionId.Length);
        Assert.AreEqual(7u, packet.TruncatedPacketNumber);
        Assert.AreEqual(2 + length, decoded.HeaderLength);
    }

    [TestMethod]
    public void Decode_ShortHeaderWithA21ByteConnectionIdLength_Throws()
    {
        var datagram = Hex("40" + new string('a', 42) + "07");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketCodec.Decode(datagram, 21));
    }

    [TestMethod]
    public void Decode_CoalescedInitialHandshakeAndShortHeader_ReadsEachFromWhatIsLeft()
    {
        var initial = QuicPacketCodec.Encode(new QuicLongHeaderPacket(QuicPacketType.Initial, QuicPacketCodec.Version1, Hex("aa"), Hex("bb"), Hex("cc"), 1, 0, Hex("01"), 0));
        var handshake = QuicPacketCodec.Encode(new QuicLongHeaderPacket(QuicPacketType.Handshake, QuicPacketCodec.Version1, Hex("aa"), Hex("bb"), ReadOnlyMemory<byte>.Empty, 2, 1, Hex("01"), 0));
        byte[] shortHeader = Hex("41aa000201");
        ReadOnlyMemory<byte> datagram = initial.Concat(handshake).Concat(shortHeader).ToArray();
        Diagnostics.Bytes("datagram", datagram.Span);

        var first = QuicPacketCodec.Decode(datagram, 1);
        var second = QuicPacketCodec.Decode(datagram[first.Length..], 1);
        var third = QuicPacketCodec.Decode(datagram[(first.Length + second.Length)..], 1);

        Diagnostics.Act("lengths", $"{first.Length}, {second.Length}, {third.Length}");
        Assert.AreEqual(initial.Length, first.Length);
        Assert.AreEqual(handshake.Length, second.Length);
        Assert.AreEqual(shortHeader.Length, third.Length);
        Assert.AreEqual(QuicPacketType.Initial, ((QuicLongHeaderPacket)first.Packet).Type);
        Assert.AreEqual(QuicPacketType.Handshake, ((QuicLongHeaderPacket)second.Packet).Type);
        Assert.AreEqual(2u, ((QuicShortHeaderPacket)third.Packet).TruncatedPacketNumber);
    }

    [TestMethod]
    public void Decode_EveryProperPrefixOfTheRfcClientInitial_IsAProtocolViolation()
    {
        // The header's Length, 0x449e, is the two-byte encoding of 1182: a 4-byte packet number and 1178 bytes of payload.
        var datagram = Hex(ClientInitialHeader + new string('0', 2 * 1178));
        Diagnostics.Arrange("datagram length", datagram.Length);

        for (var length = 0; length < datagram.Length; length++)
        {
            var error = ErrorOf(() => QuicPacketCodec.Decode(datagram.AsMemory(0, length), 0));
            Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, error, $"prefix of {length} bytes");
        }

        Assert.AreEqual(datagram.Length, QuicPacketCodec.Decode(datagram, 0).Length);
    }

    [TestMethod]
    public void Decode_RetryWithExactlyAnIntegrityTagAfterItsConnectionIds_IsAProtocolViolation()
    {
        const string Retry = "f0000000010000" + "00112233445566778899aabbccddeeff";
        Diagnostics.Arrange("datagram hex", Retry);

        var error = ErrorOf(() => QuicPacketCodec.Decode(Hex(Retry), 0));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, error);
    }

    [TestMethod]
    public void Decode_RetryWithAOneByteToken_IsRead()
    {
        const string Retry = "f0000000010000" + "aa" + "00112233445566778899aabbccddeeff";

        var retry = (QuicRetryPacket)QuicPacketCodec.Decode(Hex(Retry), 0).Packet;

        Diagnostics.Act("token", HexOf(retry.RetryToken));
        Assert.AreEqual("aa", HexOf(retry.RetryToken));
    }

    [TestMethod]
    public void HasValidTag_RfcRetryWithEachBitOfItsTagFlipped_IsFalse()
    {
        var odcid = Hex(RetryOriginalDestinationConnectionId);
        var retry = (QuicRetryPacket)QuicPacketCodec.Decode(Hex(RetryPacket), 0).Packet;
        Assert.IsTrue(QuicRetryIntegrity.HasValidTag(odcid, retry));

        for (var bit = 0; bit < 8 * QuicRetryPacket.RetryIntegrityTagLength; bit++)
        {
            var tag = retry.RetryIntegrityTag.ToArray();
            tag[bit / 8] ^= (byte)(1 << (bit % 8));
            Assert.IsFalse(QuicRetryIntegrity.HasValidTag(odcid, retry with { RetryIntegrityTag = tag }), $"bit {bit}");
        }
    }

    [TestMethod]
    public void HasValidTag_RfcRetryCheckedAgainstAnotherOriginalConnectionId_IsFalse()
    {
        var retry = (QuicRetryPacket)QuicPacketCodec.Decode(Hex(RetryPacket), 0).Packet;
        var odcid = Hex(RetryOriginalDestinationConnectionId);
        odcid[^1] ^= 1;

        var valid = QuicRetryIntegrity.HasValidTag(odcid, retry);

        Diagnostics.Act("valid", valid);
        Assert.IsFalse(valid);
    }

    [TestMethod]
    public void ComputeTag_A21ByteOriginalConnectionId_Throws()
    {
        var retry = (QuicRetryPacket)QuicPacketCodec.Decode(Hex(RetryPacket), 0).Packet;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicRetryIntegrity.ComputeTag(new byte[21], retry));
    }

    [TestMethod]
    [DataRow("80000000000000", 0, DisplayName = "no versions")]
    [DataRow("8000000000" + "14" + "0000000000000000000000000000000000000000" + "00" + "00000001", 1, DisplayName = "20-byte destination connection ID")]
    [DataRow("ff000000000000" + "6b3343cf" + "00000001", 2, DisplayName = "first byte all ones")]
    public void Decode_VersionNegotiationAtItsLimits_ReadsEveryVersion(string hex, int versionCount)
    {
        Diagnostics.Arrange("datagram hex", hex);

        var packet = (QuicVersionNegotiationPacket)QuicPacketCodec.Decode(Hex(hex), 0).Packet;

        Diagnostics.Act("versions", string.Join(", ", packet.SupportedVersions));
        Assert.HasCount(versionCount, packet.SupportedVersions);
    }

    [TestMethod]
    public void Decode_RandomDatagrams_NeverThrowAnythingButATransportException()
    {
        const int Seed = 15220;
        var random = new Random(Seed);
        Diagnostics.Arrange("seed", Seed);
        byte[] firstBytes = [0x40, 0x41, 0x43, 0xc0, 0xc3, 0xd0, 0xe0, 0xf0, 0x80, 0x00];

        for (var iteration = 0; iteration < 20000; iteration++)
        {
            var datagram = new byte[random.Next(1, 64)];
            random.NextBytes(datagram);
            datagram[0] = firstBytes[random.Next(firstBytes.Length)];
            if (datagram.Length >= 5 && datagram[0] >= 0xc0)
            {
                // Mostly version 1, so most long headers get past the version check.
                datagram[1] = datagram[2] = datagram[3] = 0;
                datagram[4] = (byte)(random.Next(4) == 0 ? 0 : 1);
                if (datagram.Length >= 6)
                {
                    datagram[5] = (byte)random.Next(0, 24);
                }
            }

            var connectionIdLength = random.Next(0, 21);
            DecodeExpectingOnlyTransportErrors(() => QuicPacketCodec.Decode(datagram, connectionIdLength), Seed, iteration, datagram);
        }
    }

    [TestMethod]
    [DataRow("03 02 44 b0", 1200UL, DisplayName = "max_udp_payload_size of exactly 1200")]
    [DataRow("0a 01 14", 20UL, DisplayName = "ack_delay_exponent of exactly 20")]
    [DataRow("0b 02 7f ff", 16383UL, DisplayName = "max_ack_delay of 2^14 - 1")]
    [DataRow("0e 01 02", 2UL, DisplayName = "active_connection_id_limit of exactly 2")]
    [DataRow("04 08 ff ff ff ff ff ff ff ff", Maximum, DisplayName = "initial_max_data of 2^62 - 1")]
    public void Decode_IntegerParameterExactlyAtItsLimit_IsRead(string hex, ulong expected)
    {
        Diagnostics.Arrange("parameters hex", hex);

        var parameters = QuicTransportParameters.Decode(Hex(hex));

        ulong[] values = [parameters.MaxUdpPayloadSize, parameters.AckDelayExponent, parameters.MaxAckDelay, parameters.ActiveConnectionIdLimit, parameters.InitialMaxData];
        Diagnostics.Act("values", string.Join(", ", values));
        CollectionAssert.Contains(values, expected);
    }

    [TestMethod]
    [DataRow("09 08 d0 00 00 00 00 00 00 01", DisplayName = "initial_max_streams_uni above 2^60")]
    [DataRow("4040 06 0000000000", DisplayName = "unknown parameter whose length runs past the end")]
    [DataRow("ffffffffffffffff ffffffffffffffff", DisplayName = "unknown parameter of length 2^62-1")]
    [DataRow("01", DisplayName = "parameter ID with no length")]
    [DataRow("01 00", DisplayName = "integer parameter with no value")]
    [DataRow("01 01 40", DisplayName = "integer parameter whose value is cut short")]
    [DataRow("00 15 000000000000000000000000000000000000000000", DisplayName = "21-byte original_destination_connection_id")]
    [DataRow("02 11 0000000000000000000000000000000000", DisplayName = "17-byte stateless reset token")]
    public void Decode_MalformedTransportParameters_IsATransportParameterError(string hex)
    {
        Diagnostics.Arrange("parameters hex", hex);

        var error = ErrorOf(() => QuicTransportParameters.Decode(Hex(hex)));

        Diagnostics.Act("transport error code", error);
        Assert.AreEqual(QuicTransportErrorCode.TransportParameterError, error);
    }

    [TestMethod]
    public void Decode_RandomTransportParameters_NeverThrowAnythingButATransportException()
    {
        const int Seed = 152200;
        var random = new Random(Seed);
        Diagnostics.Arrange("seed", Seed);

        for (var iteration = 0; iteration < 20000; iteration++)
        {
            var encoded = new byte[random.Next(0, 40)];
            random.NextBytes(encoded);
            for (var index = 0; index + 1 < encoded.Length; index += 2 + random.Next(0, 4))
            {
                // Mostly known parameter IDs with short lengths, so most values get read.
                encoded[index] = (byte)random.Next(0, 0x12);
                encoded[index + 1] = (byte)random.Next(0, 6);
            }

            DecodeExpectingOnlyTransportErrors(() => QuicTransportParameters.Decode(encoded), Seed, iteration, encoded);
        }
    }

    [TestMethod]
    public void Receive_FrameEndingExactlyAtTheBufferLimit_IsHeldAndOneBytePastThrows()
    {
        var reassembler = new QuicCryptoReassembler();

        var heldBack = reassembler.Receive(new QuicCryptoFrame(1, new byte[QuicCryptoReassembler.MaximumBufferedBytes - 1]));
        var error = ErrorOf(() => reassembler.Receive(new QuicCryptoFrame(1, new byte[QuicCryptoReassembler.MaximumBufferedBytes])));
        var delivered = reassembler.Receive(new QuicCryptoFrame(0, new byte[1]));

        Diagnostics.Act("delivered bytes", delivered.Length);
        Assert.IsEmpty(heldBack);
        Assert.AreEqual(QuicTransportErrorCode.CryptoBufferExceeded, error);
        Assert.AreEqual((int)QuicCryptoReassembler.MaximumBufferedBytes, delivered.Length);
    }

    [TestMethod]
    public void Receive_OneByteFramesInReverseOrder_DeliversEverythingOnceAtTheEnd()
    {
        var reassembler = new QuicCryptoReassembler();
        var data = Enumerable.Range(0, 64).Select(index => (byte)index).ToArray();

        for (var offset = data.Length - 1; offset > 0; offset--)
        {
            Assert.IsEmpty(reassembler.Receive(new QuicCryptoFrame((ulong)offset, data.AsMemory(offset, 1))), $"offset {offset}");
        }

        var delivered = reassembler.Receive(new QuicCryptoFrame(0, data.AsMemory(0, 1)));

        Diagnostics.Bytes("delivered", delivered);
        CollectionAssert.AreEqual(data, delivered);
        Assert.IsEmpty(reassembler.Receive(new QuicCryptoFrame(0, data)));
    }

    [TestMethod]
    public void Receive_OverlappingFramesInShuffledOrderWithRepeats_DeliversTheStreamExactlyOnce()
    {
        const int Seed = 1522;
        var random = new Random(Seed);
        Diagnostics.Arrange("seed", Seed);
        var data = new byte[2000];
        random.NextBytes(data);
        var frames = new List<QuicCryptoFrame>();
        for (var offset = 0; offset < data.Length;)
        {
            // Each frame reaches at least to where the next starts, so the frames cover every byte.
            var step = random.Next(1, 50);
            var length = Math.Min(Math.Max(step, random.Next(1, 120)), data.Length - offset);
            frames.Add(new QuicCryptoFrame((ulong)offset, data.AsMemory(offset, length)));
            offset += step;
        }

        frames.AddRange(frames.Where((_, index) => index % 3 == 0).ToList());
        random.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(frames));
        var reassembler = new QuicCryptoReassembler();
        var delivered = new List<byte>();
        foreach (var frame in frames)
        {
            delivered.AddRange(reassembler.Receive(frame));
        }

        Diagnostics.Act("delivered bytes", delivered.Count);
        CollectionAssert.AreEqual(data, delivered.ToArray());
    }

    [TestMethod]
    public void Decode_SamePayloadOnManyTasksAtOnce_GivesTheSameFramesAsOneAfterAnother()
    {
        var payload = Hex(EveryKindOfFramePayload);
        var expected = HexOf(QuicFrameCodec.Encode(QuicFrameCodec.Decode(payload, QuicPacketType.OneRtt)));

        var results = new string[64];
        Parallel.For(0, results.Length, index => results[index] = HexOf(QuicFrameCodec.Encode(QuicFrameCodec.Decode(payload, QuicPacketType.OneRtt))));

        Diagnostics.Act("distinct results", results.Distinct().Count());
        Assert.IsTrue(results.All(result => result == expected));
    }

    [TestMethod]
    public void Decode_AfterRefusingAPayload_ReadsTheNextOne()
    {
        ErrorOf(() => QuicFrameCodec.Decode(Hex("1f"), QuicPacketType.OneRtt));

        var frames = QuicFrameCodec.Decode(Hex("01"), QuicPacketType.OneRtt);

        Diagnostics.Act("frame", frames.Single());
        Assert.IsInstanceOfType<QuicPingFrame>(frames.Single());
    }

    [TestMethod]
    [DataRow(Maximum - 1, 0xffu, 1, Maximum, DisplayName = "1-byte number reaching 2^62 - 1 exactly")]
    [DataRow(Maximum - 0x10, 0x00u, 1, Maximum - 0xff, DisplayName = "1-byte number that would move past 2^62 - 1 stays a window below")]
    public void Decode_PacketNumberNearTheTwoTo62Ceiling_NeverPassesIt(ulong largest, uint truncated, int length, ulong expected)
    {
        Diagnostics.Arrange("largest packet number", largest);

        var decoded = QuicPacketNumber.Decode(largest, truncated, length);

        Diagnostics.Act("decoded", decoded);
        Assert.IsLessThanOrEqualTo(Maximum, decoded);
        Assert.AreEqual(expected, decoded);
    }

    private void DecodeExpectingOnlyTransportErrors(Action decode, int seed, int iteration, byte[] input)
    {
        try
        {
            decode();
        }
        catch (QuicTransportException)
        {
        }
        catch (Exception exception)
        {
            Diagnostics.Arrange("seed", seed);
            Diagnostics.Arrange("iteration", iteration);
            Diagnostics.Bytes("input", input);
            Assert.Fail($"Seed {seed}, iteration {iteration}, input {Convert.ToHexStringLower(input)}: {exception.GetType().Name}: {exception.Message}");
        }
    }
}
