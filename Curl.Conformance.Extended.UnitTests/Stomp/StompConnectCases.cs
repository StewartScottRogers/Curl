using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Stomp;

/// <summary>
/// Checks curl's CONNECT frame and its reading of CONNECTED and ERROR against STOMP 1.2 (BL-4493): the headers CONNECT
/// carries, credentials sent unescaped, protocol negotiation, an ERROR at connect, repeated headers and <c>stomps://</c>.
/// </summary>
[TestClass]
public sealed class StompConnectCases
{
    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Frames-1")]
    [SpecRequirement("Stomp:STOMP12-Frames-3")]
    [SpecRequirement("Stomp:STOMP12-Connect-1")]
    [SpecRequirement("Stomp:STOMP12-Connect-2")]
    [SpecRequirement("Stomp:STOMP12-Connect-4")]
    [SpecRequirement("Stomp:STOMP12-Send-1")]
    [SpecRequirement("Stomp:STOMP12-ContentLength-1")]
    [SpecRequirement("Stomp:STOMP12-Disconnect-1")]
    [SpecRequirement("Stomp:STOMP12-Disconnect-2")]
    public async Task Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt()
    {
        CurlRunResult result = await StompRun.RunAsync(StompRun.PublishAnswered(), "-d", "hi", "stomp://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty);
        Assert.AreEqual(StompRun.PlainConnect + "SEND\ndestination:/q\ncontent-length:2\n\nhi\0" + StompRun.Disconnect, StompRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Connect-2")]
    [SpecRequirement("Stomp:STOMP12-Connect-3")]
    [SpecRequirement("Stomp:STOMP12-Encoding-4")]
    public async Task Publish_CredentialsWithBackslashAndColon_AreSentUnescapedAsLoginAndPasscode()
    {
        CurlRunResult result = await StompRun.RunAsync(StompRun.PublishAnswered(), "-u", "al\\ice:pa:ss", "-d", "hi", "stomp://mq.example/q");

        StompRun.AssertExit(result, 0, string.Empty);
        StringAssert.StartsWith(
            StompRun.ClientText(result),
            "CONNECT\naccept-version:1.2\nhost:mq.example\nheart-beat:0,0\nlogin:al\\ice\npasscode:pa:ss\n\n\0SEND\n");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Encoding-4")]
    public async Task Subscribe_ConnectedHeaderWithBackslash_IsNotUnescaped()
    {
        CurlRunResult result = await StompRun.RunAsync(
            StompRun.SubscribeAnswered("CONNECTED\nversion:1.2\nserver:Ref\\tBroker\n\n\0"),
            "stomp://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty);
        Assert.AreEqual(StompRun.PlainConnect + StompRun.SubscribeQ, StompRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Negotiation-1")]
    public async Task Subscribe_ServerChoosesVersion11_FailsBeforeSubscribing()
    {
        CurlRunResult result = await StompRun.RunAsync(
            [ScriptStep.ReceiveUntil("\0"), ScriptStep.Send("CONNECTED\nversion:1.1\n\n\0")],
            "stomp://localhost/q");

        StompRun.AssertExit(result, 8, "curl: (8) STOMP server speaks version 1.1, not 1.2\n");
        Assert.AreEqual(StompRun.PlainConnect, StompRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Negotiation-1")]
    public async Task Subscribe_ConnectedWithoutVersion_IsTakenAsStomp10AndRefused()
    {
        CurlRunResult result = await StompRun.RunAsync(
            [ScriptStep.ReceiveUntil("\0"), ScriptStep.Send("CONNECTED\nserver:ref\n\n\0")],
            "stomp://localhost/q");

        StompRun.AssertExit(result, 8, "curl: (8) STOMP server speaks version 1.0, not 1.2\n");
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Connect-5")]
    [SpecRequirement("Stomp:STOMP12-Negotiation-2")]
    [SpecRequirement("Stomp:STOMP12-Error-1")]
    [SpecRequirement("Stomp:STOMP12-Error-2")]
    public async Task Publish_ErrorAtConnect_FailsWithItsMessageAndSendsNothingMore()
    {
        CurlRunResult result = await StompRun.RunAsync(
            [ScriptStep.ReceiveUntil("\0"), ScriptStep.Send("ERROR\nmessage:Supported protocol versions are 1.0\ncontent-type:text/plain\ncontent-length:3\n\nbad\0")],
            "-d",
            "hi",
            "stomp://localhost/q");

        StompRun.AssertExit(result, 8, "curl: (8) STOMP server sent ERROR: Supported protocol versions are 1.0\n");
        Assert.AreEqual(StompRun.PlainConnect, StompRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Repeated-1")]
    public async Task Subscribe_RepeatedVersionInConnected_UsesTheFirst()
    {
        CurlRunResult result = await StompRun.RunAsync(
            StompRun.SubscribeAnswered("CONNECTED\nversion:1.2\nversion:1.1\n\n\0"),
            "stomp://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty);
        Assert.AreEqual(StompRun.PlainConnect + StompRun.SubscribeQ, StompRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Stomp:STOMP12-Transport-1")]
    public async Task Publish_Stomps_SpeaksTlsFromTheFirstByteOnPort61614()
    {
        CurlRunResult result = await StompRun.RunTlsAsync(StompRun.PublishAnswered(), "-d", "hi", "stomps://localhost/q");

        StompRun.AssertExit(result, 0, string.Empty);
        Assert.AreEqual(StompRun.PlainConnect + "SEND\ndestination:/q\ncontent-length:2\n\nhi\0" + StompRun.Disconnect, StompRun.ClientText(result));
    }
}
