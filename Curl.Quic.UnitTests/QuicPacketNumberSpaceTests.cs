namespace Curl.Quic;

[TestClass]
public sealed class QuicPacketNumberSpaceTests
{
    [TestMethod]
    public void TakeFrames_GapsInReceivedNumbers_AcknowledgesEveryRange()
    {
        using QuicPacketNumberSpace space = SpaceWithKeys();
        foreach (ulong packetNumber in new ulong[] { 0, 1, 2, 5, 6, 9 })
        {
            Assert.IsTrue(space.RecordReceived(packetNumber, TimeSpan.Zero));
        }

        space.RequireAcknowledgement();
        QuicAckFrame ack = (QuicAckFrame)space.TakeFrames(0, TimeSpan.Zero).Frames.Single();

        Assert.AreEqual(9UL, ack.LargestAcknowledged);
        Assert.AreEqual(0UL, ack.FirstAckRange);
        CollectionAssert.AreEqual(new[] { new QuicAckRange(1, 1), new QuicAckRange(1, 2) }, ack.AckRanges.ToArray());
        Assert.AreEqual(9UL, space.LargestReceived);
    }

    [TestMethod]
    public void RecordReceived_Duplicate_ReturnsFalse()
    {
        using QuicPacketNumberSpace space = new(QuicPacketType.Handshake);

        Assert.IsNull(space.LargestReceived);
        Assert.IsTrue(space.RecordReceived(4, TimeSpan.Zero));
        Assert.IsFalse(space.RecordReceived(4, TimeSpan.Zero));
    }

    [TestMethod]
    public void HasFramesToSend_NoKeys_IsFalse()
    {
        using QuicPacketNumberSpace space = new(QuicPacketType.OneRtt);
        space.QueueFrame(new QuicPingFrame());

        Assert.IsFalse(space.HasFramesToSend);
    }

    [TestMethod]
    public void ReceiveAcknowledgement_Smaller_KeepsTheLargest()
    {
        using QuicPacketNumberSpace space = new(QuicPacketType.Initial);
        for (var packet = 0; packet < 6; packet++)
        {
            space.TakeFrames(0, TimeSpan.Zero);
        }

        space.ReceiveAcknowledgement(new QuicAckFrame(5, 0, 0, [], null));
        space.ReceiveAcknowledgement(new QuicAckFrame(3, 0, 0, [], null));

        Assert.AreEqual(5UL, space.LargestAcknowledged);
    }

    [TestMethod]
    public void TakeFrames_CryptoBudgetAndRequeue_SendsChunksThenAllAgain()
    {
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1, 2, 3]);

        QuicCryptoFrame first = (QuicCryptoFrame)space.TakeFrames(QuicPacketNumberSpace.CryptoFrameOverhead + 2, TimeSpan.Zero).Frames.Single();
        QuicCryptoFrame second = (QuicCryptoFrame)space.TakeFrames(QuicPacketNumberSpace.CryptoFrameOverhead + 2, TimeSpan.Zero).Frames.Single();
        Assert.IsFalse(space.HasFramesToSend);
        space.RequeueCrypto();
        (List<QuicFrame> frames, ulong packetNumber) = space.TakeFrames(100, TimeSpan.Zero);

        Assert.AreEqual((0UL, 2UL), (first.Offset, second.Offset));
        Assert.AreEqual(3, ((QuicCryptoFrame)frames.Single()).Data.Length);
        Assert.AreEqual(2UL, packetNumber);
        Assert.AreEqual(3UL, space.NextPacketNumber);
    }

    [TestMethod]
    public void Discard_WithFramesWaiting_DropsKeysAndFrames()
    {
        QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1]);
        space.QueueFrame(new QuicPingFrame());
        space.RecordReceived(0, TimeSpan.Zero);
        space.RequireAcknowledgement();

        space.Discard();

        Assert.IsTrue(space.IsDiscarded);
        Assert.IsNull(space.SendProtection);
        Assert.IsNull(space.ReceiveProtection);
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
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueFrame(new QuicPingFrame());
        space.TakeFrames(100, TimeSpan.Zero);

        space.ReceiveAcknowledgement(new QuicAckFrame(0, 0, 0, [], null));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, QuicTest.ErrorOf(() => space.ReceiveAcknowledgement(new QuicAckFrame(1, 0, 0, [], null))));
    }

    [TestMethod]
    public void RequeueLost_EveryKindOfFrame_ResendsStreamControlAndCryptoDataButNotAckPaddingPingOrPath()
    {
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
        Assert.IsFalse(space.HasOnlyAcknowledgementToSend);
        List<QuicFrame> frames = space.TakeFrames(100, TimeSpan.Zero).Frames;

        Assert.HasCount(3, frames);
        Assert.AreEqual(stream, frames[0]);
        Assert.AreEqual(maxData, frames[1]);
        QuicCryptoFrame crypto = (QuicCryptoFrame)frames[2];
        Assert.AreEqual(1UL, crypto.Offset);
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, crypto.Data.ToArray());
        Assert.IsFalse(space.HasFramesToSend);
    }

    [TestMethod]
    public void TakeFrames_LostCryptoLargerThanThePacket_SplitsItBeforeNewCrypto()
    {
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

        CollectionAssert.AreEqual(new ulong[] { 0, 4, 8, 10 }, sent.Select(frame => frame.Offset).ToArray());
        CollectionAssert.AreEqual(new[] { 4, 4, 2, 2 }, sent.Select(frame => frame.Data.Length).ToArray());
        CollectionAssert.AreEqual(new byte[] { 8, 9 }, sent[2].Data.ToArray());
    }

    [TestMethod]
    public void TakeFrames_NoRoomForLostCrypto_KeepsItQueued()
    {
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1]);
        space.TakeFrames(100, TimeSpan.Zero);
        space.RequeueLost([new QuicCryptoFrame(0, new byte[] { 1 })]);

        Assert.IsEmpty(space.TakeFrames(QuicPacketNumberSpace.CryptoFrameOverhead, TimeSpan.Zero).Frames);
        Assert.IsTrue(space.HasFramesToSend);
    }

    [TestMethod]
    public void RequeueCryptoAndDropUnsentCrypto_LostRanges_AreForgotten()
    {
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.QueueCrypto([1, 2]);
        space.TakeFrames(100, TimeSpan.Zero);

        space.RequeueLost([new QuicCryptoFrame(1, new byte[] { 2 })]);
        space.RequeueCrypto();
        QuicCryptoFrame again = (QuicCryptoFrame)space.TakeFrames(100, TimeSpan.Zero).Frames.Single();
        space.RequeueLost([new QuicCryptoFrame(1, new byte[] { 2 })]);
        space.DropUnsentCrypto();

        Assert.AreEqual((0UL, 2), (again.Offset, again.Data.Length));
        Assert.IsFalse(space.HasFramesToSend);
    }

    [TestMethod]
    public void TakeFrames_AcknowledgementDue_CarriesTheDelaySinceTheLargestArrivedScaledByTheExponent()
    {
        using QuicPacketNumberSpace space = SpaceWithKeys();
        space.RecordReceived(5, TimeSpan.FromMilliseconds(1));
        space.RecordReceived(3, TimeSpan.FromMilliseconds(2));
        space.RequireAcknowledgement();
        Assert.IsTrue(space.HasOnlyAcknowledgementToSend);

        // 8 ms since packet 5 arrived: 8000 microseconds, divided by 2^3 (RFC 9000 section 19.3).
        QuicAckFrame ack = (QuicAckFrame)space.TakeFrames(100, TimeSpan.FromMilliseconds(9)).Frames.Single();
        space.AckDelayExponent = 0;
        space.RequireAcknowledgement();
        QuicAckFrame early = (QuicAckFrame)space.TakeFrames(100, TimeSpan.Zero).Frames.Single();

        Assert.AreEqual(1000UL, ack.AckDelay);
        Assert.AreEqual(0UL, early.AckDelay);
    }

    [TestMethod]
    public void Id_EachPacketType_NamesItsLossRecoverySpace()
    {
        using QuicPacketNumberSpace initial = new(QuicPacketType.Initial);
        using QuicPacketNumberSpace handshake = new(QuicPacketType.Handshake);
        using QuicPacketNumberSpace application = new(QuicPacketType.OneRtt);

        Assert.AreEqual(QuicPacketNumberSpaceId.Initial, initial.Id);
        Assert.AreEqual(QuicPacketNumberSpaceId.Handshake, handshake.Id);
        Assert.AreEqual(QuicPacketNumberSpaceId.ApplicationData, application.Id);
    }
}
