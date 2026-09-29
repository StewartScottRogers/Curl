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
            Assert.IsTrue(space.RecordReceived(packetNumber));
        }

        space.RequireAcknowledgement();
        QuicAckFrame ack = (QuicAckFrame)space.TakeFrames(0).Frames.Single();

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
        Assert.IsTrue(space.RecordReceived(4));
        Assert.IsFalse(space.RecordReceived(4));
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
            space.TakeFrames(0);
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

        QuicCryptoFrame first = (QuicCryptoFrame)space.TakeFrames(QuicPacketNumberSpace.CryptoFrameOverhead + 2).Frames.Single();
        QuicCryptoFrame second = (QuicCryptoFrame)space.TakeFrames(QuicPacketNumberSpace.CryptoFrameOverhead + 2).Frames.Single();
        Assert.IsFalse(space.HasFramesToSend);
        space.RequeueCrypto();
        (List<QuicFrame> frames, ulong packetNumber) = space.TakeFrames(100);

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
        space.RecordReceived(0);
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
        space.TakeFrames(100);

        space.ReceiveAcknowledgement(new QuicAckFrame(0, 0, 0, [], null));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, QuicTest.ErrorOf(() => space.ReceiveAcknowledgement(new QuicAckFrame(1, 0, 0, [], null))));
    }
}
