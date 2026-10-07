using Curl.Testing;

namespace Curl.Quic;

[TestClass]
public sealed class QuicLocalConnectionIdsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void IssueUpToAndRetire_ServerLimitAndRetirement_IssuesToTheLimitAndReplaces()
    {
        Diagnostics.Arrange("initial connection id", QuicTest.HexOf(new byte[8]));
        Diagnostics.Arrange("issue up to", 20);
        Diagnostics.Arrange("retire sequence", 0);
        QuicLocalConnectionIds ids = new(new byte[8], new QuicTestRandomSource { FirstByte = 1 });

        IReadOnlyList<QuicNewConnectionIdFrame> issued = ids.IssueUpTo(20);
        IReadOnlyList<QuicNewConnectionIdFrame> replacement = ids.Retire(new QuicRetireConnectionIdFrame(0));

        Diagnostics.Act("issued count", issued.Count);
        Diagnostics.Act("replacement sequence", replacement.Single().SequenceNumber);
        Diagnostics.Act("active count", ids.Active.Count);
        Diagnostics.Assert("issued count", QuicLocalConnectionIds.MaximumActive - 1, issued.Count);
        Assert.HasCount(QuicLocalConnectionIds.MaximumActive - 1, issued);
        Assert.IsTrue(issued.All(frame => frame.ConnectionId.Length == 8 && frame.StatelessResetToken.Length == 16));
        Diagnostics.Assert("replacement sequence", 8UL, replacement.Single().SequenceNumber);
        Assert.AreEqual(8UL, replacement.Single().SequenceNumber);
        Diagnostics.Assert("retired id still contained", false, ids.Contains(new byte[8]));
        Assert.IsFalse(ids.Contains(new byte[8]));
        Diagnostics.Assert("first issued id contained", true, ids.Contains(issued[0].ConnectionId.Span));
        Assert.IsTrue(ids.Contains(issued[0].ConnectionId.Span));
        Diagnostics.Assert("active count", QuicLocalConnectionIds.MaximumActive, ids.Active.Count);
        Assert.HasCount(QuicLocalConnectionIds.MaximumActive, ids.Active);
        var retiredAgain = ids.Retire(new QuicRetireConnectionIdFrame(0));
        Diagnostics.Assert("frames after retiring twice", 0, retiredAgain.Count);
        Assert.IsEmpty(retiredAgain);
        var issuedAgain = ids.IssueUpTo(2);
        Diagnostics.Assert("frames after issuing at the limit", 0, issuedAgain.Count);
        Assert.IsEmpty(issuedAgain);
    }

    [TestMethod]
    public void Retire_NeverIssued_ThrowsProtocolViolation()
    {
        Diagnostics.Arrange("initial connection id", QuicTest.HexOf(new byte[8]));
        Diagnostics.Arrange("retire sequence", 1);
        QuicLocalConnectionIds ids = new(new byte[8], new QuicTestRandomSource());

        var error = QuicTest.ErrorOf(() => ids.Retire(new QuicRetireConnectionIdFrame(1)));

        Diagnostics.Act("error code", error);
        Diagnostics.Assert("error code", QuicTransportErrorCode.ProtocolViolation, error);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, error);
        var nullFrame = Assert.ThrowsExactly<ArgumentNullException>(() => ids.Retire(null!));
        Diagnostics.Act("null frame exception", nullFrame.GetType().Name);
        Diagnostics.Assert("null frame exception", nameof(ArgumentNullException), nullFrame.GetType().Name);
        var nullId = Assert.ThrowsExactly<ArgumentNullException>(() => new QuicLocalConnectionIds(null!, new QuicTestRandomSource()));
        Diagnostics.Act("null id exception", nullId.GetType().Name);
        Diagnostics.Assert("null id exception", nameof(ArgumentNullException), nullId.GetType().Name);
    }

    [TestMethod]
    public void Retire_TheOnlyLiveId_IssuesAReplacementOfTheSameLength()
    {
        Diagnostics.Arrange("initial connection id length", 20);
        QuicLocalConnectionIds ids = new(new byte[20], new QuicTestRandomSource());

        QuicNewConnectionIdFrame replacement = ids.Retire(new QuicRetireConnectionIdFrame(0)).Single();

        Diagnostics.Act("replacement length", replacement.ConnectionId.Length);
        Diagnostics.Act("replacement sequence", replacement.SequenceNumber);
        Diagnostics.Assert("replacement length", 20, replacement.ConnectionId.Length);
        Assert.AreEqual(20, replacement.ConnectionId.Length);
        Diagnostics.Assert("replacement sequence", 1UL, replacement.SequenceNumber);
        Assert.AreEqual(1UL, replacement.SequenceNumber);
    }
}
