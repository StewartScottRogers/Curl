using Curl.Conformance.Differential.Libcurl.WebSocket;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.WebSocket;

[TestClass]
public sealed class LibcurlWebSocketLineWriterTests
{
    private static readonly LibcurlWebSocketFrameFields TextFrame = new(0, 1, 2, 3, 4);

    [TestMethod]
    public void Perform_Code_WritesThePerformLine() =>
        Assert.AreEqual("perform |code=7", LibcurlWebSocketLineWriter.Perform(7));

    [TestMethod]
    public void Send_Step_WritesItsFlagsFragmentSizeLengthCodeAndSent() =>
        Assert.AreEqual("send flags=33 frag=8 len=4 |code=0 |sent=4", LibcurlWebSocketLineWriter.Send(LibcurlWebSocketStep.Send("abcd"u8.ToArray(), 33, 8), 0, 4));

    [TestMethod]
    public void Receive_WithFrame_WritesTheDataAndEveryFrameField() =>
        Assert.AreEqual("recv buf=4 |code=0 |n=2 |data=b'hi' |frame=age=0 flags=1 offset=2 bytesleft=3 len=4", LibcurlWebSocketLineWriter.Receive(LibcurlWebSocketStep.Receive(4), 0, "hi"u8, TextFrame));

    [TestMethod]
    public void Receive_WithoutFrame_WritesNull() =>
        Assert.AreEqual("recv buf=256 |code=81 |n=0 |data=b'' |frame=null", LibcurlWebSocketLineWriter.Receive(LibcurlWebSocketStep.Receive(), 81, [], null));

    [TestMethod]
    public void Meta_WithAndWithoutFrame_WritesTheFrameOrNull()
    {
        Assert.AreEqual("meta |frame=null", LibcurlWebSocketLineWriter.Meta(null));
        Assert.AreEqual("meta |frame=age=0 flags=1 offset=2 bytesleft=3 len=4", LibcurlWebSocketLineWriter.Meta(TextFrame));
    }

    [TestMethod]
    public void Write_Data_WritesItsLengthDataAndFrame() =>
        Assert.AreEqual("write |n=1 |data=b'x' |frame=null", LibcurlWebSocketLineWriter.Write("x"u8, null));

    [TestMethod]
    public void SendAndReceive_NullStep_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlWebSocketLineWriter.Send(null!, 0, 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlWebSocketLineWriter.Receive(null!, 0, [], null));
    }

    [TestMethod]
    public void Meta_Step_HasNoPayloadFlagsOrBuffer()
    {
        var step = LibcurlWebSocketStep.Meta();

        Assert.AreEqual(LibcurlWebSocketStepKind.Meta, step.Kind);
        Assert.IsEmpty(step.Payload);
        Assert.AreEqual(0, step.Flags + step.FragmentSize + step.BufferSize);
    }
}
