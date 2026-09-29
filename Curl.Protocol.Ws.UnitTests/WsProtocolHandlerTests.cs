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
    public async Task ExecuteAsync_101ThenTextAndClose_SendsCurlsRequestWritesBothPayloadsAndSucceedsWithCode101()
    {
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x05hello\x88\x02\x03\xe8"));
        var output = new MemoryStream();

        TransferResult result = await Handler(connection).ExecuteAsync(Context("ws://127.0.0.1:47901/chat", output));

        Assert.AreEqual(Request, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual(HttpVersion.Version11, result.Report.HttpVersion);
        Assert.AreEqual("GET", result.Report.Method);
        Assert.AreEqual("hello\x03\xe8", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(11L, result.BytesTransferred);
        Assert.AreEqual(11L, result.Report.DownloadSize);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoTextMessagesThenClose_WritesOnetwoAndReportsCurlsSizes()
    {
        // curl -w '%{size_download} %{http_code} %{size_upload} %{size_header} %{size_request}':
        // "onetwo" 03 e8, then 14 101 0 <head> <request>.
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x03one\x81\x03two\x88\x02\x03\xe8"));
        var output = new MemoryStream();

        TransferResult result = await Handler(connection).ExecuteAsync(Context("ws://127.0.0.1:47901/chat", output));

        Assert.AreEqual("onetwo\x03\xe8", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(14L, result.Report!.DownloadSize);
        Assert.AreEqual(8L, result.Report.DeliveredSize);
        Assert.AreEqual(0L, result.Report.UploadSize);
        Assert.AreEqual(Head101.Length, result.Report.HeaderSize);
        Assert.AreEqual(Request.Length, result.Report.RequestSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_FragmentedBinaryMessage_WritesItsBytesAndCountsEveryFrameHead()
    {
        // Measured: 01 02 03 on stdout, size_download 9, size_delivered 3.
        var output = new MemoryStream();

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101), Bytes("\x02\x02\x01\x02"), Bytes("\x80\x01\x03\x88\x00"))).ExecuteAsync(Context("ws://h/", output));

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(9L, result.Report!.DownloadSize);
        Assert.AreEqual(3L, result.Report.DeliveredSize);
    }

    [TestMethod]
    [DataRow("\x81\x05hello", "hello", 7L, 5L)]
    [DataRow("\x81\x05hel", "hel", 5L, 3L)]
    public async Task ExecuteAsync_ServerDropsTheConnectionWithoutAClose_SucceedsWithWhatArrived(string frames, string written, long downloadSize, long deliveredSize)
    {
        var output = new MemoryStream();

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + frames))).ExecuteAsync(Context("ws://h/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(written, Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(downloadSize, result.Report!.DownloadSize);
        Assert.AreEqual(deliveredSize, result.Report.DeliveredSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_101ThenNoFrame_FailsWithEmptyReplyAndKeepsCode101()
    {
        // Measured: exit 52, "Empty reply from server", -w "0 101 0 102 192".
        var headers = new MemoryStream();
        var output = new MemoryStream();

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = output, HeaderOutput = headers });

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual(0L, result.Report.DownloadSize);
        Assert.AreEqual(Head101, Encoding.Latin1.GetString(headers.ToArray()));
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_SendsItAsOneMaskedBinaryFrameAndCountsItAsUploadAndRequest()
    {
        // Measured with -T - and "abc": 82 83 <mask> <masked abc> after the request;
        // -w "4 101 9 102 201": size_request is the request plus the frame.
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"));
        var output = new MemoryStream();
        var progress = new RecordingProgress();
        var context = new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = output, Upload = new MemoryStream(Bytes("abc")), Progress = progress };

        TransferResult result = await Handler(connection).ExecuteAsync(context);

        Assert.AreEqual(Request + "\x82\x83\x00\x01\x02\x03\x61\x63\x61", Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(4L, result.Report!.DownloadSize);
        Assert.AreEqual(9L, result.Report.UploadSize);
        Assert.AreEqual(Request.Length + 9L, result.Report.RequestSize);
        CollectionAssert.AreEqual(new[] { "started", "down 4/?", "up 9/9", "done" }, progress.Reports.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadReadFails_SendsWhatWasReadBeforeTheFailure()
    {
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"));
        var context = new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = new MemoryStream(), Upload = new FailingUploadStream(Bytes("a"), new IOException("gone")) };

        TransferResult result = await Handler(connection).ExecuteAsync(context);

        Assert.AreEqual(Request + "\x82\x81\x00\x01\x02\x03\x61", Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(7L, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostData_IsNotSent()
    {
        // Measured: -d xyz sends nothing after the request, and the method stays GET.
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"));

        await Handler(connection).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = new MemoryStream(), PostData = Bytes("xyz") });

        Assert.AreEqual(Request, Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_Ping_CountsThePongAsRequestBytes()
    {
        // Measured: ping "hi" then "ok" gives -w "8 101 0 102 200": the 8-byte pong is in size_request.
        var connection = new ScriptedConnection(Bytes(Head101 + "\x89\x02hi\x81\x02ok"));
        var output = new MemoryStream();

        TransferResult result = await Handler(connection).ExecuteAsync(Context("ws://127.0.0.1:47901/chat", output));

        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(8L, result.Report!.DownloadSize);
        Assert.AreEqual(0L, result.Report.UploadSize);
        Assert.AreEqual(Request.Length + 8L, result.Report.RequestSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ViolationAfterAFrame_FailsWith56AndKeepsTheReport()
    {
        var output = new MemoryStream();

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"), Bytes("\x81\x85\x01\x02\x03\x04"))).ExecuteAsync(Context("ws://h/", output));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("[WS] masked input frame", result.ErrorMessage);
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(10L, result.BytesTransferred);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual(10L, result.Report.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputWriteFails_FailsWithWriteErrorNamingWhatItTook()
    {
        var output = new FailingStream(new OutputWriteFailedException(1, "short"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"))).ExecuteAsync(Context("ws://h/", output));

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 2 returned 1", result.ErrorMessage);
        Assert.AreEqual(101, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTimeRunsOutWhileTheServerHoldsTheConnection_LetsTheCancellationEscapeAfterReportingTheBytes()
    {
        // Measured: -m 1 after "ok" ends with 28 "... with 4 bytes received", from the runner (ADR-0117).
        using var cancellation = new CancellationTokenSource();
        var progress = new RecordingProgress();
        var output = new CancellingStream(cancellation);
        var context = new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = output, Progress = progress, CancellationToken = cancellation.Token };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"))).ExecuteAsync(context));

        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new[] { "started", "down 4/?" }, progress.Reports.ToArray());
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
    public async Task ExecuteAsync_Negotiate_SendsTheContextsTokenOnTheUpgradeRequest()
    {
        var connection = new ScriptedConnection(Bytes(Head101));
        var authenticator = new AsynchronousOnlyAuthenticator("Negotiate YIIB");
        var credential = new NetworkCredential(string.Empty, string.Empty);
        var options = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Negotiate };

        await new WsProtocolHandler(Connector(connection), authenticator, new FixedRandomSource()).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/"), Output = new MemoryStream(), Http = options, Credentials = credential });

        Assert.AreEqual(
            "GET / HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "Authorization: Negotiate YIIB\r\n" +
            "User-Agent: curl/8.21.0\r\n" +
            "Accept: */*\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            "Sec-WebSocket-Key: " + FixedRandomSource.Key + "\r\n" +
            "Connection: Upgrade\r\n" +
            "\r\n",
            Encoding.Latin1.GetString(connection.Sent));
        HttpAuthRequest asked = authenticator.Requests.Single();
        Assert.AreEqual(HttpAuthSchemes.Negotiate, asked.AllowedSchemes);
        Assert.AreSame(credential, asked.Credential);
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
    public async Task ExecuteAsync_NoBody_SendsHeadReadsNoFramesAndFailsWithEmptyReply()
    {
        // curl -sS -I -w '%{http_code} %{size_download} %{size_header} %{size_request}' against a
        // 101 with 81 05 "hello" 88 02 03 e8 (BL-583): HEAD, no output, 101 0 <head> <request>, exit 52.
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x05hello"), Bytes("\x88\x02\x03\xe8"));
        var output = new MemoryStream();
        var progress = new RecordingProgress();

        TransferResult result = await Handler(connection).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = output, NoBody = true, Progress = progress });

        Assert.AreEqual(string.Concat("HEAD", Request.AsSpan(3)), Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual("HEAD", result.Report.Method);
        Assert.AreEqual(0L, result.Report.DownloadSize);
        Assert.AreEqual(Head101.Length, result.Report.HeaderSize);
        Assert.AreEqual(Request.Length + 1, result.Report.RequestSize);
        Assert.AreEqual("done", progress.Reports[^1]);
        Assert.AreEqual(4, await connection.ReadAsync(new byte[16], CancellationToken.None));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyWithCustomMethod_KeepsTheCustomMethod()
    {
        // curl -I -X POST ws://... sends POST and still ends with 52 (BL-788 Notes).
        var connection = new ScriptedConnection(Bytes(Head101));

        TransferResult result = await Handler(connection).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), NoBody = true, Http = new HttpRequestOptions { CustomMethod = "POST" } });

        StringAssert.StartsWith(Encoding.Latin1.GetString(connection.Sent), "POST / HTTP/1.1\r\n");
        Assert.AreEqual("POST", result.Report!.Method);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithAWrongAcceptValue_StillSucceeds()
    {
        string head = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: wrong\r\n\r\n\x81\x02ok";

        TransferResult result = await Handler(new ScriptedConnection(Bytes(head))).ExecuteAsync(Context("ws://h/"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithoutUpgrade_StillSucceeds()
    {
        TransferResult result = await Handler(new ScriptedConnection(Bytes("HTTP/1.1 101 Switching Protocols\r\n\r\n\x81\x02ok"))).ExecuteAsync(Context("ws://h/"));

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
