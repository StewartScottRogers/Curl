using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SupportedSchemes_AreWsAndWss()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] expected = ["ws", "wss"];
        diagnostics.Arrange("handler", "WsProtocolHandler over an empty scripted connection");

        string[] actual = Handler(new ScriptedConnection()).SupportedSchemes.ToArray();

        diagnostics.Act("supported schemes", string.Join(",", actual));
        diagnostics.Assert("supported schemes", string.Join(",", expected), string.Join(",", actual));
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connector", "(null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new WsProtocolHandler(null!, new RecordingAuthenticator(), new FixedRandomSource()));

        diagnostics.Act("exception", $"{exception.GetType().Name} for parameter {exception.ParamName}");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void Constructor_NullAuthenticator_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("authenticator", "(null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new WsProtocolHandler(Connector(new ScriptedConnection()), null!, new FixedRandomSource()));

        diagnostics.Act("exception", $"{exception.GetType().Name} for parameter {exception.ParamName}");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void Constructor_NullRandomSource_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("random source", "(null)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new WsProtocolHandler(Connector(new ScriptedConnection()), new RecordingAuthenticator(), null!));

        diagnostics.Act("exception", $"{exception.GetType().Name} for parameter {exception.ParamName}");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context", "(null)");

        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new ScriptedConnection()).ExecuteAsync(null!));

        diagnostics.Act("exception", $"{exception.GetType().Name} for parameter {exception.ParamName}");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_WsUrl_ConnectsWithoutTlsToTheUrlsPort()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingConnector connector = Connector(new ScriptedConnection(Bytes(Head101)));
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(connector).ExecuteAsync(Context("ws://127.0.0.1:47901/chat"));

        ConnectTarget expected = new("127.0.0.1", 47901, false);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("connect target", connector.Targets.Single());
        diagnostics.Assert("connect target", expected, connector.Targets.Single());
        Assert.AreEqual(expected, connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_WssUrlWithoutPort_ConnectsWithTlsToPort443()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingConnector connector = Connector(new ScriptedConnection(Bytes(Head101)));
        diagnostics.Arrange("url", "wss://example.invalid/");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(connector).ExecuteAsync(Context("wss://example.invalid/"));

        ConnectTarget expected = new("example.invalid", 443, true);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("connect target", connector.Targets.Single());
        diagnostics.Assert("connect target", expected, connector.Targets.Single());
        Assert.AreEqual(expected, connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_Proxy_HandsItToTheConnector()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingConnector connector = Connector(new ScriptedConnection(Bytes(Head101)));
        var proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 47901, null);
        diagnostics.Arrange("url", "ws://example.invalid/");
        diagnostics.Arrange("proxy", "http 127.0.0.1:47901");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(connector).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://example.invalid/"), Output = new MemoryStream(), Proxy = proxy });

        ConnectTarget expected = new("example.invalid", 80, false) { Proxy = proxy };
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("connect target", connector.Targets.Single());
        diagnostics.Assert("connect target", expected, connector.Targets.Single());
        Assert.AreEqual(expected, connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connector = new RecordingConnector(ConnectResult.Refused("Failed to connect to h port 80"));
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("connector", "refuses: Failed to connect to h port 80");

        TransferResult result = await Handler(connector).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("result", Describe(result));
        diagnostics.Act("connection refused", result.IsConnectionRefused);
        diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        diagnostics.Assert("error message", "Failed to connect to h port 80", result.ErrorMessage);
        diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to h port 80", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_101ThenTextAndClose_SendsCurlsRequestWritesBothPayloadsAndSucceedsWithCode101()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x05hello\x88\x02\x03\xe8"));
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Bytes("scripted 101 and frames", Bytes(Head101 + "\x81\x05hello\x88\x02\x03\xe8"));

        TransferResult result;
        using (diagnostics.Phase("upgrade and frame exchange"))
        {
            result = await Handler(connection).ExecuteAsync(Context("ws://127.0.0.1:47901/chat", output));
        }

        string written = Encoding.Latin1.GetString(output.ToArray());
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("frames", "text FIN 1 length 5 'hello'; close FIN 1 length 2 code 1000");
        diagnostics.Act("response code and method", $"{result.Report!.ResponseCode} {result.Report.Method}");
        diagnostics.Act("bytes written", output.Length);
        diagnostics.Diff("upgrade request", Request, Encoding.Latin1.GetString(connection.Sent));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("response code", 101, result.Report.ResponseCode);
        diagnostics.Diff("output", "hello\x03\xe8", written);
        diagnostics.Assert("bytes transferred", 11L, result.BytesTransferred);
        diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.AreEqual(Request, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual(HttpVersion.Version11, result.Report.HttpVersion);
        Assert.AreEqual("GET", result.Report.Method);
        Assert.AreEqual("hello\x03\xe8", written);
        Assert.AreEqual(11L, result.BytesTransferred);
        Assert.AreEqual(11L, result.Report.DownloadSize);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoTextMessagesThenClose_WritesOnetwoAndReportsCurlsSizes()
    {
        // curl -w '%{size_download} %{http_code} %{size_upload} %{size_header} %{size_request}':
        // "onetwo" 03 e8, then 14 101 0 <head> <request>.
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x03one\x81\x03two\x88\x02\x03\xe8"));
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Bytes("scripted 101 and frames", Bytes(Head101 + "\x81\x03one\x81\x03two\x88\x02\x03\xe8"));

        TransferResult result;
        using (diagnostics.Phase("frame exchange"))
        {
            result = await Handler(connection).ExecuteAsync(Context("ws://127.0.0.1:47901/chat", output));
        }

        string written = Encoding.Latin1.GetString(output.ToArray());
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("sizes", $"download {result.Report!.DownloadSize}, delivered {result.Report.DeliveredSize}, upload {result.Report.UploadSize}, header {result.Report.HeaderSize}, request {result.Report.RequestSize}");
        diagnostics.Diff("output", "onetwo\x03\xe8", written);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("download size", 14L, result.Report.DownloadSize);
        diagnostics.Assert("delivered size", 8L, result.Report.DeliveredSize);
        diagnostics.Assert("upload size", 0L, result.Report.UploadSize);
        diagnostics.Assert("header size", Head101.Length, result.Report.HeaderSize);
        diagnostics.Assert("request size", Request.Length, result.Report.RequestSize);
        Assert.AreEqual("onetwo\x03\xe8", written);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted 101", Bytes(Head101));
        diagnostics.Bytes("scripted binary fragment (FIN 0, length 2)", Bytes("\x02\x02\x01\x02"));
        diagnostics.Bytes("scripted continuation and close", Bytes("\x80\x01\x03\x88\x00"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101), Bytes("\x02\x02\x01\x02"), Bytes("\x80\x01\x03\x88\x00"))).ExecuteAsync(Context("ws://h/", output));

        diagnostics.Act("result", Describe(result));
        diagnostics.Bytes("output", output.ToArray());
        diagnostics.Act("sizes", $"download {result.Report!.DownloadSize}, delivered {result.Report.DeliveredSize}");
        diagnostics.Diff("output", new byte[] { 1, 2, 3 }, output.ToArray());
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("download size", 9L, result.Report.DownloadSize);
        diagnostics.Assert("delivered size", 3L, result.Report.DeliveredSize);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted 101 and frames, then the drop", Bytes(Head101 + frames));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + frames))).ExecuteAsync(Context("ws://h/", output));

        string actualWritten = Encoding.Latin1.GetString(output.ToArray());
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("output", actualWritten);
        diagnostics.Act("sizes", $"download {result.Report!.DownloadSize}, delivered {result.Report.DeliveredSize}");
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Diff("output", written, actualWritten);
        diagnostics.Assert("download size", downloadSize, result.Report.DownloadSize);
        diagnostics.Assert("delivered size", deliveredSize, result.Report.DeliveredSize);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(written, actualWritten);
        Assert.AreEqual(downloadSize, result.Report!.DownloadSize);
        Assert.AreEqual(deliveredSize, result.Report.DeliveredSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_101ThenNoFrame_FailsWithEmptyReplyAndKeepsCode101()
    {
        // Measured: exit 52, "Empty reply from server", -w "0 101 0 102 192".
        var diagnostics = TestDiagnostics.For(TestContext);
        var headers = new MemoryStream();
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted 101 and nothing more", Bytes(Head101));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = output, HeaderOutput = headers });

        string headerText = Encoding.Latin1.GetString(headers.ToArray());
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("response code and download size", $"{result.Report!.ResponseCode} {result.Report.DownloadSize}");
        diagnostics.Bytes("header output", headers.ToArray());
        diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        diagnostics.Assert("error message", "Empty reply from server", result.ErrorMessage);
        diagnostics.Assert("response code", 101, result.Report.ResponseCode);
        diagnostics.Assert("download size", 0L, result.Report.DownloadSize);
        diagnostics.Diff("header output", Head101, headerText);
        diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual(0L, result.Report.DownloadSize);
        Assert.AreEqual(Head101, headerText);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_SendsItAsOneMaskedBinaryFrameAndCountsItAsUploadAndRequest()
    {
        // Measured with -T - and "abc": 82 83 <mask> <masked abc> after the request;
        // -w "4 101 9 102 201": size_request is the request plus the frame.
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"));
        var output = new MemoryStream();
        var progress = new RecordingProgress();
        var context = new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = output, Upload = new MemoryStream(Bytes("abc")), Progress = progress };
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Arrange("upload", "abc");
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x02ok"));

        TransferResult result;
        using (diagnostics.Phase("upgrade and frame exchange"))
        {
            result = await Handler(connection).ExecuteAsync(context);
        }

        string sent = Encoding.Latin1.GetString(connection.Sent);
        string expectedSent = Request + "\x82\x83\x00\x01\x02\x03\x61\x63\x61";
        diagnostics.Bytes("bytes sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("upload frame", "binary FIN 1 masked length 3");
        diagnostics.Act("sizes", $"download {result.Report!.DownloadSize}, upload {result.Report.UploadSize}, request {result.Report.RequestSize}");
        diagnostics.Act("progress", string.Join(" | ", progress.Reports));
        diagnostics.Diff("bytes sent", expectedSent, sent);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("download size", 4L, result.Report.DownloadSize);
        diagnostics.Assert("upload size", 9L, result.Report.UploadSize);
        diagnostics.Assert("request size", Request.Length + 9L, result.Report.RequestSize);
        Assert.AreEqual(expectedSent, sent);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"));
        var context = new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = new MemoryStream(), Upload = new FailingUploadStream(Bytes("a"), new IOException("gone")) };
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Arrange("upload", "'a' then IOException 'gone'");
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x02ok"));

        TransferResult result = await Handler(connection).ExecuteAsync(context);

        string sent = Encoding.Latin1.GetString(connection.Sent);
        string expectedSent = Request + "\x82\x81\x00\x01\x02\x03\x61";
        diagnostics.Bytes("bytes sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("upload size", result.Report!.UploadSize);
        diagnostics.Diff("bytes sent", expectedSent, sent);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("upload size", 7L, result.Report.UploadSize);
        Assert.AreEqual(expectedSent, sent);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(7L, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostData_IsNotSent()
    {
        // Measured: -d xyz sends nothing after the request, and the method stays GET.
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"));
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Arrange("post data", "xyz");
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x02ok"));

        TransferResult result = await Handler(connection).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = new MemoryStream(), PostData = Bytes("xyz") });

        string sent = Encoding.Latin1.GetString(connection.Sent);
        diagnostics.Bytes("bytes sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Diff("bytes sent", Request, sent);
        Assert.AreEqual(Request, sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_Ping_CountsThePongAsRequestBytes()
    {
        // Measured: ping "hi" then "ok" gives -w "8 101 0 102 200": the 8-byte pong is in size_request.
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + "\x89\x02hi\x81\x02ok"));
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Bytes("scripted 101, ping 'hi' and text frame", Bytes(Head101 + "\x89\x02hi\x81\x02ok"));

        TransferResult result = await Handler(connection).ExecuteAsync(Context("ws://127.0.0.1:47901/chat", output));

        string written = Encoding.Latin1.GetString(output.ToArray());
        diagnostics.Bytes("bytes sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("sizes", $"download {result.Report!.DownloadSize}, upload {result.Report.UploadSize}, request {result.Report.RequestSize}");
        diagnostics.Assert("output", "ok", written);
        diagnostics.Assert("download size", 8L, result.Report.DownloadSize);
        diagnostics.Assert("upload size", 0L, result.Report.UploadSize);
        diagnostics.Assert("request size", Request.Length + 8L, result.Report.RequestSize);
        Assert.AreEqual("ok", written);
        Assert.AreEqual(8L, result.Report!.DownloadSize);
        Assert.AreEqual(0L, result.Report.UploadSize);
        Assert.AreEqual(Request.Length + 8L, result.Report.RequestSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ViolationAfterAFrame_FailsWith56AndKeepsTheReport()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x02ok"));
        diagnostics.Bytes("scripted masked input frame", Bytes("\x81\x85\x01\x02\x03\x04"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"), Bytes("\x81\x85\x01\x02\x03\x04"))).ExecuteAsync(Context("ws://h/", output));

        string written = Encoding.Latin1.GetString(output.ToArray());
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("output", written);
        diagnostics.Act("response code and download size", $"{result.Report!.ResponseCode} {result.Report.DownloadSize}");
        diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        diagnostics.Assert("error message", "[WS] masked input frame", result.ErrorMessage);
        diagnostics.Assert("output", "ok", written);
        diagnostics.Assert("bytes transferred", 10L, result.BytesTransferred);
        diagnostics.Assert("response code", 101, result.Report.ResponseCode);
        diagnostics.Assert("download size", 10L, result.Report.DownloadSize);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("[WS] masked input frame", result.ErrorMessage);
        Assert.AreEqual("ok", written);
        Assert.AreEqual(10L, result.BytesTransferred);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual(10L, result.Report.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputWriteFails_FailsWithWriteErrorNamingWhatItTook()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new FailingStream(new OutputWriteFailedException(1, "short"));
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("output", "accepts 1 byte of 2 then fails ('short')");
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x02ok"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"))).ExecuteAsync(Context("ws://h/", output));

        diagnostics.Act("result", Describe(result));
        diagnostics.Act("response code", result.Report!.ResponseCode);
        diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
        diagnostics.Assert("error message", "Failure writing output to destination, passed 2 returned 1", result.ErrorMessage);
        diagnostics.Assert("response code", 101, result.Report.ResponseCode);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 2 returned 1", result.ErrorMessage);
        Assert.AreEqual(101, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTimeRunsOutWhileTheServerHoldsTheConnection_LetsTheCancellationEscapeAfterReportingTheBytes()
    {
        // Measured: -m 1 after "ok" ends with 28 "... with 4 bytes received", from the runner (ADR-0117).
        var diagnostics = TestDiagnostics.For(TestContext);
        using var cancellation = new CancellationTokenSource();
        var progress = new RecordingProgress();
        var output = new CancellingStream(cancellation);
        var context = new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = output, Progress = progress, CancellationToken = cancellation.Token };
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("output", "cancels the transfer after the first write");
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x02ok"));

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"))).ExecuteAsync(context));

        string written = Encoding.Latin1.GetString(output.ToArray());
        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Act("output", written);
        diagnostics.Act("progress", string.Join(" | ", progress.Reports));
        diagnostics.Assert("exception type", nameof(OperationCanceledException), exception.GetType().Name);
        diagnostics.Assert("output", "ok", written);
        diagnostics.Assert("progress", "started | down 4/?", string.Join(" | ", progress.Reports));
        Assert.AreEqual("ok", written);
        CollectionAssert.AreEqual(new[] { "started", "down 4/?" }, progress.Reports.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderAgentAndUser_SendsThemWithThePreemptiveAuthorization()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101));
        var authenticator = new RecordingAuthenticator("Basic dXNlcjpwdw==");
        var credential = new NetworkCredential("user", "pw");
        var options = new HttpRequestOptions { Headers = ["X-Test: 1"], UserAgent = "agent/1", BearerToken = "t", AuthSchemes = HttpAuthSchemes.Basic };
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/p?q=1");
        diagnostics.Arrange("options", "header 'X-Test: 1', user agent 'agent/1', bearer token 't', Basic only, user 'user'");
        diagnostics.Arrange("authenticator header", "Basic dXNlcjpwdw==");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(Connector(connection), authenticator).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/p?q=1"), Output = new MemoryStream(), Http = options, Credentials = credential });

        const string expectedSent =
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
            "\r\n";
        string sent = Encoding.Latin1.GetString(connection.Sent);
        HttpAuthRequest asked = authenticator.Requests.Single();
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("authorization asked for", $"{asked.Method} schemes {asked.AllowedSchemes}");
        diagnostics.Act("challenges", authenticator.Challenges.Single().Count);
        diagnostics.Diff("upgrade request", expectedSent, sent);
        diagnostics.Assert("challenge count", 0, authenticator.Challenges.Single().Count);
        Assert.AreEqual(expectedSent, sent);
        Assert.AreEqual(new HttpAuthRequest("GET", asked.Url, "/p?q=1", credential, "t", HttpAuthSchemes.Basic, false), asked with { Events = NoTransferEvents.Instance });
        Assert.AreEqual(0, authenticator.Challenges.Single().Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_Negotiate_SendsTheContextsTokenOnTheUpgradeRequest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101));
        var authenticator = new AsynchronousOnlyAuthenticator("Negotiate YIIB");
        var credential = new NetworkCredential(string.Empty, string.Empty);
        var options = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Negotiate };
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/");
        diagnostics.Arrange("options", "Negotiate only, empty credential");
        diagnostics.Arrange("authenticator header", "Negotiate YIIB (asynchronous only)");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await new WsProtocolHandler(Connector(connection), authenticator, new FixedRandomSource()).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/"), Output = new MemoryStream(), Http = options, Credentials = credential });

        const string expectedSent =
            "GET / HTTP/1.1\r\n" +
            "Host: 127.0.0.1:47901\r\n" +
            "Authorization: Negotiate YIIB\r\n" +
            "User-Agent: curl/8.21.0\r\n" +
            "Accept: */*\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            "Sec-WebSocket-Key: " + FixedRandomSource.Key + "\r\n" +
            "Connection: Upgrade\r\n" +
            "\r\n";
        string sent = Encoding.Latin1.GetString(connection.Sent);
        HttpAuthRequest asked = authenticator.Requests.Single();
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("allowed schemes asked", asked.AllowedSchemes);
        diagnostics.Diff("upgrade request", expectedSent, sent);
        diagnostics.Assert("allowed schemes", HttpAuthSchemes.Negotiate, asked.AllowedSchemes);
        diagnostics.Assert("credential is the context's", true, ReferenceEquals(credential, asked.Credential));
        Assert.AreEqual(expectedSent, sent);
        Assert.AreEqual(HttpAuthSchemes.Negotiate, asked.AllowedSchemes);
        Assert.AreSame(credential, asked.Credential);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomMethod_SendsItAndAsksAuthorizationForIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101));
        var authenticator = new RecordingAuthenticator();
        diagnostics.Arrange("url", "ws://h:81/");
        diagnostics.Arrange("custom method", "POST");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(Connector(connection), authenticator).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h:81/"), Output = new MemoryStream(), Http = new HttpRequestOptions { CustomMethod = "POST" } });

        string sent = Encoding.Latin1.GetString(connection.Sent);
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("methods", $"authorization asked for {authenticator.Requests.Single().Method}, report {result.Report!.Method}");
        diagnostics.Assert("authorization method", "POST", authenticator.Requests.Single().Method);
        diagnostics.Assert("report method", "POST", result.Report.Method);
        diagnostics.Assert("request starts with", "POST / HTTP/1.1\r\n", sent[..Math.Min(sent.Length, "POST / HTTP/1.1\r\n".Length)]);
        StringAssert.StartsWith(sent, "POST / HTTP/1.1\r\n");
        Assert.AreEqual("POST", authenticator.Requests.Single().Method);
        Assert.AreEqual("POST", result.Report!.Method);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_SendsHeadReadsNoFramesAndFailsWithEmptyReply()
    {
        // curl -sS -I -w '%{http_code} %{size_download} %{size_header} %{size_request}' against a
        // 101 with 81 05 "hello" 88 02 03 e8 (BL-583): HEAD, no output, 101 0 <head> <request>, exit 52.
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x05hello"), Bytes("\x88\x02\x03\xe8"));
        var output = new MemoryStream();
        var progress = new RecordingProgress();
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Arrange("no body", true);
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x05hello"));
        diagnostics.Bytes("scripted close frame", Bytes("\x88\x02\x03\xe8"));

        TransferResult result = await Handler(connection).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = output, NoBody = true, Progress = progress });

        string sent = Encoding.Latin1.GetString(connection.Sent);
        string expectedSent = string.Concat("HEAD", Request.AsSpan(3));
        int unread = await connection.ReadAsync(new byte[16], CancellationToken.None);
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("sizes", $"download {result.Report!.DownloadSize}, header {result.Report.HeaderSize}, request {result.Report.RequestSize}");
        diagnostics.Act("method and response code", $"{result.Report.Method} {result.Report.ResponseCode}");
        diagnostics.Act("unread bytes left in the connection", unread);
        diagnostics.Diff("upgrade request", expectedSent, sent);
        diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        diagnostics.Assert("error message", "Empty reply from server", result.ErrorMessage);
        diagnostics.Assert("output length", 0L, output.Length);
        diagnostics.Assert("response code", 101, result.Report.ResponseCode);
        diagnostics.Assert("method", "HEAD", result.Report.Method);
        diagnostics.Assert("download size", 0L, result.Report.DownloadSize);
        diagnostics.Assert("header size", Head101.Length, result.Report.HeaderSize);
        diagnostics.Assert("request size", Request.Length + 1, result.Report.RequestSize);
        diagnostics.Assert("last progress report", "done", progress.Reports[^1]);
        diagnostics.Assert("unread bytes", 4, unread);
        Assert.AreEqual(expectedSent, sent);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
        Assert.AreEqual(101, result.Report!.ResponseCode);
        Assert.AreEqual("HEAD", result.Report.Method);
        Assert.AreEqual(0L, result.Report.DownloadSize);
        Assert.AreEqual(Head101.Length, result.Report.HeaderSize);
        Assert.AreEqual(Request.Length + 1, result.Report.RequestSize);
        Assert.AreEqual("done", progress.Reports[^1]);
        Assert.AreEqual(4, unread);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyWithCustomMethod_KeepsTheCustomMethod()
    {
        // curl -I -X POST ws://... sends POST and still ends with 52 (BL-788 Notes).
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101));
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("no body and custom method", "true, POST");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(connection).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), NoBody = true, Http = new HttpRequestOptions { CustomMethod = "POST" } });

        string sent = Encoding.Latin1.GetString(connection.Sent);
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("report method", result.Report!.Method);
        diagnostics.Assert("report method", "POST", result.Report.Method);
        diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        StringAssert.StartsWith(sent, "POST / HTTP/1.1\r\n");
        Assert.AreEqual("POST", result.Report!.Method);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithAWrongAcceptValue_StillSucceeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string head = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: wrong\r\n\r\n\x81\x02ok";
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted 101 with a wrong Sec-WebSocket-Accept, then a text frame", Bytes(head));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(head))).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithoutUpgrade_StillSucceeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string reply = "HTTP/1.1 101 Switching Protocols\r\n\r\n\x81\x02ok";
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted 101 without Upgrade headers, then a text frame", Bytes(reply));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(reply))).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 200)]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n", 401)]
    [DataRow("http/1.1 101 Sw\r\n\r\n", 200)]
    public async Task ExecuteAsync_StatusOtherThan101_RefusesTheUpgradeWithExit22(string reply, int statusCode)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted reply", Bytes(reply));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(reply))).ExecuteAsync(Context("ws://h/", output));

        diagnostics.Act("result", Describe(result));
        diagnostics.Act("response code", result.Report!.ResponseCode);
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        diagnostics.Assert("error message", $"Refused WebSocket upgrade: {statusCode}", result.ErrorMessage);
        diagnostics.Assert("response code", statusCode, result.Report.ResponseCode);
        diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual($"Refused WebSocket upgrade: {statusCode}", result.ErrorMessage);
        Assert.AreEqual(statusCode, result.Report!.ResponseCode);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestChallengeThen101_SendsTheUpgradeOnceAndFailsWithExit22()
    {
        // curl --digest -u u:p ws://... against a Digest 401 then a 101 on the same connection
        // (ADR-0228): one request with no Authorization, exit 22, the 101 never read.
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] challenge = Bytes("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 0\r\n\r\n");
        var connection = new ScriptedConnection(challenge, Bytes(Head101 + "\x81\x02hi"));
        var authenticator = new RecordingAuthenticator();
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Arrange("options", "Digest only, user 'u'");
        diagnostics.Bytes("scripted Digest 401", challenge);
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x02hi"));

        TransferResult result = await Handler(Connector(connection), authenticator).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"),
            Output = output,
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest },
        });

        string sent = Encoding.Latin1.GetString(connection.Sent);
        int unread = await connection.ReadAsync(new byte[256], CancellationToken.None);
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("response code", result.Report!.ResponseCode);
        diagnostics.Act("challenges and continuations", $"{authenticator.Challenges.Single().Count} challenges, {authenticator.ContinuationCount} continuations");
        diagnostics.Act("unread bytes left in the connection", unread);
        diagnostics.Diff("upgrade request", Request, sent);
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        diagnostics.Assert("error message", "Refused WebSocket upgrade: 401", result.ErrorMessage);
        diagnostics.Assert("response code", 401, result.Report.ResponseCode);
        diagnostics.Assert("continuation count", 0, authenticator.ContinuationCount);
        diagnostics.Assert("unread bytes", Head101.Length + 4, unread);
        Assert.AreEqual(Request, sent);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 401", result.ErrorMessage);
        Assert.AreEqual(401, result.Report!.ResponseCode);
        Assert.AreEqual(0, output.Length);
        Assert.AreEqual(0, authenticator.Challenges.Single().Count);
        Assert.AreEqual(0, authenticator.ContinuationCount);
        Assert.AreEqual(Head101.Length + 4, unread);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmType2Challenge_AsksNoContinuationAndFailsWithExit22()
    {
        // curl --ntlm -u u:p ws://... sends the Type 1 message, and a 401 carrying the Type 2
        // challenge ends the transfer with exit 22 rather than a second leg (ADR-0228).
        const string Type1 = "NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==";
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] challenge = Bytes("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM TlRMTVNTUAACAAAAAAAAADgAAAAFgomiESIzRFVmd4gAAAAAAAAAAAAAAAAAAAAA\r\nContent-Length: 0\r\n\r\n");
        var connection = new ScriptedConnection(challenge, Bytes(Head101));
        var authenticator = new RecordingAuthenticator(Type1);
        diagnostics.Arrange("url", "ws://127.0.0.1:47901/chat");
        diagnostics.Arrange("options", "NTLM only, user 'u'");
        diagnostics.Arrange("authenticator header", Type1);
        diagnostics.Bytes("scripted NTLM Type 2 401", challenge);
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(Connector(connection), authenticator).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Ntlm },
        });

        string expectedSent = Request.Replace("Host: 127.0.0.1:47901\r\n", "Host: 127.0.0.1:47901\r\nAuthorization: " + Type1 + "\r\n", StringComparison.Ordinal);
        string sent = Encoding.Latin1.GetString(connection.Sent);
        diagnostics.Bytes("upgrade request sent", connection.Sent);
        diagnostics.Act("result", Describe(result));
        diagnostics.Act("authorization requests and continuations", $"{authenticator.Requests.Count} requests, {authenticator.ContinuationCount} continuations");
        diagnostics.Diff("upgrade request", expectedSent, sent);
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        diagnostics.Assert("error message", "Refused WebSocket upgrade: 401", result.ErrorMessage);
        diagnostics.Assert("authorization requests", 1, authenticator.Requests.Count);
        diagnostics.Assert("continuation count", 0, authenticator.ContinuationCount);
        Assert.AreEqual(expectedSent, sent);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 401", result.ErrorMessage);
        Assert.AreEqual(1, authenticator.Requests.Count);
        Assert.AreEqual(0, authenticator.ContinuationCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputOn101_WritesTheHeadByteForByte()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var headers = new MemoryStream();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted 101 and text frame", Bytes(Head101 + "\x81\x02ok"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), HeaderOutput = headers });

        string headerText = Encoding.Latin1.GetString(headers.ToArray());
        diagnostics.Act("result", Describe(result));
        diagnostics.Bytes("header output", headers.ToArray());
        diagnostics.Diff("header output", Head101, headerText);
        Assert.AreEqual(Head101, headerText);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputOnRefusal_WritesTheHeadButNotTheBody()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var headers = new MemoryStream();
        var output = new MemoryStream();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted 200 reply", Bytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = output, HeaderOutput = headers });

        string headerText = Encoding.Latin1.GetString(headers.ToArray());
        diagnostics.Act("result", Describe(result));
        diagnostics.Bytes("header output", headers.ToArray());
        diagnostics.Diff("header output", "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n", headerText);
        diagnostics.Assert("output length", 0L, output.Length);
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n", headerText);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputAcceptsPart_FailsWithWriteErrorNamingWhatItTook()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var headers = new FailingStream(new OutputWriteFailedException(5, "short"));
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("header output", "accepts 5 bytes then fails ('short')");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), HeaderOutput = headers });

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
        diagnostics.Assert("error message", $"Failure writing output to destination, passed {Head101.Length} returned 5", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"Failure writing output to destination, passed {Head101.Length} returned 5", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputFails_FailsWithWriteErrorReturningZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var headers = new FailingStream(new IOException("disk full"));
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("header output", "fails with IOException 'disk full'");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        TransferResult result = await Handler(new ScriptedConnection(Bytes(Head101))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), HeaderOutput = headers });

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
        diagnostics.Assert("error message", $"Failure writing output to destination, passed {Head101.Length} returned 0", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"Failure writing output to destination, passed {Head101.Length} returned 0", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_SendResetByThePeer_FailsWithTheWinsockWords()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var reset = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset);
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("write failure", "IOException 'reset' caused by SocketError.ConnectionReset");

        TransferResult result = await Handler(new FailingConnection(writeFailure: new IOException("reset", reset))).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        diagnostics.Assert("error message", "Send failure: Connection was reset", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_SendResetByThePeer_FailsWithTheSocketErrorsOwnMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var reset = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset);
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("write failure", "IOException 'reset' caused by SocketError.ConnectionReset");

        TransferResult result = await Handler(new FailingConnection(writeFailure: new IOException("reset", reset))).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message is the socket error's own message after 'Send failure: '", result.ErrorMessage == "Send failure: " + reset.Message);
        diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        diagnostics.Assert("error message is the socket error's own message", true, result.ErrorMessage == "Send failure: " + reset.Message);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: " + reset.Message, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_UpgradeSendAborted_FailsWithTheWinsockWords()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var aborted = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionAborted);
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("write failure", "IOException 'aborted' caused by SocketError.ConnectionAborted");

        TransferResult result = await Handler(new FailingConnection(writeFailure: new IOException("aborted", aborted))).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        diagnostics.Assert("error message", "Send failure: Connection was aborted", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: Connection was aborted", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_UpgradeSendAborted_FailsWithTheSocketErrorsOwnMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var aborted = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionAborted);
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("write failure", "IOException 'aborted' caused by SocketError.ConnectionAborted");

        TransferResult result = await Handler(new FailingConnection(writeFailure: new IOException("aborted", aborted))).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message is the socket error's own message after 'Send failure: '", result.ErrorMessage == "Send failure: " + aborted.Message);
        diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        diagnostics.Assert("error message is the socket error's own message", true, result.ErrorMessage == "Send failure: " + aborted.Message);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: " + aborted.Message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FlushFails_FailsWithSendFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("flush failure", "IOException 'broken'");

        TransferResult result = await Handler(new FailingConnection(flushFailure: new IOException("broken"))).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        diagnostics.Assert("error message", "Failed sending data to the peer", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoReply_FailsWithEmptyReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("scripted reply", "(none)");

        TransferResult result = await Handler(connection).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("result", Describe(result));
        diagnostics.Act("connection disposed", connection.IsDisposed);
        diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        diagnostics.Assert("error message", "Empty reply from server", result.ErrorMessage);
        diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2StatusLine_FailsWithExit1()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Bytes("scripted HTTP/2 status line", Bytes("HTTP/2 101 Sw\r\n\r\n"));

        TransferResult result = await Handler(new ScriptedConnection(Bytes("HTTP/2 101 Sw\r\n\r\n"))).ExecuteAsync(Context("ws://h/"));

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        diagnostics.Assert("error message", "Unsupported HTTP version (2.0) in response", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Unsupported HTTP version (2.0) in response", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        diagnostics.Arrange("url", "ws://h/");
        diagnostics.Arrange("cancellation token", "already cancelled");
        diagnostics.Bytes("scripted 101", Bytes(Head101));

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await Handler(new ScriptedConnection(Bytes(Head101))).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), CancellationToken = cancellation.Token }));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception type", nameof(OperationCanceledException), exception.GetType().Name);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static string Describe(TransferResult result) =>
        $"exit {(int)result.ExitCode} ({result.ExitCode}), error '{result.ErrorMessage}', bytes transferred {result.BytesTransferred}";

    private static RecordingConnector Connector(IConnection connection) => new(ConnectResult.Connected(connection));

    private static WsProtocolHandler Handler(IConnection connection) => Handler(Connector(connection));

    private static WsProtocolHandler Handler(IConnector connector, RecordingAuthenticator? authenticator = null) =>
        new(connector, authenticator ?? new RecordingAuthenticator(), new FixedRandomSource());

    private static TransferContext Context(string url, Stream? output = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output ?? new MemoryStream() };
}
