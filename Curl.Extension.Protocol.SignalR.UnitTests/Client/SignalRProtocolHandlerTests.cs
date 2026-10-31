using System.Text;
using Curl.Extension.Protocol.SignalR.Transport;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.SignalR.Client;

/// <summary>
/// Tests the whole client flow against TransportProtocols.md ("POST [endpoint-base]/negotiate request", "WebSockets
/// (Full Duplex)") and HubProtocol.md ("Overview", "Invocation", "StreamInvocation", "StreamItem", "Completion",
/// "Close"), over scripted connections. The mask source gives zero bytes, so each client frame carries its text as is.
/// </summary>
[TestClass]
public sealed class SignalRProtocolHandlerTests
{
    private const string NegotiateBody =
        "{\"connectionToken\":\"tok\",\"connectionId\":\"cid\",\"negotiateVersion\":1,"
        + "\"availableTransports\":[{\"transport\":\"WebSockets\",\"transferFormats\":[\"Text\",\"Binary\"]}]}";

    private const string HandshakeRequest = "{\"protocol\":\"json\",\"version\":1}\u001e";

    private static readonly string Upgraded =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "
        + SignalRWebSocketUpgrade.AcceptFor(Convert.ToBase64String(new byte[16])) + "\r\n\r\n";

    [TestMethod]
    public async Task Invocation_SendsHandshakeAndInvocationWithId_WritesTheCompletionResult_HubProtocolInvocationAndCompletion()
    {
        ScriptedConnection negotiate = Negotiate();
        ScriptedConnection webSocket = Hub("{}", "{\"type\":3,\"invocationId\":\"1\",\"result\":{\"sum\":3}}");
        var connector = new ScriptedConnector(ConnectResult.Connected(negotiate), ConnectResult.Connected(webSocket));
        var output = new MemoryStream();

        TransferResult result = await Run(connector, "signalr://example.com:5000/chat?room=a", output, "{\"target\":\"Add\",\"arguments\":[1,2]}");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("{\"sum\":3}\n", Encoding.UTF8.GetString(output.ToArray()));
        StringAssert.StartsWith(negotiate.WrittenText, "POST /chat/negotiate?room=a&negotiateVersion=1 HTTP/1.1\r\n");
        StringAssert.StartsWith(webSocket.WrittenText, "GET /chat?room=a&id=tok HTTP/1.1\r\n");
        StringAssert.Contains(webSocket.WrittenText, HandshakeRequest);
        StringAssert.Contains(webSocket.WrittenText, "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Add\",\"arguments\":[1,2]}\u001e");
        Assert.IsTrue(webSocket.IsDisposed);
        Assert.AreEqual(("example.com", 5000, false), (connector.Targets[0].Host, connector.Targets[0].Port, connector.Targets[0].UseTls));
        Assert.AreEqual(("example.com", 5000, false), (connector.Targets[1].Host, connector.Targets[1].Port, connector.Targets[1].UseTls));
    }

    [TestMethod]
    public async Task StreamInvocation_WritesEachStreamItemOfItsInvocation_HubProtocolStreamInvocationAndStreamItem()
    {
        ScriptedConnection webSocket = Hub(
            "{}",
            "{\"type\":6}",
            "{\"type\":2,\"invocationId\":\"1\",\"item\":1}",
            "{\"type\":2,\"invocationId\":\"9\",\"item\":\"other\"}",
            "{\"type\":3,\"invocationId\":\"9\"}",
            "{\"type\":2,\"invocationId\":\"1\",\"item\":2}",
            "{\"type\":3,\"invocationId\":\"1\"}");
        var output = new MemoryStream();

        TransferResult result = await Run(Connector(webSocket), "signalr://example.com/count", output, "{\"type\":4,\"target\":\"Counter\",\"arguments\":[2]}");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("1\n2\n", Encoding.UTF8.GetString(output.ToArray()));
        StringAssert.Contains(webSocket.WrittenText, "{\"type\":4,\"invocationId\":\"1\",\"target\":\"Counter\",\"arguments\":[2]}\u001e");
    }

    [TestMethod]
    public async Task Invocation_CompletionWithoutResult_WritesNothing_HubProtocolCompletion()
    {
        var output = new MemoryStream();

        TransferResult result = await Run(Connector(Hub("{}", "{\"type\":3,\"invocationId\":\"1\"}")), "signalr://example.com/hub", output, "{\"target\":\"Go\"}");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task WithoutData_WritesEachHubInvocationAsAnArray_UntilClose_HubProtocolInvocationAndClose()
    {
        ScriptedConnection webSocket = Hub(
            "{}",
            "{\"type\":1,\"target\":\"Send\",\"arguments\":[\"a\",1]}",
            "{\"type\":99}",
            "{\"type\":7}",
            "{\"type\":1,\"target\":\"Late\",\"arguments\":[]}");
        var output = new MemoryStream();

        TransferResult result = await Run(Connector(webSocket), "signalr://example.com/hub", output, null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("[\"Send\",\"a\",1]\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.IsFalse(webSocket.WrittenText.Contains("\"type\":1", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task WithoutData_WebSocketCloses_EndsWithSuccess_TransportProtocolsWebSockets()
    {
        ScriptedConnection webSocket = new([.. Encoding.Latin1.GetBytes(Upgraded), .. Frame(0x81, "{}\u001e"), .. Frame(0x88, "\u0003è")]);

        TransferResult result = await Run(Connector(webSocket), "signalr://example.com/hub", new MemoryStream(), null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow("{\"error\":\"Unsupported protocol\"}", "SignalR hub refused the handshake: Unsupported protocol")]
    [DataRow("{\"type\":3,\"invocationId\":\"1\",\"error\":\"boom\"}", "SignalR hub method failed: boom")]
    [DataRow("{\"type\":7,\"error\":\"bye\"}", "SignalR hub closed the connection: bye")]
    public async Task HubErrors_FailWithExit22_HubProtocolOverviewCompletionAndClose(string record, string message)
    {
        string[] records = record.StartsWith("{\"error\"", StringComparison.Ordinal) ? [record] : ["{}", record];

        TransferResult result = await Run(Connector(Hub(records)), "signalr://example.com/hub", new MemoryStream(), "{\"target\":\"Go\"}");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task WebSocketClosedBeforeHandshakeResponse_FailsWithExit8()
    {
        ScriptedConnection webSocket = new([.. Encoding.Latin1.GetBytes(Upgraded), .. Frame(0x88, "\u0003è")]);

        TransferResult result = await Run(Connector(webSocket), "signalr://example.com/hub", new MemoryStream(), null);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("SignalR: The hub closed the WebSocket before answering the handshake.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task RecordThatIsNotAHubMessage_FailsWithExit8_HubProtocolJsonEncoding()
    {
        TransferResult result = await Run(Connector(Hub("{}", "[1]")), "signalr://example.com/hub", new MemoryStream(), null);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "SignalR: ");
    }

    [TestMethod]
    public async Task NegotiateFails_FailsWithExit8_TransportProtocolsNegotiate()
    {
        ScriptedConnection negotiate = ScriptedConnection.FromText("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n");

        TransferResult result = await Run(new ScriptedConnector(ConnectResult.Connected(negotiate)), "signalr://example.com/hub", new MemoryStream(), null);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("SignalR: The negotiate request failed with HTTP status 500.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectRefused_GivesTheConnectorsResult()
    {
        TransferResult result = await Run(new ScriptedConnector(ConnectResult.Refused("Failed to connect")), "signalrs://example.com/hub", new MemoryStream(), null);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [DataRow("not json", "SignalR: -d must be a JSON object {\"type\":1|4,\"target\":\"Method\",\"arguments\":[...]}: ")]
    [DataRow("{\"type\":2,\"target\":\"X\"}", "SignalR: -d must be a JSON object {\"type\":1|4,\"target\":\"Method\",\"arguments\":[...]}: type 2 is neither 1 nor 4")]
    [DataRow("{\"arguments\":[]}", "SignalR: -d must be a JSON object {\"type\":1|4,\"target\":\"Method\",\"arguments\":[...]}: ")]
    public async Task BadData_FailsWithExit2BeforeConnecting(string data, string messageStart)
    {
        var connector = new ScriptedConnector();

        TransferResult result = await Run(connector, "signalr://example.com/hub", new MemoryStream(), data);

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, messageStart);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new SignalRProtocolHandler(new ScriptedConnector(), new RepeatingRandomSource(0)).ExecuteAsync(null!));

    [TestMethod]
    [DataRow("signalr://example.com/chat", "http://example.com/chat")]
    [DataRow("signalrs://example.com/chat", "https://example.com/chat")]
    [DataRow("SIGNALRS://example.com:8443/chat?a=b", "https://example.com:8443/chat?a=b")]
    [DataRow("signalr://example.com", "http://example.com/")]
    public void EndpointUrlOf_MapsTheSchemeToHttpAndKeepsPortPathAndQuery(string url, string expected) =>
        Assert.AreEqual(expected, SignalRProtocolHandler.EndpointUrlOf(CurlUrl.Parse(url)).AbsoluteUri);

    [TestMethod]
    public void SupportedSchemes_AreSignalRAndSignalRs() =>
        CollectionAssert.AreEqual(new[] { "signalr", "signalrs" }, new SignalRProtocolHandler(new ScriptedConnector(), new RepeatingRandomSource(0)).SupportedSchemes.ToArray());

    private static ScriptedConnection Negotiate() =>
        ScriptedConnection.FromText($"HTTP/1.1 200 OK\r\nContent-Length: {NegotiateBody.Length}\r\n\r\n{NegotiateBody}");

    private static ScriptedConnection Hub(params string[] records) =>
        new([.. Encoding.Latin1.GetBytes(Upgraded), .. records.SelectMany(record => Frame(0x81, record + "\u001e"))]);

    private static ScriptedConnector Connector(ScriptedConnection webSocket) =>
        new(ConnectResult.Connected(Negotiate()), ConnectResult.Connected(webSocket));

    private static byte[] Frame(byte first, string payload)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(payload);
        return [first, (byte)bytes.Length, .. bytes];
    }

    private static ValueTask<TransferResult> Run(ScriptedConnector connector, string url, MemoryStream output, string? data) =>
        new SignalRProtocolHandler(connector, new RepeatingRandomSource(0)).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            PostData = data is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(data)),
        });
}
