namespace Curl.Quic;

[TestClass]
public sealed class QuicPeerConnectionIdsTests
{
    private static readonly byte[] ResetToken = new byte[16];

    [TestMethod]
    public void Receive_RetirePriorTo_RetiresLowerIdsAndMovesToALiveOne()
    {
        QuicPeerConnectionIds ids = new([0xa0], 3);
        ids.SetHandshakeResetToken([.. ResetToken]);

        Assert.IsEmpty(ids.Receive(NewId(1, 0, 0xa1)));
        IReadOnlyList<QuicRetireConnectionIdFrame> retired = ids.Receive(NewId(2, 2, 0xa2));

        CollectionAssert.AreEqual(new ulong[] { 0, 1 }, retired.Select(frame => frame.SequenceNumber).ToArray());
        Assert.AreEqual(2UL, ids.Current.SequenceNumber);
        Assert.HasCount(1, ids.Active);
    }

    [TestMethod]
    public void Receive_NoRetirement_KeepsTheIdInUse()
    {
        QuicPeerConnectionIds ids = new([0xa0], 4);
        ids.Receive(NewId(1, 0, 0xa1));
        ids.Receive(NewId(3, 0, 0xa3));

        Assert.IsEmpty(ids.Receive(NewId(2, 0, 0xa2)));
        Assert.AreEqual(0UL, ids.Current.SequenceNumber);
        Assert.IsNull(ids.Current.StatelessResetToken);
    }

    [TestMethod]
    public void Receive_RepeatedOrAlreadyRetired_IgnoresOrRetiresAgain()
    {
        QuicPeerConnectionIds ids = new([0xa0], 2);
        ids.Receive(NewId(3, 3, 0xa3));

        Assert.IsEmpty(ids.Receive(NewId(3, 3, 0xa3)));
        Assert.AreEqual(1UL, ids.Receive(NewId(1, 0, 0xa1)).Single().SequenceNumber);
    }

    [TestMethod]
    public void Receive_ReusedSequenceOrTooMany_Throws()
    {
        QuicPeerConnectionIds ids = new([0xa0], 2);
        ids.Receive(NewId(1, 0, 0xa1));

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, QuicTest.ErrorOf(() => ids.Receive(NewId(1, 0, 0xff))));
        Assert.AreEqual(QuicTransportErrorCode.ConnectionIdLimitError, QuicTest.ErrorOf(() => ids.Receive(NewId(2, 0, 0xa2))));
        Assert.ThrowsExactly<ArgumentNullException>(() => ids.Receive(null!));
    }

    private static QuicNewConnectionIdFrame NewId(ulong sequence, ulong retirePriorTo, byte id) => new(sequence, retirePriorTo, new byte[] { id }, ResetToken);
}
