using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;
using static Curl.Conformance.Extended.UnitTests.SignalR.SignalRRun;

namespace Curl.Conformance.Extended.UnitTests.SignalR;

/// <summary>RFC 6455 client rules the SignalR WebSocket transport relies on.</summary>
[TestClass]
public sealed class SignalRWebSocketCases
{
    [TestMethod]
    [SpecRequirement("SignalR:RFC6455-4.1-1")]
    [SpecRequirement("SignalR:RFC6455-5.1-1")]
    [SpecRequirement("SignalR:RFC6455-5.6-1")]
    public async Task OpeningHandshakeAndFrames_AreAClientsMaskedTextFrames()
    {
        SignalRReferenceHub hub = CallHub(SignalRHubStep.Records("""{"type":3,"invocationId":"1","result":3}"""));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        SignalRReferenceRequest upgrade = hub.Requests[1];
        Assert.AreEqual("GET", upgrade.Method);
        Assert.AreEqual("localhost:5000", upgrade.Header("Host"));
        Assert.AreEqual("websocket", upgrade.Header("Upgrade"), ignoreCase: true);
        StringAssert.Contains(upgrade.Header("Connection"), "Upgrade");
        Assert.AreEqual("13", upgrade.Header("Sec-WebSocket-Version"));
        Assert.HasCount(16, Convert.FromBase64String(upgrade.Header("Sec-WebSocket-Key")!));
        Assert.IsTrue(hub.ClientFrames.All(frame => frame.IsMasked), "Every client frame is masked.");
        Assert.IsTrue(hub.ClientFrames.Take(2).All(frame => frame.IsFinal && frame.Opcode == SignalRReferenceFrame.Text));
    }

    [TestMethod]
    [SpecRequirement("SignalR:RFC6455-4.1-2")]
    public async Task UpgradeAnsweredWithTheWrongAccept_FailsWithExit8AndSendsNoFrame()
    {
        SignalRReferenceHub hub = new([Version1()], SignalRHubStep.Receive) { AcceptOverride = "d3JvbmcgYWNjZXB0IHZhbHVlIQ==" };

        CurlRunResult result = await RunAsync(hub, Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.IsEmpty(hub.ClientFrames);
        Assert.AreEqual("curl: (8) SignalR: The server refused the WebSocket upgrade: it sent no Sec-WebSocket-Accept that answers the key.\n", Error(result));
    }

    [TestMethod]
    [SpecRequirement("SignalR:RFC6455-5.5.2-1")]
    public async Task WebSocketPing_IsAnsweredWithAPongCarryingItsPayload()
    {
        SignalRReferenceHub hub = CallHub(
            SignalRHubStep.Send(0x89, Encoding.ASCII.GetBytes("are you there")),
            SignalRHubStep.Receive,
            SignalRHubStep.Records("""{"type":3,"invocationId":"1","result":3}"""));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        SignalRReferenceFrame pong = hub.ClientFrames[2];
        Assert.AreEqual(SignalRReferenceFrame.Pong, pong.Opcode);
        Assert.AreEqual("are you there", Encoding.ASCII.GetString(pong.Payload));
        Assert.AreEqual("3\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SignalR:RFC6455-5.5.1-1")]
    public async Task WebSocketClose_IsAnsweredWithAClose()
    {
        SignalRReferenceHub hub = CallHub(SignalRHubStep.Send(0x88, [0x03, 0xE8]));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        SignalRReferenceFrame close = hub.ClientFrames[^1];
        Assert.AreEqual(SignalRReferenceFrame.Close, close.Opcode);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0xE8 }, close.Payload);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SignalR:RFC6455-5.4-1")]
    public async Task FragmentedTextMessage_IsReassembled()
    {
        SignalRReferenceHub hub = CallHub(
            SignalRHubStep.Send(0x01, Encoding.UTF8.GetBytes("""{"type":3,"invocationId":"1",""")),
            SignalRHubStep.Send(0x80, Encoding.UTF8.GetBytes("\"result\":\"" + new string('x', 200) + "\"}\u001e")));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("\"" + new string('x', 200) + "\"\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SignalR:RFC6455-1.4-1")]
    public async Task CallCompleted_ClosesTheWebSocketWithACloseFrame()
    {
        SignalRReferenceHub hub = CallHub(SignalRHubStep.Records("""{"type":3,"invocationId":"1","result":3}"""));

        await SignalRRun.KnownFailures.RunCaseAsync(["SignalR:RFC6455-1.4-1"], async () =>
        {
            CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(SignalRReferenceFrame.Close, hub.ClientFrames[^1].Opcode);
        });
    }
}
