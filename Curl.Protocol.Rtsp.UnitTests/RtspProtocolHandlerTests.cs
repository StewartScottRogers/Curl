using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Rtsp.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Pins the <c>rtsp://</c> transfer against curl 8.21.0, measured on 2026-09-28 against a
/// loopback listener (ADR-0169 rows 1-19, BL-591 Notes): the request bytes, what reaches the
/// output and the header output, and the exit code and message for each reply.
/// </summary>
[TestClass]
public sealed class RtspProtocolHandlerTests
{
    private const string Request = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n";

    private const string Ok = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nPublic: OPTIONS, DESCRIBE\r\n\r\n";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_IsRtspOnly()
    {
        string[] schemes = [.. Handler(new ScriptedConnection()).SupportedSchemes];

        Diagnostics.Act("supported schemes", string.Join(", ", schemes));
        Diagnostics.Assert("supported schemes", "rtsp", string.Join(", ", schemes));
        CollectionAssert.AreEqual(new[] { "rtsp" }, Handler(new ScriptedConnection()).SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Diagnostics.Arrange("connector, authenticator", "null, RecordingAuthenticator");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new RtspProtocolHandler(null!, new RecordingAuthenticator()));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name} for {thrown.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public void Constructor_NullAuthenticator_Throws()
    {
        Diagnostics.Arrange("connector, authenticator", "RecordingConnector, null");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())), null!));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name} for {thrown.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        Diagnostics.Arrange("context", "null");

        ArgumentNullException thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new ScriptedConnection()).ExecuteAsync(null!));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name} for {thrown.ParamName}");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutPort_ConnectsToPort554WithoutTlsPassingProxyAndEvents()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(Server(Ok)));
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("url", "rtsp://h/media");
        Diagnostics.Arrange("proxy", proxy);

        TransferResult result = await new RtspProtocolHandler(connector, new RecordingAuthenticator()).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("rtsp://h/media"), Output = new MemoryStream(), Proxy = proxy, Events = events });

        Diagnostics.ActResult(result);
        ConnectTarget target = connector.Targets.Single();
        Diagnostics.Assert("connect target", new ConnectTarget("h", 554, false) { Proxy = proxy, Events = events, PoolScheme = "rtsp" }, target);
        Assert.AreEqual(new ConnectTarget("h", 554, false) { Proxy = proxy, Events = events, PoolScheme = "rtsp" }, target);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithPort_ConnectsToThatPort()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(Server(Ok)));

        TransferResult result = await new RtspProtocolHandler(connector, new RecordingAuthenticator()).ExecuteAsync(Context("rtsp://h:47950/media"));

        Diagnostics.ActResult(result);
        Diagnostics.Assert("connect port", 47950, connector.Targets.Single().Port);
        Assert.AreEqual(47950, connector.Targets.Single().Port);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheFailureUnchanged()
    {
        var connector = new RecordingConnector(ConnectResult.Refused("Failed to connect"));

        TransferResult result = await new RtspProtocolHandler(connector, new RecordingAuthenticator()).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.CouldntConnect, result);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_OkReply_SendsOptionsStarAndSucceedsWritingNothing()
    {
        ScriptedConnection server = Server(Ok);
        var output = new MemoryStream();

        TransferResult result = await Handler(server).ExecuteAsync(Context(output: output));

        Assert.AreEqual(Request, Encoding.Latin1.GetString(server.Sent));
        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, output.Length);
        Assert.IsTrue(server.IsDisposed);
    }

    [TestMethod]
    [DataRow(null, "1-2", null, "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nRange: 1-2\r\nUser-Agent: curl/8.21.0\r\n\r\n")]
    [DataRow(5L, null, null, "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nRange: 5-\r\nUser-Agent: curl/8.21.0\r\n\r\n")]
    [DataRow(null, "1-2", "Range: npt=0-", "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nRange: npt=0-\r\n\r\n")]
    [DataRow(0L, null, null, "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n")]
    [DataRow(5L, "1-2", null, "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nRange: 5-\r\nUser-Agent: curl/8.21.0\r\n\r\n")]
    public async Task ExecuteAsync_ResumeAndRange_SendsCurlsRangeLine(long? resumeFrom, string? rangeText, string? header, string expected)
    {
        ScriptedConnection server = Server(Ok);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Headers = header is null ? [] : [header] },
            ResumeFrom = resumeFrom,
            RangeText = rangeText,
        };
        Diagnostics.ArrangeContext(context);

        TransferResult result = await Handler(server).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(server.Sent);
        Diagnostics.DiffText("request sent", expected, server.Sent);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeAndReferer_SendsRangeBeforeReferer()
    {
        ScriptedConnection server = Server(Ok);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Referer = "http://r/" },
            RangeText = "1-2",
        };
        Diagnostics.ArrangeContext(context);

        TransferResult result = await Handler(server).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(server.Sent);
        Diagnostics.DiffText("request sent", "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nRange: 1-2\r\nReferer: http://r/\r\nUser-Agent: curl/8.21.0\r\n\r\n", server.Sent);
        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nRange: 1-2\r\nReferer: http://r/\r\nUser-Agent: curl/8.21.0\r\n\r\n",
            Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomMethodTargetAndBody_StillSendsOptionsStar()
    {
        ScriptedConnection server = Server(Ok);
        var http = new HttpRequestOptions { CustomMethod = "DESCRIBE", RequestTarget = "/x", Compressed = true };

        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            Http = http,
            PostData = "abc"u8.ToArray(),
        };
        Diagnostics.ArrangeContext(context);

        TransferResult result = await Handler(server).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(server.Sent);
        Diagnostics.DiffText("request sent", Request, server.Sent);
        Assert.AreEqual(Request, Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyWithBody_ReadsTheBodyAndWritesNeitherHeadNorBody()
    {
        const string reply = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Type: application/sdp\r\nContent-Length: 5\r\n\r\nv=0\r\n";
        var output = new MemoryStream();

        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context(output: output));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5L, result.BytesTransferred);
        Assert.AreEqual(5L, result.Report!.DownloadSize);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_IncludeWithBody_WritesTheHeadOnly()
    {
        const string reply = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\nok";
        var output = new MemoryStream();

        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context(output: output, headerOutput: output));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\n", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyArrivingInLaterReads_ReadsItAll()
    {
        byte[][] reads = [Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 6\r\n\r\nab"), Bytes("cd"), Bytes("efEXTRA")];

        Diagnostics.ArrangeReads(reads);

        TransferResult result = await Handler(new ScriptedConnection(reads)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.Assert("bytes transferred", 6L, result.BytesTransferred);
        Assert.AreEqual(6L, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyShorterThanContentLength_Succeeds()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 10\r\n\r\nab")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2L, result.BytesTransferred);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 10\r\n\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AgentHeaderAndCredentials_SendsThemInCurlsOrder()
    {
        ScriptedConnection server = Server(Ok);
        var authenticator = new RecordingAuthenticator("Basic dTpw");
        var http = new HttpRequestOptions { UserAgent = "agent/1", Headers = ["X-Test: 1"], BearerToken = "t", AuthSchemes = HttpAuthSchemes.Bearer };
        var credentials = new NetworkCredential("u", "p");

        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            Http = http,
            Credentials = credentials,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("authenticator answers", "Basic dTpw");

        TransferResult result = await new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), authenticator).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(server.Sent);
        Diagnostics.Act("authenticator requests", authenticator.Requests.Count);
        Diagnostics.DiffText("request sent", "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: agent/1\r\nAuthorization: Basic dTpw\r\nX-Test: 1\r\n\r\n", server.Sent);
        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: agent/1\r\nAuthorization: Basic dTpw\r\nX-Test: 1\r\n\r\n",
            Encoding.Latin1.GetString(server.Sent));
        Assert.AreEqual(
            new HttpAuthRequest("OPTIONS", context.Url, "*", credentials, "t", HttpAuthSchemes.Bearer, IsProxy: false),
            authenticator.Requests.Single());
        Assert.AreEqual(0, authenticator.Challenges.Single().Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadRequested_WritesTheReplyHeadByteForByte()
    {
        var headers = new MemoryStream();

        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            HeaderOutput = headers,
            NoBody = true,
        };

        TransferResult result = await Handler(Server(Ok)).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(Ok, Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_LineFeedOnlyHead_WritesItAsReceived()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\nCSeq: 1\n\n")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\nCSeq: 1\n\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_CSeqInAnotherCaseWithBlanks_Matches()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\ncseq:   1 \r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_CSeqWithTrailingText_ReadsTheNumber()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1x\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_WrongCSeq_FailsWith85()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 7\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("The CSeq of this request 1 did not match the response 7", result.ErrorMessage);
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCSeq_FailsWith85AgainstZero()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("The CSeq of this request 1 did not match the response 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoCSeqHeaders_ChecksTheLast()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nCSeq: 2\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.Diff("error", "The CSeq of this request 1 did not match the response 2", result.ErrorMessage ?? string.Empty);
        Assert.AreEqual("The CSeq of this request 1 did not match the response 2", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotFoundWithoutCSeq_WritesTheHeadAndFailsWith85()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 404 Not Found\r\nContent-Length: 3\r\n\r\nabc")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("The CSeq of this request 1 did not match the response 0", result.ErrorMessage);
        Assert.AreEqual("RTSP/1.0 404 Not Found\r\nContent-Length: 3\r\n\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_SessionInReply_IsAccepted()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: 1234;timeout=60\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow("Session: 1234;timeout=60\r\nSession: 1234\r\n")]
    [DataRow("Session:\r\nSession:  \r\n")]
    [DataRow("session: \tab c\r\nSESSION: ab\r\n")]
    [DataRow("Session:\t 7 ;x\r\nSession:7\r\n")]
    [DataRow("Session: aéb\r\nSession: a\r\n")]
    public async Task ExecuteAsync_SessionHeadersNamingTheSameId_Succeed(string lines)
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n" + lines + "\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondSessionHeaderNamingAnotherId_WritesTheHeadUpToItAndFailsWith86()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: 1234;timeout=60\r\nSession: 9999\r\n\r\n")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspSessionError, result);
        Assert.AreEqual(CurlExitCode.RtspSessionError, result.ExitCode);
        Assert.AreEqual("Got RTSP Session ID Line [9999\r\n], but wanted ID [1234]", result.ErrorMessage);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: 1234;timeout=60\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: ;x\r\nSession: 5\r\n\r\n", "Got RTSP Session ID Line [5\r\n], but wanted ID []")]
    [DataRow("RTSP/1.0 200 OK\r\nSession: 1\r\nSession: 2\r\nCSeq: 1\r\n\r\n", "Got RTSP Session ID Line [2\r\n], but wanted ID [1]")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: 12\r\nSession: 123\r\n\r\n", "Got RTSP Session ID Line [123\r\n], but wanted ID [12]")]
    [DataRow("RTSP/1.0 200 OK\nCSeq: 1\nSession: 1\nSession: 2\n\n", "Got RTSP Session ID Line [2\n], but wanted ID [1]")]
    [DataRow("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: a\u007fb\r\nSession: a\r\n\r\n", "Got RTSP Session ID Line [a\r\n], but wanted ID [a\u007fb]")]
    public async Task ExecuteAsync_SessionMismatchInOneReply_FailsWith86(string reply, string message)
    {
        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspSessionError, result);
        Assert.AreEqual(CurlExitCode.RtspSessionError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SessionMismatchInANotFoundUnderFail_FailsWith86NotWith22()
    {
        const string reply = "RTSP/1.0 404 Not Found\r\nCSeq: 1\r\nSession: 1\r\nSession: 2\r\n\r\n";

        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context(http: new HttpRequestOptions { Fail = HttpFailMode.Fail }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspSessionError, result);
        Assert.AreEqual(CurlExitCode.RtspSessionError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExchangeAsync_LaterRequest_SendsTheNextCSeqAndTheKeptSession()
    {
        var server = new ScriptedConnection(
            Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: 1234;timeout=60\r\n\r\n"),
            Bytes("RTSP/1.0 200 OK\r\nCSeq: 2\r\nSession: 1234\r\n\r\n"));
        var session = new RtspSessionState(RtspProtocolHandler.FirstSequenceNumber);
        RtspProtocolHandler handler = Handler(server);

        TransferResult first = await handler.ExchangeAsync(server, Context(), session);
        TransferResult second = await handler.ExchangeAsync(server, Context(), session);

        Diagnostics.ActResult(first);
        Diagnostics.ActResult(second);
        Diagnostics.ActSent(server.Sent);
        Diagnostics.DiffText("requests sent", Request + "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nSession: 1234\r\nUser-Agent: curl/8.21.0\r\n\r\n", server.Sent);
        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual(
            Request + "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nSession: 1234\r\nUser-Agent: curl/8.21.0\r\n\r\n",
            Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExchangeAsync_LaterReplyNamingAnotherSession_FailsWith86()
    {
        var server = new ScriptedConnection(
            Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: 1234;timeout=60\r\n\r\n"),
            Bytes("RTSP/1.0 200 OK\r\nCSeq: 2\r\nSession: 9999\r\n\r\n"));
        var session = new RtspSessionState(RtspProtocolHandler.FirstSequenceNumber);
        RtspProtocolHandler handler = Handler(server);

        await handler.ExchangeAsync(server, Context(), session);
        TransferResult second = await handler.ExchangeAsync(server, Context(), session);

        Diagnostics.ActResult(second);
        Diagnostics.AssertExitCode(CurlExitCode.RtspSessionError, second);
        Assert.AreEqual(CurlExitCode.RtspSessionError, second.ExitCode);
        Assert.AreEqual("Got RTSP Session ID Line [9999\r\n], but wanted ID [1234]", second.ErrorMessage);
    }

    [TestMethod]
    public async Task ExchangeAsync_LaterReplyWithTheFirstCSeq_FailsWith85NamingTheSecond()
    {
        var server = new ScriptedConnection(Bytes(Ok), Bytes(Ok));
        var session = new RtspSessionState(RtspProtocolHandler.FirstSequenceNumber);
        RtspProtocolHandler handler = Handler(server);

        await handler.ExchangeAsync(server, Context(), session);
        TransferResult second = await handler.ExchangeAsync(server, Context(), session);

        Diagnostics.ActResult(second);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, second);
        Assert.AreEqual(CurlExitCode.RtspCseqError, second.ExitCode);
        Assert.AreEqual("The CSeq of this request 2 did not match the response 1", second.ErrorMessage);
    }

    [TestMethod]
    public async Task ExchangeAsync_EmptyKeptSession_IsSentEmpty()
    {
        var server = new ScriptedConnection(
            Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession:\r\n\r\n"),
            Bytes("RTSP/1.0 200 OK\r\nCSeq: 2\r\n\r\n"));
        var session = new RtspSessionState(RtspProtocolHandler.FirstSequenceNumber);
        RtspProtocolHandler handler = Handler(server);

        await handler.ExchangeAsync(server, Context(), session);
        await handler.ExchangeAsync(server, Context(), session);

        Diagnostics.ActSent(server.Sent);
        string sent = Encoding.Latin1.GetString(server.Sent);
        const string expectedEnd = "CSeq: 2\r\nSession: \r\nUser-Agent: curl/8.21.0\r\n\r\n";
        Diagnostics.Diff("requests sent end", expectedEnd, sent[^Math.Min(sent.Length, expectedEnd.Length)..]);
        StringAssert.EndsWith(Encoding.Latin1.GetString(server.Sent), "CSeq: 2\r\nSession: \r\nUser-Agent: curl/8.21.0\r\n\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_NotFound_SucceedsWithoutFail()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(404, result.Report!.ResponseCode);
    }

    [TestMethod]
    [DataRow(HttpFailMode.Fail)]
    [DataRow(HttpFailMode.FailWithBody)]
    public async Task ExecuteAsync_NotFoundUnderFail_WritesTheHeadAndFailsWith22(HttpFailMode fail)
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\nContent-Length: 3\r\n\r\nabc"))
            .ExecuteAsync(Context(http: new HttpRequestOptions { Fail = fail }, headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.HttpReturnedError, result);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 404", result.ErrorMessage);
        Assert.AreEqual(404, result.Report!.ResponseCode);
        Assert.AreEqual("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\nContent-Length: 3\r\n\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_NotFoundWithoutCSeqUnderFail_FailsWith22NotWith85()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 404 Not Found\r\n\r\n"))
            .ExecuteAsync(Context(http: new HttpRequestOptions { Fail = HttpFailMode.Fail }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.HttpReturnedError, result);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BelowFourHundredUnderFail_Succeeds()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 399 X\r\nCSeq: 1\r\n\r\n"))
            .ExecuteAsync(Context(http: new HttpRequestOptions { Fail = HttpFailMode.Fail }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_StatusBelow100_WritesTheHeadAndFailsWith1()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 099 X\r\nCSeq: 1\r\n\r\n")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.UnsupportedProtocol, result);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Unsupported response code in HTTP response", result.ErrorMessage);
        Assert.AreEqual(99, result.Report!.ResponseCode);
        Assert.AreEqual("RTSP/1.0 099 X\r\nCSeq: 1\r\n\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpReply_FailsWith52WritingNothing()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.GotNothing, result);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        Assert.AreEqual(0L, headers.Length);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("RTSPX")]
    [DataRow("rtsp/1.0 200 OK\r\nCSeq: 1\r\n\r\n")]
    [DataRow("RTSP")]
    public async Task ExecuteAsync_EmptyOrNotRtspReply_FailsWith52(string reply)
    {
        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.GotNothing, result);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("RTSP/1.0 abc\r\nCSeq: 1\r\n\r\n")]
    [DataRow("RTSP/2.0 200 OK\r\nCSeq: 1\r\n\r\n")]
    [DataRow("RTSP/1.0\r\nCSeq: 1\r\n\r\n")]
    [DataRow("RTSP/1.0 1000 X\r\nCSeq: 1\r\n\r\n")]
    [DataRow("RTSP/1.0  200 X\r\nCSeq: 1\r\n\r\n")]
    public async Task ExecuteAsync_WeirdStatusLine_FailsWith8WritingNothing(string reply)
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Assert.AreEqual(0L, headers.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_StatusWithoutReason_IsAccepted()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200\r\nCSeq: 1\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidContentLength_WritesTheLinesBeforeItAndFailsWith8()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: x\r\n\r\n")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.WeirdServerReply, result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", result.ErrorMessage);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    [DataRow("CSeq: abc\r\n")]
    [DataRow("CSeq:\r\n")]
    [DataRow("CSeq: -\n")]
    [DataRow("CSeq: 99999999999999999999\r\n")]
    public async Task ExecuteAsync_UnreadableCSeq_FailsWith85QuotingTheLine(string line)
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\n" + line + "\r\n")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("Unable to read the CSeq header: [" + line + "]", result.ErrorMessage);
        Assert.AreEqual("RTSP/1.0 200 OK\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    [DataRow("CSeq: +1\r\n")]
    [DataRow("CSeq:1\r\n")]
    public async Task ExecuteAsync_SignedOrUnspacedCSeq_Matches(string line)
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\n" + line + "\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegativeCSeq_FailsWith85NamingIt()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: -3\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.Diff("error", "The CSeq of this request 1 did not match the response -3", result.ErrorMessage ?? string.Empty);
        Assert.AreEqual("The CSeq of this request 1 did not match the response -3", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadCutAfterTheCSeqLine_WritesItAndFailsWith85AgainstZero()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("The CSeq of this request 1 did not match the response 0", result.ErrorMessage);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadCutInsideTheLineAfterCSeq_WritesItAndSucceeds()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\nPubl")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\nPubl", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadCutInsideTheStatusLine_WritesItAndFailsWith85()
    {
        var headers = new MemoryStream();

        TransferResult result = await Handler(Server("RTSP/1.0 200 OK")).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK", Encoding.Latin1.GetString(headers.ToArray()));
        Assert.AreEqual(0, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotFoundCutBeforeTheBlankLineUnderFail_ChecksOnlyTheCSeq()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\nX"))
            .ExecuteAsync(Context(http: new HttpRequestOptions { Fail = HttpFailMode.Fail }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow("CSeq: 5")]
    [DataRow("CSeq:")]
    [DataRow("cseq;")]
    public async Task ExecuteAsync_CustomCSeqHeader_FailsWith85AfterConnectingSendingNothing(string header)
    {
        ScriptedConnection server = Server(Ok);

        TransferResult result = await Handler(server).ExecuteAsync(Context(http: new HttpRequestOptions { Headers = [header] }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
        Assert.AreEqual("CSeq cannot be set as a custom header.", result.ErrorMessage);
        Assert.AreEqual(0, server.Sent.Length);
    }

    [TestMethod]
    [DataRow("Session: 5")]
    [DataRow("Session:")]
    [DataRow("session;")]
    public async Task ExecuteAsync_CustomSessionHeader_FailsWith43AfterConnectingSendingNothing(string header)
    {
        ScriptedConnection server = Server(Ok);

        TransferResult result = await Handler(server).ExecuteAsync(Context(http: new HttpRequestOptions { Headers = [header] }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.BadFunctionArgument, result);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("Session ID cannot be set as a custom header.", result.ErrorMessage);
        Assert.AreEqual(0, server.Sent.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomSessionAndCSeqHeaders_FailsWith85()
    {
        TransferResult result = await Handler(Server(Ok)).ExecuteAsync(Context(http: new HttpRequestOptions { Headers = ["Session: 5", "CSeq: 3"] }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RtspCseqError, result);
        Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomHeaderEndingInSession_IsSent()
    {
        ScriptedConnection server = Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");

        TransferResult result = await Handler(server).ExecuteAsync(Context(http: new HttpRequestOptions { Headers = ["X-Session: 1"] }));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nX-Session: 1\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_Report_CarriesStatusMethodAndSizes()
    {
        TransferResult result = await Handler(Server("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n")).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.Assert(
            "status, method, header size, request size",
            $"200, OPTIONS, 28, {Request.Length}",
            $"{result.Report?.ResponseCode}, {result.Report?.Method}, {result.Report?.HeaderSize}, {result.Report?.RequestSize}");
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual("OPTIONS", result.Report.Method);
        Assert.AreEqual(28L, result.Report.HeaderSize);
        Assert.AreEqual((long)Request.Length, result.Report.RequestSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadSplitOverReads_IsReadWhole()
    {
        byte[][] reads = [.. Ok.Select(character => new[] { (byte)character })];
        var headers = new MemoryStream();

        TransferResult result = await Handler(new ScriptedConnection(reads)).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(Ok, Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadLongerThanTheBuffer_IsReadWhole()
    {
        string reply = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nX: " + new string('a', 40000) + "\r\n\r\n";

        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((long)reply.Length, result.Report!.HeaderSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadLongerThanTheLimit_FailsWith100()
    {
        string reply = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nX: " + new string('a', RtspReplyReader.MaximumHeadLength) + "\r\n\r\n";

        TransferResult result = await Handler(Server(reply)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.TooLarge, result);
        Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode);
        Assert.AreEqual("A value or data field grew larger than allowed", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_SendReset_FailsWith55AndTheWinsockWords()
    {
        var connection = new FailingConnection(writeFailure: Reset());

        TransferResult result = await Handler(connection).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.SendError, result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_SendReset_FailsWith55AndTheSocketErrorsOwnMessage()
    {
        IOException reset = Reset();

        TransferResult result = await Handler(new FailingConnection(writeFailure: reset)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.SendError, result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: " + reset.InnerException!.Message, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_SendAborted_FailsWith55AndTheWinsockWords()
    {
        TransferResult result = await Handler(new FailingConnection(writeFailure: Aborted())).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.SendError, result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: Connection was aborted", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_SendAborted_FailsWith55AndTheSocketErrorsOwnMessage()
    {
        IOException aborted = Aborted();

        TransferResult result = await Handler(new FailingConnection(writeFailure: aborted)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.SendError, result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: " + aborted.InnerException!.Message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FlushFails_FailsWith55()
    {
        TransferResult result = await Handler(new FailingConnection(flushFailure: new IOException())).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.SendError, result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ReceiveReset_FailsWith56AndTheWinsockWords()
    {
        TransferResult result = await Handler(new FailingConnection(readFailure: Reset())).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Recv failure: Connection was reset", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ReceiveReset_FailsWith56AndTheSocketErrorsOwnMessage()
    {
        IOException reset = Reset();

        TransferResult result = await Handler(new FailingConnection(readFailure: reset)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Recv failure: " + reset.InnerException!.Message, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ReceiveAborted_FailsWith56AndTheWinsockWords()
    {
        TransferResult result = await Handler(new FailingConnection(readFailure: Aborted())).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Recv failure: Connection was aborted", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ReceiveAborted_FailsWith56AndTheSocketErrorsOwnMessage()
    {
        IOException aborted = Aborted();

        TransferResult result = await Handler(new FailingConnection(readFailure: aborted)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Recv failure: " + aborted.InnerException!.Message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReceiveFails_FailsWith56()
    {
        TransferResult result = await Handler(new FailingConnection(readFailure: new IOException())).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyReceiveFails_FailsWith56()
    {
        var connection = new HeadThenFailingConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 5\r\n\r\n"));

        TransferResult result = await Handler(connection).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.RecvError, result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderWriteFails_FailsWith23()
    {
        TransferResult result = await Handler(Server(Ok))
            .ExecuteAsync(Context(headerOutput: new FailingStream(new OutputWriteFailedException(3, "m"))));

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.WriteError, result);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 17 returned 3", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderWriteFailsPlainly_ReportsNothingAccepted()
    {
        Diagnostics.Arrange("header output", "FailingStream throwing a plain IOException");

        TransferResult result = await Handler(Server(Ok)).ExecuteAsync(Context(headerOutput: new FailingStream(new IOException())));

        Diagnostics.ActResult(result);
        Diagnostics.Diff("error", "Failure writing output to destination, passed 17 returned 0", result.ErrorMessage ?? string.Empty);
        Assert.AreEqual("Failure writing output to destination, passed 17 returned 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            CancellationToken = cancellation.Token,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("cancellation", "cancelled before the transfer starts");

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await Handler(Server(Ok)).ExecuteAsync(context));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("exception type", nameof(OperationCanceledException), thrown.GetType().Name);
    }

    private RtspProtocolHandler Handler(IConnection connection)
    {
        Diagnostics.Arrange("connection", connection.GetType().Name);
        return new(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator());
    }

    private ScriptedConnection Server(string reply)
    {
        Diagnostics.ArrangeReply(reply);
        return reply.Length == 0 ? new ScriptedConnection() : new ScriptedConnection(Bytes(reply));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static IOException Reset() => new("reset", new SocketException((int)SocketError.ConnectionReset));

    private static IOException Aborted() => new("aborted", new SocketException((int)SocketError.ConnectionAborted));

    private TransferContext Context(
        string url = "rtsp://127.0.0.1:47950/media",
        Stream? output = null,
        Stream? headerOutput = null,
        HttpRequestOptions? http = null)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = output ?? new MemoryStream(),
            HeaderOutput = headerOutput,
            Http = http,
        };
        Diagnostics.ArrangeContext(context);
        return context;
    }

    /// <summary>A connection that returns one head, then fails every later read.</summary>
    private sealed class HeadThenFailingConnection(byte[] head) : IConnection
    {
        private bool headSent;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (headSent)
            {
                return ValueTask.FromException<int>(new IOException());
            }

            headSent = true;
            head.CopyTo(buffer);
            return ValueTask.FromResult(head.Length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
