using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Stomp;

/// <summary>
/// Checks curl's SUBSCRIBE frame and its reading of MESSAGE and ERROR frames against STOMP 1.2 (BL-4493): bodies with and
/// without <c>content-length</c>, CR LF frames, heart-beat EOLs, frames split across reads, header unescaping and
/// repeated headers.
/// </summary>
[TestClass]
public sealed class StompSubscribeCases
{
    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Subscribe-1")]
    [SpecRequirement("Stomp:STOMP12-Subscribe-2")]
    [SpecRequirement("Stomp:STOMP12-Subscribe-3")]
    [SpecRequirement("Stomp:STOMP12-Frames-3")]
    [SpecRequirement("Stomp:STOMP12-ContentLength-2")]
    [SpecRequirement("Stomp:STOMP12-ContentLength-3")]
    [SpecRequirement("Stomp:STOMP12-Message-1")]
    public async Task Subscribe_MessagesWithAndWithoutContentLength_AreWrittenUntilTheServerCloses()
    {
        CurlRunResult result = await StompRun.RunAsync(
            StompRun.SubscribeAnswered(
                StompRun.Connected,
                "MESSAGE\nsubscription:0\nmessage-id:1\ndestination:q\ncontent-length:3\n\na\0b\0",
                "MESSAGE\nsubscription:0\nmessage-id:2\ndestination:q\n\nhi\n\0"),
            "stomp://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty, "a\0bhi\n");
        Assert.AreEqual(StompRun.PlainConnect + StompRun.SubscribeQ, StompRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Frames-2")]
    public async Task Subscribe_CrLfFrames_AreRead()
    {
        CurlRunResult result = await StompRun.RunAsync(
            StompRun.SubscribeAnswered(
                "CONNECTED\r\nversion:1.2\r\n\r\n\0",
                "MESSAGE\r\nsubscription:0\r\nmessage-id:1\r\ndestination:q\r\ncontent-length:2\r\n\r\nhi\0"),
            "stomp://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty, "hi");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Frames-4")]
    public async Task Subscribe_HeartBeatEolsBetweenFrames_AreSkipped()
    {
        CurlRunResult result = await StompRun.RunAsync(
            StompRun.SubscribeAnswered(
                "\n" + StompRun.Connected + "\r\n",
                "\n\n\r\nMESSAGE\nsubscription:0\nmessage-id:1\ndestination:q\n\na\0\n",
                "\r\nMESSAGE\nsubscription:0\nmessage-id:2\ndestination:q\n\nb\0\n\n"),
            "stomp://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty, "ab");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Frames-5")]
    public async Task Subscribe_FramesSplitAcrossReads_AreReassembled()
    {
        CurlRunResult result = await StompRun.RunAsync(
            [
                ScriptStep.ReceiveUntil("\0"),
                ScriptStep.Send("CONNEC"),
                ScriptStep.Send("TED\nver"),
                ScriptStep.Send("sion:1.2\n\n"),
                ScriptStep.Send("\0"),
                ScriptStep.ReceiveUntil("ack:auto\n\n\0"),
                ScriptStep.Send("MESSAGE\nsubscription:0\nmessage-id:1\ncontent-len"),
                ScriptStep.Send("gth:5\n\nhel"),
                ScriptStep.Send("lo"),
                ScriptStep.Send("\0"),
            ],
            "stomp://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty, "hello");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Encoding-3")]
    public async Task Subscribe_ErrorWithEscapedMessage_IsUnescaped()
    {
        CurlRunResult result = await StompRun.RunAsync(
            StompRun.SubscribeAnswered(StompRun.Connected, "ERROR\nmessage:no\\cqueue\\\\q\n\n\0"),
            "stomp://localhost/q");

        StompRun.AssertExit(result, 8, "curl: (8) STOMP server sent ERROR: no:queue\\q\n");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Encoding-5")]
    public async Task Subscribe_UndefinedEscapeSequence_IsAFatalProtocolError()
    {
        CurlRunResult result = await StompRun.RunAsync(
            StompRun.SubscribeAnswered(StompRun.Connected, "MESSAGE\nsubscription:0\nmessage-id:1\nx:a\\tb\n\nhi\0"),
            "stomp://localhost/q");

        StompRun.AssertExit(result, 8, "curl: (8) Undefined STOMP escape sequence in: a\\tb\n");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Repeated-1")]
    public async Task Subscribe_RepeatedContentLength_UsesTheFirst()
    {
        CurlRunResult result = await StompRun.RunAsync(
            StompRun.SubscribeAnswered(StompRun.Connected, "MESSAGE\nsubscription:0\nmessage-id:1\ncontent-length:2\ncontent-length:9\n\nhi\0"),
            "stomp://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty, "hi");
    }
}
