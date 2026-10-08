using System.Globalization;
using Curl.Testing;

namespace Curl.Quic;

[TestClass]
public sealed class QuicPeerConnectionIdsTests
{
    private static readonly byte[] ResetToken = new byte[16];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Receive_RetirePriorTo_RetiresLowerIdsAndMovesToALiveOne()
    {
        Diagnostics.Arrange("initial id", "a0");
        Diagnostics.Arrange("active limit", 3);
        Diagnostics.Arrange("frames (sequence, retire prior to, id)", "(1, 0, a1), (2, 2, a2)");
        QuicPeerConnectionIds ids = new([0xa0], 3);
        ids.SetHandshakeResetToken([.. ResetToken]);

        var first = ids.Receive(NewId(1, 0, 0xa1));
        Diagnostics.Assert("frames retired by the first id", 0, first.Count);
        Assert.IsEmpty(first);
        IReadOnlyList<QuicRetireConnectionIdFrame> retired = ids.Receive(NewId(2, 2, 0xa2));

        var retiredSequences = retired.Select(frame => frame.SequenceNumber).ToArray();
        var retiredText = string.Join(",", retiredSequences.Select(sequence => sequence.ToString(CultureInfo.InvariantCulture)));
        Diagnostics.Act("retired sequences", retiredText);
        Diagnostics.Act("current sequence", ids.Current.SequenceNumber);
        Diagnostics.Act("active count", ids.Active.Count);
        Diagnostics.Assert("retired sequences", "0,1", retiredText);
        CollectionAssert.AreEqual(new ulong[] { 0, 1 }, retiredSequences);
        Diagnostics.Assert("current sequence", 2UL, ids.Current.SequenceNumber);
        Assert.AreEqual(2UL, ids.Current.SequenceNumber);
        Diagnostics.Assert("active count", 1, ids.Active.Count);
        Assert.HasCount(1, ids.Active);
    }

    [TestMethod]
    public void Receive_NoRetirement_KeepsTheIdInUse()
    {
        Diagnostics.Arrange("initial id", "a0");
        Diagnostics.Arrange("active limit", 4);
        Diagnostics.Arrange("frames (sequence, retire prior to, id)", "(1, 0, a1), (3, 0, a3), (2, 0, a2)");
        QuicPeerConnectionIds ids = new([0xa0], 4);
        ids.Receive(NewId(1, 0, 0xa1));
        ids.Receive(NewId(3, 0, 0xa3));

        var retired = ids.Receive(NewId(2, 0, 0xa2));

        Diagnostics.Act("retired count", retired.Count);
        Diagnostics.Act("current sequence", ids.Current.SequenceNumber);
        Diagnostics.Act("current reset token is null", ids.Current.StatelessResetToken is null);
        Diagnostics.Assert("retired count", 0, retired.Count);
        Assert.IsEmpty(retired);
        Diagnostics.Assert("current sequence", 0UL, ids.Current.SequenceNumber);
        Assert.AreEqual(0UL, ids.Current.SequenceNumber);
        Diagnostics.Assert("current reset token is null", true, ids.Current.StatelessResetToken is null);
        Assert.IsNull(ids.Current.StatelessResetToken);
    }

    [TestMethod]
    public void Receive_RepeatedOrAlreadyRetired_IgnoresOrRetiresAgain()
    {
        Diagnostics.Arrange("initial id", "a0");
        Diagnostics.Arrange("active limit", 2);
        Diagnostics.Arrange("frames (sequence, retire prior to, id)", "(3, 3, a3), (3, 3, a3), (1, 0, a1)");
        QuicPeerConnectionIds ids = new([0xa0], 2);
        ids.Receive(NewId(3, 3, 0xa3));

        var repeated = ids.Receive(NewId(3, 3, 0xa3));
        var alreadyRetired = ids.Receive(NewId(1, 0, 0xa1));

        Diagnostics.Act("repeated retired count", repeated.Count);
        Diagnostics.Act("already retired sequence", alreadyRetired.Single().SequenceNumber);
        Diagnostics.Assert("repeated retired count", 0, repeated.Count);
        Assert.IsEmpty(repeated);
        Diagnostics.Assert("already retired sequence", 1UL, alreadyRetired.Single().SequenceNumber);
        Assert.AreEqual(1UL, alreadyRetired.Single().SequenceNumber);
    }

    [TestMethod]
    public void Receive_ReusedSequenceOrTooMany_Throws()
    {
        Diagnostics.Arrange("initial id", "a0");
        Diagnostics.Arrange("active limit", 2);
        Diagnostics.Arrange("frames (sequence, retire prior to, id)", "(1, 0, a1), (1, 0, ff), (2, 0, a2)");
        QuicPeerConnectionIds ids = new([0xa0], 2);
        ids.Receive(NewId(1, 0, 0xa1));

        var reused = QuicTest.ErrorOf(() => ids.Receive(NewId(1, 0, 0xff)));
        Diagnostics.Act("reused sequence error code", reused);
        Diagnostics.Assert("reused sequence error code", QuicTransportErrorCode.ProtocolViolation, reused);
        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, reused);
        var tooMany = QuicTest.ErrorOf(() => ids.Receive(NewId(2, 0, 0xa2)));
        Diagnostics.Act("too many ids error code", tooMany);
        Diagnostics.Assert("too many ids error code", QuicTransportErrorCode.ConnectionIdLimitError, tooMany);
        Assert.AreEqual(QuicTransportErrorCode.ConnectionIdLimitError, tooMany);
        var nullFrame = Assert.ThrowsExactly<ArgumentNullException>(() => ids.Receive(null!));
        Diagnostics.Act("null frame exception", nullFrame.GetType().Name);
        Diagnostics.Assert("null frame exception", nameof(ArgumentNullException), nullFrame.GetType().Name);
    }

    private static QuicNewConnectionIdFrame NewId(ulong sequence, ulong retirePriorTo, byte id) => new(sequence, retirePriorTo, new byte[] { id }, ResetToken);
}
