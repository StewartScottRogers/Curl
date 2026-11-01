using System.Text;
using Curl.Extended.Protocol.Stomp.Client;

namespace Curl.Extended.Protocol.Stomp.Wire;

/// <summary>Checks frame reading against STOMP 1.2, "STOMP Frames", "Header content-length" and "Heart-beating".</summary>
[TestClass]
public sealed class StompFrameReaderTests
{
    [TestMethod]
    public async Task ReadAsync_FrameWithoutContentLength_ReadsBodyUpToNul()
    {
        StompFrame? frame = await Read("MESSAGE\nsubscription:0\n\nhello\0");

        Assert.IsNotNull(frame);
        Assert.AreEqual("MESSAGE", frame.Command);
        Assert.AreEqual("0", frame.HeaderValue("subscription"));
        Assert.AreEqual("hello", Encoding.UTF8.GetString(frame.Body));
    }

    [TestMethod]
    public async Task ReadAsync_ContentLength_ReadsBodyHoldingNul()
    {
        StompFrame? frame = await Read("MESSAGE\ncontent-length:3\n\na\0b\0");

        CollectionAssert.AreEqual(new byte[] { (byte)'a', 0, (byte)'b' }, frame!.Body);
    }

    [TestMethod]
    public async Task ReadAsync_CrLfLinesAndHeartBeatEols_AreAccepted()
    {
        StompFrame? frame = await Read("\n\r\nRECEIPT\r\nreceipt-id:7\r\n\r\n\0");

        Assert.AreEqual("RECEIPT", frame!.Command);
        Assert.AreEqual("7", frame.HeaderValue("receipt-id"));
        Assert.IsEmpty(frame.Body);
    }

    [TestMethod]
    public async Task ReadAsync_EscapedHeaders_AreUnescapedButNotInConnected()
    {
        StompFrameReader reader = new(ScriptedStompConnection.Replaying("MESSAGE\na\\cb:c\\nd\n\n\0CONNECTED\nx:a\\b\n\n\0"), CancellationToken.None);

        StompFrame? message = await reader.ReadAsync();
        StompFrame? connected = await reader.ReadAsync();

        Assert.AreEqual("c\nd", message!.HeaderValue("a:b"));
        Assert.AreEqual("a\\b", connected!.HeaderValue("x"));
    }

    [TestMethod]
    public async Task ReadAsync_FrameSplitAcrossReads_IsReassembled()
    {
        StompFrameReader reader = new(ScriptedStompConnection.Replaying("MESS", "AGE\ncontent-le", "ngth:2\n\nh", "i", "\0"), CancellationToken.None);

        StompFrame? frame = await reader.ReadAsync();

        Assert.AreEqual("hi", Encoding.UTF8.GetString(frame!.Body));
        Assert.IsNull(await reader.ReadAsync());
    }

    [TestMethod]
    public async Task ReadAsync_BodyLargerThanTheBuffer_GrowsTheBuffer()
    {
        string body = new('x', 10_000);

        StompFrame? frame = await Read($"MESSAGE\n\n{body}\0");

        Assert.AreEqual(body, Encoding.UTF8.GetString(frame!.Body));
    }

    [TestMethod]
    public async Task ReadAsync_EndBetweenFrames_GivesNull() =>
        Assert.IsNull(await Read("\n"));

    [TestMethod]
    [DataRow("MESSAGE\n")]
    [DataRow("MESSAGE\n\nno nul")]
    [DataRow("MESSAGE\ncontent-length:5\n\nab")]
    public async Task ReadAsync_EndInsideAFrame_ThrowsEndOfStream(string bytes) =>
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await Read(bytes));

    [TestMethod]
    [DataRow("MESSAGE\ncontent-length:1\n\nab\0")]
    [DataRow("MESSAGE\ncontent-length:-1\n\n\0")]
    [DataRow("MESSAGE\nbad\n\n\0")]
    public async Task ReadAsync_MalformedFrame_ThrowsFormat(string bytes) =>
        await Assert.ThrowsExactlyAsync<FormatException>(async () => await Read(bytes));

    private static ValueTask<StompFrame?> Read(string bytes) =>
        new StompFrameReader(ScriptedStompConnection.Replaying(bytes), CancellationToken.None).ReadAsync();
}
