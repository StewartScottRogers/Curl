using System.Globalization;
using Curl.Testing;

namespace Curl.Quic;

[TestClass]
public sealed class QuicPacketNumberSpaceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TakeFrames_GapsInReceivedNumbers_AcknowledgesEveryRange()
    {
        Diagnostics.Arrange("received packet numbers", "0, 1, 2, 5, 6, 9");
        using QuicPacketNumberSpace space = SpaceWithKeys();
        foreach (ulong packetNumber in new ulong[] { 0, 1, 2, 5, 6, 9 })
        {
            Assert.IsTrue(space.RecordReceived(packetNumber, TimeSpan.Zero));
        }

        space.RequireAcknowledgement();
        QuicAckFrame ack = (QuicAckFrame)space.TakeFrames(0, TimeSpan.Zero).Frames.Single();

        Diagnostics.Act("largest acknowledged", ack.LargestAcknowledged);
        Diagnostics.Act("first ack range", ack.FirstAckRange);
        Diagnostics.Act("ack range count", ack.AckRanges.Count);
        Diagnostics.Assert("largest acknowledged", 9UL, ack.LargestAcknowledged);
        Assert.AreEqual(9UL, ack.LargestAcknowledged);
        Diagnostics.Assert("first ack range", 0UL, ack.FirstAckRange);
        Assert.AreEqual(0UL, ack.FirstAckRange);
        CollectionAssert.AreEqual(new[] { new QuicAckRange(1, 1), new QuicAckRange(1, 2) }, ack.AckRanges.ToArray());
        Diagnostics.Assert("largest received", 9UL, space.LargestReceived);
        Assert.AreEqual(9UL, space.LargestReceived);
    }

    [TestMethod]
    public void RecordReceived_Duplicate_ReturnsFalse()
    {
        Diagnostics.Arrange("packet number recorded twice", 4);
        using QuicPacketNumberSpace space = new(QuicPacketType.Handshake);

        Diagnostics.Assert("largest received before any packet is null", true, space.LargestReceived is null);
        Assert.IsNull(space.LargestReceived);
        var first = space.RecordReceived(4, TimeSpan.Zero);
        var second = space.RecordReceived(4, TimeSpan.Zero);

        Diagnostics.Act("first record result", first);
        Diagnostics.Act("second record result", second);
        Diagnostics.Assert("first record result", true, first);
        Assert.IsTrue(first);
        Diagnostics.Assert("second record result", false, second);
        Assert.IsFalse(second);
    }

    [TestMethod]
    public void HasFramesToSend_NoKeys_IsFalse()
    {
        Diagnostics.Arrange("send protection", "none");
        using QuicPacketNumberSpace space = new(QuicPacketType.OneRtt);
        space.QueueFrame(new QuicPingFrame());

        Diagnostics.Act("has frames to send", space.HasFramesToSend);
        Diagnostics.Assert("has frames to send", false, space.HasFramesToSend);
        Assert.IsFalse(space.HasFramesToSend);
    }

    [TestMethod]
    public void ReceiveAcknowledgement_Smaller_KeepsTheLargest()
    {
        Diagnostics.Arrange("packets taken", 6);
        Diagnostics.Arrange("acknowledged largest values", "5 then 3");
        using QuicPacketNumberSpace space = new(QuicPacketType.Initial);
        for (var packet = 0; packet < 6; packet++)
        {
            space.TakeFrames(0, TimeSpan.Zero);
        }

        space.ReceiveAcknowledgement(new QuicAckFrame(5, 0, 0, [], null));
        space.ReceiveAcknowledgement(new QuicAckFrame(3, 0, 0, [], null));

        Diagnostics.Act("largest acknowledged", space.LargestAcknowledged);
        Diagnostics.Assert("largest acknowledged", 5UL, space.LargestAcknowledged);
        Assert.AreEqual(5UL, space.LargestAcknowledged);
    }

    [TestMethod]
    public void TakeFrames_CryptoBudgetAndRequeue_SendsChunksThenAllAgain()
    {
        Diagnostics.Arrange("crypto data", "01 02 03");
        Diagnostics.Arrange("room per packet", QuicPacketNumberSpace.CryptoFrameOverhead + 2);
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1, 2, 3]);

        QuicCryptoFrame first = (QuicCryptoFrame)space.TakeFrames(QuicPacketNumberSpace.CryptoFrameOverhead + 2, TimeSpan.Zero).Frames.Single();
        QuicCryptoFrame second = (QuicCryptoFrame)space.TakeFrames(QuicPacketNumberSpace.CryptoFrameOverhead + 2, TimeSpan.Zero).Frames.Single();
        Diagnostics.Assert("has frames after both chunks", false, space.HasFramesToSend);
        Assert.IsFalse(space.HasFramesToSend);
        space.RequeueCrypto();
        (List<QuicFrame> frames, ulong packetNumber) = space.TakeFrames(100, TimeSpan.Zero);

        Diagnostics.Act("chunk offsets", $"{first.Offset}, {second.Offset}");
        Diagnostics.Act("requeued data length", ((QuicCryptoFrame)frames.Single()).Data.Length);
        Diagnostics.Act("packet number", packetNumber);
        Diagnostics.Act("next packet number", space.NextPacketNumber);
        Diagnostics.Assert("chunk offsets", "0, 2", $"{first.Offset}, {second.Offset}");
        Assert.AreEqual((0UL, 2UL), (first.Offset, second.Offset));
        Diagnostics.Assert("requeued data length", 3, ((QuicCryptoFrame)frames.Single()).Data.Length);
        Assert.AreEqual(3, ((QuicCryptoFrame)frames.Single()).Data.Length);
        Diagnostics.Assert("packet number", 2UL, packetNumber);
        Assert.AreEqual(2UL, packetNumber);
        Diagnostics.Assert("next packet number", 3UL, space.NextPacketNumber);
        Assert.AreEqual(3UL, space.NextPacketNumber);
    }

    [TestMethod]
    public void Discard_WithFramesWaiting_DropsKeysAndFrames()
    {
        Diagnostics.Arrange("queued", "crypto 01, ping frame, received packet 0 needing an ack");
        QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1]);
        space.QueueFrame(new QuicPingFrame());
        space.RecordReceived(0, TimeSpan.Zero);
        space.RequireAcknowledgement();

        space.Discard();

        Diagnostics.Act("is discarded", space.IsDiscarded);
        Diagnostics.Act("send protection is null", space.SendProtection is null);
        Diagnostics.Act("receive protection is null", space.ReceiveProtection is null);
        Diagnostics.Act("has frames to send", space.HasFramesToSend);
        Diagnostics.Assert("is discarded", true, space.IsDiscarded);
        Assert.IsTrue(space.IsDiscarded);
        Diagnostics.Assert("send protection is null", true, space.SendProtection is null);
        Assert.IsNull(space.SendProtection);
        Diagnostics.Assert("receive protection is null", true, space.ReceiveProtection is null);
        Assert.IsNull(space.ReceiveProtection);
        Diagnostics.Assert("has frames to send", false, space.HasFramesToSend);
        Assert.IsFalse(space.HasFramesToSend);
    }

    private static QuicPacketNumberSpace SpaceWithKeys() => new(QuicPacketType.Initial)
    {
        SendProtection = QuicPacketProtection.CreateClientInitial([1, 2, 3, 4, 5, 6, 7, 8]),
        ReceiveProtection = QuicPacketProtection.CreateServerInitial([1, 2, 3, 4, 5, 6, 7, 8]),
    };

    [TestMethod]
    public void ReceiveAcknowledgement_PacketNeverSent_ThrowsProtocolViolation()
    {
        Diagnostics.Arrange("packets sent", 1);
        Diagnostics.Arrange("acknowledged largest values", "0 then 1");
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueFrame(new QuicPingFrame());
        space.TakeFrames(100, TimeSpan.Zero);

        space.ReceiveAcknowledgement(new QuicAckFrame(0, 0, 0, [], null));

        var error = QuicTest.ErrorOf(() => space.ReceiveAcknowledgement(new QuicAckFrame(1, 0, 0, [], null)));
        Diagnostics.Act("error code", error);
        Diagnostics.Assert("error code", QuicTransportErrorCode.ProtocolViolation, error);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, error);
    }

    [TestMethod]
    public void RequeueLost_EveryKindOfFrame_ResendsStreamControlAndCryptoDataButNotAckPaddingPingOrPath()
    {
        Diagnostics.Arrange("lost frame kinds", "ack, padding, ping, connection close, path challenge, path response, stream, max data, crypto at offset 1");
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1, 2, 3, 4]);
        space.TakeFrames(100, TimeSpan.Zero);
        QuicStreamFrame stream = new(0, 0, new byte[] { 9, 9 }, IsFin: true);
        QuicMaxDataFrame maxData = new(4096);

        space.RequeueLost([
            new QuicAckFrame(0, 0, 0, [], null),
            new QuicPaddingFrame(3),
            new QuicPingFrame(),
            new QuicConnectionCloseFrame(0, 0, ReadOnlyMemory<byte>.Empty),
            new QuicPathChallengeFrame(new byte[8]),
            new QuicPathResponseFrame(new byte[8]),
            stream,
            maxData,
            new QuicCryptoFrame(1, new byte[] { 2, 3 }),
        ]);
        Diagnostics.Assert("has only acknowledgement to send", false, space.HasOnlyAcknowledgementToSend);
        Assert.IsFalse(space.HasOnlyAcknowledgementToSend);
        List<QuicFrame> frames = space.TakeFrames(100, TimeSpan.Zero).Frames;

        Diagnostics.Act("resent frame types", string.Join(", ", frames.Select(frame => frame.GetType().Name)));
        Diagnostics.Assert("resent frame count", 3, frames.Count);
        Assert.HasCount(3, frames);
        Assert.AreEqual(stream, frames[0]);
        Assert.AreEqual(maxData, frames[1]);
        QuicCryptoFrame crypto = (QuicCryptoFrame)frames[2];
        Diagnostics.Act("crypto offset", crypto.Offset);
        Diagnostics.Bytes("crypto data", crypto.Data.Span);
        Diagnostics.Assert("crypto offset", 1UL, crypto.Offset);
        Assert.AreEqual(1UL, crypto.Offset);
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, crypto.Data.ToArray());
        Diagnostics.Assert("has frames to send", false, space.HasFramesToSend);
        Assert.IsFalse(space.HasFramesToSend);
    }

    [TestMethod]
    public void TakeFrames_LostCryptoLargerThanThePacket_SplitsItBeforeNewCrypto()
    {
        Diagnostics.Arrange("lost crypto", "offset 0, 10 bytes");
        Diagnostics.Arrange("new crypto", "offset 10, 2 bytes");
        Diagnostics.Arrange("room per packet", QuicPacketNumberSpace.CryptoFrameOverhead + 4);
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
        space.TakeFrames(100, TimeSpan.Zero);
        space.RequeueLost([new QuicCryptoFrame(0, new byte[10])]);
        space.QueueCrypto([10, 11]);
        const int room = QuicPacketNumberSpace.CryptoFrameOverhead + 4;

        List<QuicCryptoFrame> sent = [];
        while (space.HasFramesToSend)
        {
            sent.AddRange(space.TakeFrames(room, TimeSpan.Zero).Frames.Cast<QuicCryptoFrame>());
        }

        var offsets = string.Join(", ", sent.Select(frame => frame.Offset.ToString(CultureInfo.InvariantCulture)));
        var lengths = string.Join(", ", sent.Select(frame => frame.Data.Length.ToString(CultureInfo.InvariantCulture)));
        Diagnostics.Act("sent offsets", offsets);
        Diagnostics.Act("sent lengths", lengths);
        Diagnostics.Assert("sent offsets", "0, 4, 8, 10", offsets);
        CollectionAssert.AreEqual(new ulong[] { 0, 4, 8, 10 }, sent.Select(frame => frame.Offset).ToArray());
        Diagnostics.Assert("sent lengths", "4, 4, 2, 2", lengths);
        CollectionAssert.AreEqual(new[] { 4, 4, 2, 2 }, sent.Select(frame => frame.Data.Length).ToArray());
        Diagnostics.Bytes("third frame data", sent[2].Data.Span);
        CollectionAssert.AreEqual(new byte[] { 8, 9 }, sent[2].Data.ToArray());
    }

    [TestMethod]
    public void TakeFrames_NoRoomForLostCrypto_KeepsItQueued()
    {
        Diagnostics.Arrange("lost crypto", "offset 0, 1 byte");
        Diagnostics.Arrange("room per packet", QuicPacketNumberSpace.CryptoFrameOverhead);
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1]);
        space.TakeFrames(100, TimeSpan.Zero);
        space.RequeueLost([new QuicCryptoFrame(0, new byte[] { 1 })]);

        var frames = space.TakeFrames(QuicPacketNumberSpace.CryptoFrameOverhead, TimeSpan.Zero).Frames;

        Diagnostics.Act("frames taken", frames.Count);
        Diagnostics.Act("has frames to send", space.HasFramesToSend);
        Diagnostics.Assert("frames taken", 0, frames.Count);
        Assert.IsEmpty(frames);
        Diagnostics.Assert("has frames to send", true, space.HasFramesToSend);
        Assert.IsTrue(space.HasFramesToSend);
    }

    [TestMethod]
    public void RequeueCryptoAndDropUnsentCrypto_LostRanges_AreForgotten()
    {
        Diagnostics.Arrange("crypto data", "01 02");
        Diagnostics.Arrange("lost crypto", "offset 1, 1 byte (twice)");
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1, 2]);
        space.TakeFrames(100, TimeSpan.Zero);

        space.RequeueLost([new QuicCryptoFrame(1, new byte[] { 2 })]);
        space.RequeueCrypto();
        QuicCryptoFrame again = (QuicCryptoFrame)space.TakeFrames(100, TimeSpan.Zero).Frames.Single();
        space.RequeueLost([new QuicCryptoFrame(1, new byte[] { 2 })]);
        space.DropUnsentCrypto();

        Diagnostics.Act("resent crypto offset and length", $"{again.Offset}, {again.Data.Length}");
        Diagnostics.Act("has frames to send", space.HasFramesToSend);
        Diagnostics.Assert("resent crypto offset and length", "0, 2", $"{again.Offset}, {again.Data.Length}");
        Assert.AreEqual((0UL, 2), (again.Offset, again.Data.Length));
        Diagnostics.Assert("has frames to send", false, space.HasFramesToSend);
        Assert.IsFalse(space.HasFramesToSend);
    }

    [TestMethod]
    public void TakeFrames_AcknowledgementDue_CarriesTheDelaySinceTheLargestArrivedScaledByTheExponent()
    {
        Diagnostics.Arrange("packet 5 arrived at ms", 1);
        Diagnostics.Arrange("packet 3 arrived at ms", 2);
        Diagnostics.Arrange("ack sent at ms", 9);
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.RecordReceived(5, TimeSpan.FromMilliseconds(1));
        space.RecordReceived(3, TimeSpan.FromMilliseconds(2));
        space.RequireAcknowledgement();
        Diagnostics.Assert("has only acknowledgement to send", true, space.HasOnlyAcknowledgementToSend);
        Assert.IsTrue(space.HasOnlyAcknowledgementToSend);

        // 8 ms since packet 5 arrived: 8000 microseconds, divided by 2^3 (RFC 9000 section 19.3).
        QuicAckFrame ack = (QuicAckFrame)space.TakeFrames(100, TimeSpan.FromMilliseconds(9)).Frames.Single();
        space.AckDelayExponent = 0;
        space.RequireAcknowledgement();
        QuicAckFrame early = (QuicAckFrame)space.TakeFrames(100, TimeSpan.Zero).Frames.Single();

        Diagnostics.Act("ack delay", ack.AckDelay);
        Diagnostics.Act("early ack delay", early.AckDelay);
        Diagnostics.Assert("ack delay", 1000UL, ack.AckDelay);
        Assert.AreEqual(1000UL, ack.AckDelay);
        Diagnostics.Assert("early ack delay", 0UL, early.AckDelay);
        Assert.AreEqual(0UL, early.AckDelay);
    }

    [TestMethod]
    public void Id_EachPacketType_NamesItsLossRecoverySpace()
    {
        Diagnostics.Arrange("packet types", "Initial, Handshake, OneRtt");
        using QuicPacketNumberSpace initial = new(QuicPacketType.Initial);
        using QuicPacketNumberSpace handshake = new(QuicPacketType.Handshake);
        using QuicPacketNumberSpace application = new(QuicPacketType.OneRtt);

        Diagnostics.Act("space ids", $"{initial.Id}, {handshake.Id}, {application.Id}");
        Diagnostics.Assert("initial id", QuicPacketNumberSpaceId.Initial, initial.Id);
        Assert.AreEqual(QuicPacketNumberSpaceId.Initial, initial.Id);
        Diagnostics.Assert("handshake id", QuicPacketNumberSpaceId.Handshake, handshake.Id);
        Assert.AreEqual(QuicPacketNumberSpaceId.Handshake, handshake.Id);
        Diagnostics.Assert("application id", QuicPacketNumberSpaceId.ApplicationData, application.Id);
        Assert.AreEqual(QuicPacketNumberSpaceId.ApplicationData, application.Id);
    }
}
