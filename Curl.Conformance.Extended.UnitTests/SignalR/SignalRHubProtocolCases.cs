using System.Text.Json;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;
using static Curl.Conformance.Extended.UnitTests.SignalR.SignalRRun;

namespace Curl.Conformance.Extended.UnitTests.SignalR;

/// <summary>HubProtocol.md (JSON) cases: the handshake, invocations, streams, hub calls, Ping and Close.</summary>
[TestClass]
public sealed class SignalRHubProtocolCases
{
    [TestMethod]
    [SpecRequirement("SignalR:HUB-Handshake-1")]
    [SpecRequirement("SignalR:HUB-Json-1")]
    [SpecRequirement("SignalR:HUB-Invocation-1")]
    [SpecRequirement("SignalR:HUB-Completion-1")]
    public async Task Invocation_SendsHandshakeThenInvocationAndWritesTheCompletionResult()
    {
        SignalRReferenceHub hub = CallHub(SignalRHubStep.Records("""{"type":3,"invocationId":"1","result":{"sum":3}}"""));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("{\"sum\":3}\n", Text(result.StandardOutput));
        Assert.AreEqual(string.Empty, Text(result.StandardError));
        IReadOnlyList<string> texts = hub.ClientTexts;
        using JsonDocument handshake = Message(texts[0]);
        Assert.AreEqual("json", handshake.RootElement.GetProperty("protocol").GetString());
        Assert.AreEqual(1, handshake.RootElement.GetProperty("version").GetInt32());
        using JsonDocument invocation = Message(texts[1]);
        Assert.AreEqual(1, invocation.RootElement.GetProperty("type").GetInt32());
        Assert.AreEqual("1", invocation.RootElement.GetProperty("invocationId").GetString());
        Assert.AreEqual("Add", invocation.RootElement.GetProperty("target").GetString());
        Assert.AreEqual("[1,2]", invocation.RootElement.GetProperty("arguments").GetRawText());
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Completion-2")]
    public async Task CompletionWithError_FailsWithExit22AndTheError()
    {
        SignalRReferenceHub hub = CallHub(SignalRHubStep.Records("""{"type":3,"invocationId":"1","error":"Add is not allowed"}"""));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(22, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual("curl: (22) SignalR hub method failed: Add is not allowed\n", Error(result));
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Completion-3")]
    public async Task CompletionWithNeitherResultNorError_EndsWithNoLine()
    {
        SignalRReferenceHub hub = CallHub(SignalRHubStep.Records("""{"type":3,"invocationId":"1"}"""));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Stream-1")]
    [SpecRequirement("SignalR:HUB-Stream-2")]
    public async Task StreamInvocation_WritesEachItemAndEndsAtItsCompletion()
    {
        SignalRReferenceHub hub = CallHub(
            SignalRHubStep.Records("""{"type":2,"invocationId":"1","item":1}""", """{"type":2,"invocationId":"1","item":"two"}"""),
            SignalRHubStep.Records("""{"type":2,"invocationId":"9","item":"not ours"}""", """{"type":2,"invocationId":"1","item":{"n":3}}""", """{"type":3,"invocationId":"1"}"""));

        CurlRunResult result = await RunAsync(hub, "-d", StreamCall, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("1\n\"two\"\n{\"n\":3}\n", Text(result.StandardOutput));
        using JsonDocument invocation = Message(hub.ClientTexts[1]);
        Assert.AreEqual(4, invocation.RootElement.GetProperty("type").GetInt32());
        Assert.AreEqual("1", invocation.RootElement.GetProperty("invocationId").GetString());
        Assert.AreEqual("Count", invocation.RootElement.GetProperty("target").GetString());
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Invocation-2")]
    [SpecRequirement("SignalR:HUB-Close-1")]
    public async Task HubCallFromTheServer_WritesItAsAnArrayAndSendsNoCompletion()
    {
        SignalRReferenceHub hub = new(
            [Version1()],
            SignalRHubStep.Receive,
            SignalRHubStep.Records("{}", """{"type":1,"target":"Notify","arguments":["hello",42]}"""),
            SignalRHubStep.Records("""{"type":7}"""));

        CurlRunResult result = await RunAsync(hub, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("[\"Notify\",\"hello\",42]\n", Text(result.StandardOutput));
        Assert.HasCount(1, hub.ClientTexts);
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Invocation-3")]
    public async Task HubCallWithAnInvocationId_IsAnsweredWithACompletion()
    {
        SignalRReferenceHub hub = new(
            [Version1()],
            SignalRHubStep.Receive,
            SignalRHubStep.Records("{}", """{"type":1,"invocationId":"s1","target":"Ask","arguments":[]}"""),
            SignalRHubStep.Receive,
            SignalRHubStep.Records("""{"type":7}"""));

        await SignalRRun.KnownFailures.RunCaseAsync(["SignalR:HUB-Invocation-3"], async () =>
        {
            CurlRunResult result = await RunAsync(hub, "-m", "3", Url);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsGreaterThanOrEqualTo(2, hub.ClientTexts.Count);
            using JsonDocument completion = Message(hub.ClientTexts[1]);
            Assert.AreEqual(3, completion.RootElement.GetProperty("type").GetInt32());
            Assert.AreEqual("s1", completion.RootElement.GetProperty("invocationId").GetString());
        });
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Ping-1")]
    public async Task Ping_IsIgnoredAndTheCallCompletes()
    {
        SignalRReferenceHub hub = CallHub(
            SignalRHubStep.Records("""{"type":6}"""),
            SignalRHubStep.Records("""{"type":6}""", """{"type":3,"invocationId":"1","result":"ok"}"""));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("\"ok\"\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Close-2")]
    public async Task CloseWithError_FailsWithExit22AndTheError()
    {
        SignalRReferenceHub hub = CallHub(SignalRHubStep.Records("""{"type":7,"error":"Connection closed with an error.","allowReconnect":false}"""));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(22, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        Assert.AreEqual("curl: (22) SignalR hub closed the connection: Connection closed with an error.\n", Error(result));
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Handshake-2")]
    [SpecRequirement("SignalR:HUB-Handshake-3")]
    public async Task HandshakeError_FailsWithExit22AndSendsNoCall()
    {
        SignalRReferenceHub hub = new([Version1()], SignalRHubStep.Receive, SignalRHubStep.Records("""{"error":"Requested protocol 'json' is not available."}"""));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(22, result.ExitCode);
        Assert.HasCount(1, hub.ClientTexts);
        Assert.AreEqual("curl: (22) SignalR hub refused the handshake: Requested protocol 'json' is not available.\n", Error(result));
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Json-2")]
    [SpecRequirement("SignalR:HUB-Handshake-4")]
    public async Task RecordsSplitAcrossFramesAndSeveralInOneFrame_AreEachRead()
    {
        SignalRReferenceHub hub = new(
            [Version1()],
            SignalRHubStep.Receive,
            SignalRHubStep.Text("{}\u001e{\"type\":1,\"target\":\"A\",\"arguments\":[1]}\u001e{\"type\":1,\"tar"),
            SignalRHubStep.Text("get\":\"B\",\"arguments\":[2]}"),
            SignalRHubStep.Text("\u001e{\"type\":6}\u001e{\"type\":7}\u001e"));

        CurlRunResult result = await RunAsync(hub, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("[\"A\",1]\n[\"B\",2]\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SignalR:HUB-Json-3")]
    public async Task RecordThatIsNotAHubMessage_FailsWithExit8()
    {
        SignalRReferenceHub hub = CallHub(SignalRHubStep.Records("[1,2]"));

        CurlRunResult result = await RunAsync(hub, "-d", Call, Url);

        Assert.AreEqual(8, result.ExitCode);
        StringAssert.StartsWith(Text(result.StandardError), "curl: (8) SignalR: ");
    }
}
