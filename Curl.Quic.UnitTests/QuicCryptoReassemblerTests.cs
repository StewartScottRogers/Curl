using Curl.Testing;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

[TestClass]
public sealed class QuicCryptoReassemblerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Receive_InOrder_HandsBytesOnAtOnce()
    {
        Diagnostics.Arrange("frames (offset:data)", "0:0102, 2:03");
        QuicCryptoReassembler reassembler = new();

        string first = HexOf(reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1, 2 })));
        Diagnostics.Act("handed on after offset 0", first);
        Diagnostics.Assert("handed on after offset 0", "0102", first);
        Assert.AreEqual("0102", first);
        string second = HexOf(reassembler.Receive(new QuicCryptoFrame(2, new byte[] { 3 })));
        Diagnostics.Act("handed on after offset 2", second);
        Diagnostics.Assert("handed on after offset 2", "03", second);
        Assert.AreEqual("03", second);
    }

    [TestMethod]
    public void Receive_LaterFrame_WaitsForTheGap()
    {
        Diagnostics.Arrange("frames (offset:data)", "2:0304, 0:0102");
        QuicCryptoReassembler reassembler = new();

        byte[] early = reassembler.Receive(new QuicCryptoFrame(2, new byte[] { 3, 4 }));
        Diagnostics.Act("handed on after offset 2 (a gap before it)", early.Length);
        Diagnostics.Assert("bytes handed on while the gap is open", 0, early.Length);
        Assert.IsEmpty(early);
        string filled = HexOf(reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1, 2 })));
        Diagnostics.Act("handed on after offset 0", filled);
        Diagnostics.Assert("handed on after the gap is filled", "01020304", filled);
        Assert.AreEqual("01020304", filled);
    }

    [TestMethod]
    public void Receive_RepeatedOrOverlappingBytes_HandsThemOnOnce()
    {
        Diagnostics.Arrange("frames (offset:data)", "0:0102, 0:0102, 1:0203");
        QuicCryptoReassembler reassembler = new();
        reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1, 2 }));

        byte[] repeated = reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1, 2 }));
        Diagnostics.Act("handed on for the repeated frame", repeated.Length);
        Diagnostics.Assert("bytes handed on for the repeated frame", 0, repeated.Length);
        Assert.IsEmpty(repeated);
        string overlapping = HexOf(reassembler.Receive(new QuicCryptoFrame(1, new byte[] { 2, 3 })));
        Diagnostics.Act("handed on for the overlapping frame", overlapping);
        Diagnostics.Assert("handed on for the overlapping frame", "03", overlapping);
        Assert.AreEqual("03", overlapping);
    }

    [TestMethod]
    public void Receive_TwoFramesAtOneOffset_KeepsTheLonger()
    {
        Diagnostics.Arrange("frames (offset:data)", "3:04, 3:0405, 3:04, 1:02030405, 0:01");
        QuicCryptoReassembler reassembler = new();
        reassembler.Receive(new QuicCryptoFrame(3, new byte[] { 4 }));
        reassembler.Receive(new QuicCryptoFrame(3, new byte[] { 4, 5 }));
        reassembler.Receive(new QuicCryptoFrame(3, new byte[] { 4 }));
        reassembler.Receive(new QuicCryptoFrame(1, new byte[] { 2, 3, 4, 5 }));

        string handedOn = HexOf(reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1 })));

        Diagnostics.Act("handed on after offset 0", handedOn);
        Diagnostics.Assert("handed on after offset 0", "0102030405", handedOn);
        Assert.AreEqual("0102030405", handedOn);
    }

    [TestMethod]
    public void Receive_FrameTooFarAhead_ThrowsCryptoBufferExceeded()
    {
        Diagnostics.Arrange("frame offset", QuicCryptoReassembler.MaximumBufferedBytes);
        QuicCryptoReassembler reassembler = new();

        QuicTransportErrorCode errorCode = ErrorOf(() => reassembler.Receive(new QuicCryptoFrame(QuicCryptoReassembler.MaximumBufferedBytes, new byte[] { 1 })));
        Diagnostics.Act("QuicTransportException error code", errorCode);
        Diagnostics.Assert("error code", QuicTransportErrorCode.CryptoBufferExceeded, errorCode);
        Assert.AreEqual(QuicTransportErrorCode.CryptoBufferExceeded, errorCode);
        ArgumentNullException nullFrame = Assert.ThrowsExactly<ArgumentNullException>(() => reassembler.Receive(null!));
        Diagnostics.Act("null frame exception", FormattableString.Invariant($"{nullFrame.GetType().Name}: {nullFrame.ParamName}"));
        Diagnostics.Assert("null frame exception type", typeof(ArgumentNullException).Name, nullFrame.GetType().Name);
    }
}
