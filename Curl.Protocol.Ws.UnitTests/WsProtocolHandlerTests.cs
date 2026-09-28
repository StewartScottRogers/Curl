using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the WebSocket upgrade against curl 8.21.0, measured on 2026-09-28 with
/// <c>Record-CurlExchange.ps1</c> (ADR-0128 and BL-580): what is sent, and the outcome of each
/// measured reply.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerTests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n";

    private const string Request =
        "GET /chat HTTP/1.1\r\n" +
        "Host: 127.0.0.1:47901\r\n" +
        "User-Agent: curl/8.21.0\r\n" +
        "Accept: */*\r\n" +
        "Upgrade: websocket\r\n" +
        "Sec-WebSocket-Version: 13\r\n" +
        "Sec-WebSocket-Key: " + FixedRandomSource.Key + "\r\n" +
        "Connection: Upgrade\r\n" +
        "\r\n";

    [TestMethod]
    public void SupportedSchemes_AreWsAndWss()
    {
        CollectionAssert.AreEqual(new[] { "ws", "wss" }, Handler(new ScriptedConnection()).SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WsProtocolHandler(null!, new RecordingAuthenticator(), new FixedRandomSource()));
    }

    [TestMethod]
    public void Constructor_NullAuthenticator_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WsProtocolHandler(Connector(new ScriptedConnection()), null!, new FixedRandomSource()));
    }

    [TestMethod]
    public void Constructor_NullRandomSource_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WsProtocolHandler(Connector(new ScriptedConnection()), new RecordingAuthenticator(), null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new ScriptedConnection()).ExecuteAsync(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_WsUrl_ConnectsWithoutTlsToTheUrlsPort()
    {
        RecordingConnector connector = Connector(new ScriptedConnection(Bytes(Head101)));
        await Handler(connector).ExecuteAsync(Context("ws://127.0.0.1:47901/chat"));

        Assert.AreEqual(new ConnectTarget("127.0.0.1", 47901, false), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_WssUrlWithoutPort_ConnectsWithTlsToPort443()
    {
        RecordingConnector connector = Connector(new ScriptedConnection(Bytes(Head101)));

        await Handler(connector).ExecuteAsync(Context("wss://example.invalid/"));

        Assert.AreEqual(new ConnectTarget("example.invalid", 443, true), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_Proxy_HandsItToTheConnector()
    {
        RecordingConnector connector = Connector(new ScriptedConnection(Bytes(Head101)));
        var proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 47901, null);

        await Handler(connector).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://example.invalid/"), Output = new MemoryStream(), Proxy = proxy });

        Assert.AreEqual(new ConnectTarget("example.invalid", 80, false) { Proxy = proxy }, connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsFailure()
    {
        var connector = new RecordingConnector(ConnectResult.Refused("Failed to connect to h port 80"));

        TransferResult result = await Handler(connector).ExecuteAsync(Context("ws://h/"));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to h port 80", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_101_SendsCurlsRequestAndSucceedsWithCode101()
    {
        var connection = new ScriptedConnection(Bytes(Head101));
        var output = new MemoryStream();

        TransferResult result = await Handler(connection).ExecuteAsync(Context("ws://127.0.0.1:47901/chat", output));

        Assert.AreEqual(Request, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual(HttpVersion.Version11, result.Report.HttpVersion);
        Assert.AreEqual("GET", result.Report.Method);
        Assert.AreEqual(0, output.Length);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderAgentAndUser_SendsThemWithThePreemptiveAuthorization()
    {
        var connection = new ScriptedConnection(Bytes(Head101));
        var authenticator = new RecordingAuthenticator("Basic dXNlcjpwdw==");
        var credential = new NetworkCredential("user", "pw");
        var options = new HttpRequestOptions { Headers = ["X-Test: 1"], UserAgent = "agent/1", BearerToken = "t", AuthSchemes = HttpAuthSchemes.Basic };

        await Handler(Connector(connection), authenticator).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/p?q=1"), Output = new MemoryStream(), Http = options, Credentials = credential });

        Assert.AreEqual(
            "GET /p?q=1 HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "Authorization: Basic dXNlcjpwdw==\r\n" +
            "User-Agent: agent/1\r\n" +
            "Accept: */*\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            "Sec-WebSocket-Key: " + FixedRandomSource.Key + "\r\n" +
            "X-Test: 1\r\n" +
            "Connection: Upgrade\r\n" +
            "\r\n",
            Encoding.Latin1.GetString(connection.Sent));
        HttpAuthRequest asked = authenticator.Requests.Single();
        Assert.AreEqual(new HttpAuthRequest("GET", asked.Url, "/p?q=1", credential, "t", HttpAuthSchemes.Basic, false), asked);
        Assert.AreEqual(0, authenticator.Challenges.Single().Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomMethod_SendsItAndAsksAuthorizationForIt()
    {
        var connection = new ScriptedConnection(Bytes(Head101));
        var authenticator = new RecordingAuthenticator();

        TransferResult result = await Handler(Connector(connection), authenticator).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h:81/"), Output = new MemoryStream(), Http = new HttpRequestOptions { CustomMethod = "POST" } });

        StringAssert.StartsWith(Encoding.Latin1.GetString(connection.Sent), "POST / HTTP/1.1\r\n");
        Assert.AreEqual("POST", authenticator.Requests.Single().Method);
        Assert.AreEqual("POST", result.Report!.Method);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithAWrongAcceptValue_StillSucceeds()
    {
        string head = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: wrong\r\n\r\n";

        TransferResult result = await Handler(new ScriptedConnection(Bytes(head))).ExecuteAsync(Context("ws://h/"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithoutUpgrade_StillSucceeds()
    {
        TransferResult result = await Handler(new ScriptedConnection(Bytes("HTTP/1.1 101 Switching Protocols\r\n\r\n"))).ExecuteAsync(Context("ws://h/"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 200)]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n", 401)]
    [DataRow("http/1.1 101 Sw\r\n\r\n", 200)]
    public async Task ExecuteAsync_StatusOtherThan101_RefusesTheUpgradeWithExit22(string reply, int statusCode)
    {
        var output = new MemoryStream();

        TransferResult result = await Handler(new ScriptedConnection(Bytes(reply))).ExecuteAsync(Context("ws://h/", output));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual($"Refused WebSocket upgrade: {statusCode}", result.ErrorMessage);
        Assert.AreEqual(statusCode, result.Report!.ResponseCode);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputOn101_WritesTheHeadByteForByte()
    {
        var headers = new MemoryStream();

        await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), HeaderOutput = headers });

        Assert.AreEqual(Head101, Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputOnRefusal_WritesTheHeadButNotTheBody()
    {
        var headers = new MemoryStream();
        var output = new MemoryStream();

        await Handler(new ScriptedConnection(Bytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = output, HeaderOutput = headers });

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n", Encoding.Latin1.GetString(headers.ToArray()));
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputAcceptsPart_FailsWithWriteErrorNamingWhatItTook()
    {
        var headers = new FailingStream(new OutputWriteFailedException(5, "short"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), HeaderOutput = headers });

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"Failure writing output to destination, passed {Head101.Length} returned 5", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputFails_FailsWithWriteErrorReturningZero()
    {
        var headers = new FailingStream(new IOException("disk full"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), HeaderOutput = headers });

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"Failure writing output to destination, passed {Head101.Length} returned 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendResetByThePeer_FailsWithSendFailure()
    {
        var reset = new IOException("reset", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset));

        TransferResult result = await Handler(new FailingConnection(writeFailure: reset)).ExecuteAsync(Context("ws://h/"));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FlushFails_FailsWithSendFailure()
    {
        TransferResult result = await Handler(new FailingConnection(flushFailure: new IOException("broken"))).ExecuteAsync(Context("ws://h/"));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoReply_FailsWithEmptyReply()
    {
        var connection = new ScriptedConnection();

        TransferResult result = await Handler(connection).ExecuteAsync(Context("ws://h/"));

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2StatusLine_FailsWithExit1()
    {
        TransferResult result = await Handler(new ScriptedConnection(Bytes("HTTP/2 101 Sw\r\n\r\n"))).ExecuteAsync(Context("ws://h/"));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Unsupported HTTP version (2.0) in response", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await Handler(new ScriptedConnection(Bytes(Head101))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), CancellationToken = cancellation.Token }));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static RecordingConnector Connector(IConnection connection) => new(ConnectResult.Connected(connection));

    private static WsProtocolHandler Handler(IConnection connection) => Handler(Connector(connection));

    private static WsProtocolHandler Handler(IConnector connector, RecordingAuthenticator? authenticator = null) =>
        new(connector, authenticator ?? new RecordingAuthenticator(), new FixedRandomSource());

    private static TransferContext Context(string url, Stream? output = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output ?? new MemoryStream() };
}
