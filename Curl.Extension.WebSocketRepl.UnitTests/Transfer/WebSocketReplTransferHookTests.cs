using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebSocketRepl.Transfer;

/// <summary>Pins how <c>--ws-repl</c> takes over a <c>ws://</c> or <c>wss://</c> transfer (ADR-1081, RFC 6455).</summary>
[TestClass]
public sealed class WebSocketReplTransferHookTests
{
    private static readonly byte[] RfcResponse = Encoding.ASCII.GetBytes(
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
        + "Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n");

    public TestContext TestContext { get; set; } = null!;

    // RFC 6455 section 4.1: the request goes to the URL's resource, with the port in Host when it is not the default;
    // sections 5.6 and 5.5.1: a text message is shown, and the server's close is answered and ends the session.
    [TestMethod]
    public async Task ExecuteAsync_WsUrl_HandshakesShowsMessagesAndSucceedsOnTheServersClose()
    {
        ScriptedConnection connection = new((1, [.. RfcResponse, 0x81, 0x02, (byte)'h', (byte)'i', 0x88, 0x02, 0x03, 0xE8]));
        RecordingConnector connector = new(ConnectResult.Connected(connection));
        TransferContext context = Context("ws://server.example.com:8080/chat?room=1");

        TransferResult result = await Hook(new ScriptedLineReader(false)).ExecuteAsync(context, Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hi\n", Encoding.UTF8.GetString(((MemoryStream)context.Output).ToArray()));
        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual(("server.example.com", 8080, false), (target.Host, target.Port, target.UseTls));
        StringAssert.StartsWith(
            Encoding.ASCII.GetString(connection.Writes[0]), "GET /chat?room=1 HTTP/1.1\r\nHost: server.example.com:8080\r\n");
    }

    // RFC 6455 section 3: wss uses TLS, on port 443 by default; the Host header then carries no port.
    [TestMethod]
    public async Task ExecuteAsync_WssUrlOnTheDefaultPort_ConnectsWithTlsAndSucceedsWhenTheConnectionEnds()
    {
        ScriptedConnection connection = new((1, RfcResponse));
        RecordingConnector connector = new(ConnectResult.Connected(connection));

        TransferResult result = await Hook(new ScriptedLineReader(false))
            .ExecuteAsync(Context("WSS://server.example.com"), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual(("server.example.com", 443, true), (target.Host, target.Port, target.UseTls));
        StringAssert.StartsWith(Encoding.ASCII.GetString(connection.Writes[0]), "GET / HTTP/1.1\r\nHost: server.example.com\r\n");
    }

    // RFC 6455 section 4.1: a response other than 101 fails the WebSocket connection.
    [TestMethod]
    public async Task ExecuteAsync_HandshakeRefused_FailsAsAWeirdServerReply()
    {
        ScriptedConnection connection = new((1, "HTTP/1.1 400 Bad Request\r\n\r\n"u8.ToArray()));

        TransferResult result = await Hook(new ScriptedLineReader(true))
            .ExecuteAsync(Context("ws://server.example.com/chat"), Services(new RecordingConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "Refused WebSocket upgrade: ");
    }

    // RFC 6455 section 5.1: a client fails the connection on a masked frame from the server.
    [TestMethod]
    public async Task ExecuteAsync_ServerSendsAMaskedFrame_FailsAsARecvError()
    {
        ScriptedConnection connection = new((1, [.. RfcResponse, 0x81, 0x81, 0, 0, 0, 0, (byte)'x']));

        TransferResult result = await Hook(new ScriptedLineReader(false))
            .ExecuteAsync(Context("ws://server.example.com/chat"), Services(new RecordingConnector(ConnectResult.Connected(connection))), NeverSent);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("WebSocket protocol violation: [WS] masked input frame", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_FailsWithTheConnectorsExitCode()
    {
        RecordingConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused"));

        TransferResult result = await Hook(new ScriptedLineReader(true))
            .ExecuteAsync(Context("ws://server.example.com/chat"), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server.example.com: refused", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpUrl_LeavesTheTransferToCurl()
    {
        RecordingConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "unused"));
        TransferContext context = Context("http://server.example.com/");
        ITransferContext? sent = null;

        TransferResult result = await Hook(new ScriptedLineReader(true)).ExecuteAsync(context, Services(connector), given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(7));
        });

        Assert.AreSame(context, sent);
        Assert.AreEqual(7, result.BytesTransferred);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutServices_FailsAsAFailedInit()
    {
        TransferResult result = await Hook(new ScriptedLineReader(true)).ExecuteAsync(Context("ws://server.example.com/"), NeverSent);

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual(WebSocketReplTransferHook.NoServicesMessage, result.ErrorMessage);
    }

    private static WebSocketReplTransferHook Hook(TextReader input) =>
        new(input, new FixedRandomSource(Encoding.ASCII.GetBytes("the sample nonce")));

    private static TransferHookServices Services(IConnector connector) => new(connector, new UnusedListener());

    private static ValueTask<TransferResult> NeverSent(ITransferContext context) => throw new AssertFailedException("curl's handler ran.");

    private TransferContext Context(string url) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        CancellationToken = TestContext.CancellationToken,
    };
}
