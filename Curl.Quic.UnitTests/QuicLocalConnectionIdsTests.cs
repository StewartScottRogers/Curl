namespace Curl.Quic;

[TestClass]
public sealed class QuicLocalConnectionIdsTests
{
    [TestMethod]
    public void IssueUpToAndRetire_ServerLimitAndRetirement_IssuesToTheLimitAndReplaces()
    {
        QuicLocalConnectionIds ids = new(new byte[8], new QuicTestRandomSource { FirstByte = 1 });

        IReadOnlyList<QuicNewConnectionIdFrame> issued = ids.IssueUpTo(20);
        IReadOnlyList<QuicNewConnectionIdFrame> replacement = ids.Retire(new QuicRetireConnectionIdFrame(0));

        Assert.HasCount(QuicLocalConnectionIds.MaximumActive - 1, issued);
        Assert.IsTrue(issued.All(frame => frame.ConnectionId.Length == 8 && frame.StatelessResetToken.Length == 16));
        Assert.AreEqual(8UL, replacement.Single().SequenceNumber);
        Assert.IsFalse(ids.Contains(new byte[8]));
        Assert.IsTrue(ids.Contains(issued[0].ConnectionId.Span));
        Assert.HasCount(QuicLocalConnectionIds.MaximumActive, ids.Active);
        Assert.IsEmpty(ids.Retire(new QuicRetireConnectionIdFrame(0)));
        Assert.IsEmpty(ids.IssueUpTo(2));
    }

    [TestMethod]
    public void Retire_NeverIssued_ThrowsProtocolViolation()
    {
        QuicLocalConnectionIds ids = new(new byte[8], new QuicTestRandomSource());

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, QuicTest.ErrorOf(() => ids.Retire(new QuicRetireConnectionIdFrame(1))));
        Assert.ThrowsExactly<ArgumentNullException>(() => ids.Retire(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuicLocalConnectionIds(null!, new QuicTestRandomSource()));
    }

    [TestMethod]
    public void Retire_TheOnlyLiveId_IssuesAReplacementOfTheSameLength()
    {
        QuicLocalConnectionIds ids = new(new byte[20], new QuicTestRandomSource());

        QuicNewConnectionIdFrame replacement = ids.Retire(new QuicRetireConnectionIdFrame(0)).Single();

        Assert.AreEqual(20, replacement.ConnectionId.Length);
        Assert.AreEqual(1UL, replacement.SequenceNumber);
    }
}
