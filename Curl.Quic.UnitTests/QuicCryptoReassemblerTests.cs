using static Curl.Quic.QuicTest;

namespace Curl.Quic;

[TestClass]
public sealed class QuicCryptoReassemblerTests
{
    [TestMethod]
    public void Receive_InOrder_HandsBytesOnAtOnce()
    {
        QuicCryptoReassembler reassembler = new();

        Assert.AreEqual("0102", HexOf(reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1, 2 }))));
        Assert.AreEqual("03", HexOf(reassembler.Receive(new QuicCryptoFrame(2, new byte[] { 3 }))));
    }

    [TestMethod]
    public void Receive_LaterFrame_WaitsForTheGap()
    {
        QuicCryptoReassembler reassembler = new();

        Assert.IsEmpty(reassembler.Receive(new QuicCryptoFrame(2, new byte[] { 3, 4 })));
        Assert.AreEqual("01020304", HexOf(reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1, 2 }))));
    }

    [TestMethod]
    public void Receive_RepeatedOrOverlappingBytes_HandsThemOnOnce()
    {
        QuicCryptoReassembler reassembler = new();
        reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1, 2 }));

        Assert.IsEmpty(reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1, 2 })));
        Assert.AreEqual("03", HexOf(reassembler.Receive(new QuicCryptoFrame(1, new byte[] { 2, 3 }))));
    }

    [TestMethod]
    public void Receive_TwoFramesAtOneOffset_KeepsTheLonger()
    {
        QuicCryptoReassembler reassembler = new();
        reassembler.Receive(new QuicCryptoFrame(3, new byte[] { 4 }));
        reassembler.Receive(new QuicCryptoFrame(3, new byte[] { 4, 5 }));
        reassembler.Receive(new QuicCryptoFrame(3, new byte[] { 4 }));
        reassembler.Receive(new QuicCryptoFrame(1, new byte[] { 2, 3, 4, 5 }));

        Assert.AreEqual("0102030405", HexOf(reassembler.Receive(new QuicCryptoFrame(0, new byte[] { 1 }))));
    }

    [TestMethod]
    public void Receive_FrameTooFarAhead_ThrowsCryptoBufferExceeded()
    {
        QuicCryptoReassembler reassembler = new();

        Assert.AreEqual(QuicTransportErrorCode.CryptoBufferExceeded, ErrorOf(() => reassembler.Receive(new QuicCryptoFrame(QuicCryptoReassembler.MaximumBufferedBytes, new byte[] { 1 }))));
        Assert.ThrowsExactly<ArgumentNullException>(() => reassembler.Receive(null!));
    }
}
