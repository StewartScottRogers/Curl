using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Rtsp.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Pins the libcurl RTSP requests (BL-2751, ADR-0691) against curl 8.22.0's <c>lib/rtsp.c</c> and
/// upstream cases 567 to 572: each kind's request line, headers and body, the <c>CSeq</c> and
/// session ID a handle carries from perform to perform, and the <c>SETUP</c> refusal.
/// </summary>
[TestClass]
public sealed class RtspProtocolHandlerLibcurlRequestTests
{
    private const string Uri = "rtsp://127.0.0.1:47950/media";

    private const string Ok = "RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(RtspRequestKind.Options, "x", "OPTIONS rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\n\r\n")]
    [DataRow(RtspRequestKind.Describe, null, "DESCRIBE rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\nAccept: application/sdp\r\n\r\n")]
    [DataRow(RtspRequestKind.Announce, "v=0\r\n", "ANNOUNCE rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\nContent-Length: 5\r\nContent-Type: application/sdp\r\n\r\nv=0\r\n")]
    [DataRow(RtspRequestKind.Setup, null, "SETUP rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\nTransport: RTP/AVP;unicast\r\n\r\n")]
    [DataRow(RtspRequestKind.Play, "x", "PLAY rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\n\r\n")]
    [DataRow(RtspRequestKind.Pause, null, "PAUSE rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\n\r\n")]
    [DataRow(RtspRequestKind.Teardown, null, "TEARDOWN rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\n\r\n")]
    [DataRow(RtspRequestKind.GetParameter, "scale\r\nspeed\r\n", "GET_PARAMETER rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\nContent-Length: 14\r\nContent-Type: text/parameters\r\n\r\nscale\r\nspeed\r\n")]
    [DataRow(RtspRequestKind.GetParameter, null, "GET_PARAMETER rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\n\r\n")]
    [DataRow(RtspRequestKind.SetParameter, "scale: 2\r\n", "SET_PARAMETER rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\nContent-Length: 10\r\nContent-Type: text/parameters\r\n\r\nscale: 2\r\n")]
    [DataRow(RtspRequestKind.Record, null, "RECORD rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nSession: s1\r\n\r\n")]
    public async Task ExecuteAsync_LibcurlRequestKind_SendsItAsRtspCDoes(RtspRequestKind kind, string? body, string expected)
    {
        var settings = new RtspRequestSettings
        {
            Kind = kind,
            StreamUri = Uri,
            Transport = "RTP/AVP;unicast",
            SessionId = "s1",
            Body = body is null ? null : Bytes(body),
        };
        Diagnostics.Arrange("settings", settings);
        var server = new ScriptedConnection(Bytes(Ok));

        TransferResult result = await Handler(server, settings).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.DiffText("request sent", expected, server.Sent);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(server.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(kind.ToString().ToUpperInvariant().Replace("PARAMETER", "_PARAMETER", StringComparison.Ordinal), result.Report?.Method);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyGetParameter_LeavesTheReplyBodyUnread()
    {
        var server = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 4\r\n\r\n"), Bytes("body"));
        var settings = new RtspRequestSettings { Kind = RtspRequestKind.GetParameter, SessionId = "s" };

        TransferResult result = await Handler(server, settings).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsTrue(server.HasUnreadBytes);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoPerformsSharingASessionState_ContinueTheCSeqAndSendTheRepliedSessionId()
    {
        var state = new RtspSessionState();
        var first = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: abc;timeout=60\r\n\r\n"));
        var second = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 2\r\nSession: abc\r\n\r\n"));

        TransferResult setup = await Handler(first, new RtspRequestSettings { Kind = RtspRequestKind.Setup, StreamUri = Uri, Transport = "T" }, state).ExecuteAsync(Context());
        TransferResult play = await Handler(second, new RtspRequestSettings { Kind = RtspRequestKind.Play, StreamUri = Uri }, state).ExecuteAsync(Context());

        Diagnostics.ActResult(play);
        Assert.AreEqual(CurlExitCode.Ok, setup.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, play.ExitCode);
        Assert.AreEqual("PLAY rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 2\r\nSession: abc\r\n\r\n", Encoding.Latin1.GetString(second.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_SetClientCSeqAndSessionId_SendsBoth()
    {
        // Upstream case 570's third request: CURLOPT_RTSP_CLIENT_CSEQ 1000 and session "asdf".
        var server = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1000\r\n\r\n"));
        var settings = new RtspRequestSettings { Kind = RtspRequestKind.Play, StreamUri = Uri, ClientSequenceNumber = 1000, SessionId = "asdf" };
        var state = new RtspSessionState();

        TransferResult result = await Handler(server, settings, state).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual("PLAY rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1000\r\nSession: asdf\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1001L, state.NextSequenceNumber);
    }

    [TestMethod]
    public async Task ExecuteAsync_FreshStateOnAReusedConnection_SendsCSeq0()
    {
        var server = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 0\r\n\r\n"));
        var connector = new RecordingConnector(ConnectResult.Connected(server, null, isReused: true));

        TransferResult result = await new RtspProtocolHandler(connector, new RecordingAuthenticator(), null, new RtspRequestSettings(), new RtspSessionState()).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual("OPTIONS * RTSP/1.0\r\nCSeq: 0\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedSend_DoesNotMoveTheCSeqOn()
    {
        var state = new RtspSessionState();
        var connector = new RecordingConnector(ConnectResult.Connected(new FailingConnection(writeFailure: new IOException("reset"))));

        TransferResult result = await new RtspProtocolHandler(connector, new RecordingAuthenticator(), null, new RtspRequestSettings(), state).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(1L, state.NextSequenceNumber);
    }

    [TestMethod]
    public async Task ExecuteAsync_SetupWithoutTransport_FailsWith43BeforeSending()
    {
        var server = new ScriptedConnection(Bytes(Ok));

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Setup, StreamUri = Uri }).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.AssertExitCode(CurlExitCode.BadFunctionArgument, result);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("Refusing to issue an RTSP SETUP without a Transport: header.", result.ErrorMessage);
        Assert.IsEmpty(server.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_SetupWithACustomTransportHeader_SendsItInsteadOfRefusing()
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { Headers = ["Transport: Planes/Trains"] });

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Setup, StreamUri = Uri, Transport = "unused" }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("SETUP rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nTransport: Planes/Trains\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_GetParameterWithoutASessionId_SendsItWithNoSessionHeader()
    {
        // curl 8.22.0's lib/rtsp.c has no session-ID refusal: it sends Session only when it holds one.
        var server = new ScriptedConnection(Bytes(Ok));

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.GetParameter, StreamUri = Uri }).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("GET_PARAMETER rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    [DataRow(RtspRequestKind.Play, "PLAY * RTSP/1.0\r\nCSeq: 1\r\nSession: s\r\nRange: npt=0-\r\nUser-Agent: agent\r\n\r\n")]
    [DataRow(RtspRequestKind.Options, "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nSession: s\r\nUser-Agent: agent\r\n\r\n")]
    public async Task ExecuteAsync_RangeGiven_SendsItOnlyOnPlayPauseAndRecord(RtspRequestKind kind, string expected)
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { UserAgent = "agent" }, "npt=0-");

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = kind, SessionId = "s" }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_AnnounceWithCustomContentType_SendsTheCustomOneBeforeContentLength()
    {
        // Upstream case 568's third request.
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { Headers = ["Content-Type: posty goodness"] });
        var settings = new RtspRequestSettings { Kind = RtspRequestKind.Announce, StreamUri = Uri, Body = Bytes("a=b") };

        TransferResult result = await Handler(server, settings).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual("ANNOUNCE rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nContent-Type: posty goodness\r\nContent-Length: 3\r\n\r\na=b", Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_DescribeWithCustomAccept_SendsOnlyTheCustomOne()
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { Headers = ["Accept: text/x"] });

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Describe }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual("DESCRIBE * RTSP/1.0\r\nCSeq: 1\r\nAccept: text/x\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_Receive_SendsNothingHandsTheFramesToTheSinkAndDropsTheConnection()
    {
        var sink = new RecordingInterleaveSink();
        var server = new ScriptedConnection(Bytes("$\0\0\u0002ab"));
        var connector = new RecordingConnector(ConnectResult.Connected(server));

        TransferResult result = await new RtspProtocolHandler(connector, new RecordingAuthenticator(), sink, new RtspRequestSettings { Kind = RtspRequestKind.Receive }, ChannelZero()).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(server.Sent);
        Assert.HasCount(1, sink.Frames);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedDescribe_SendsAcceptEncodingAfterAccept()
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { Compressed = true, UserAgent = "agent" });

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Describe, StreamUri = Uri }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(
            "DESCRIBE rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\nAccept-Encoding: deflate, gzip, br, zstd\r\nUser-Agent: agent\r\n\r\n",
            Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedDescribeWithCustomAcceptEncoding_SendsOnlyTheCustomOne()
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { Compressed = true, Headers = ["Accept-Encoding: identity"] });

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Describe, StreamUri = Uri }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(
            "DESCRIBE rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\nAccept-Encoding: identity\r\n\r\n",
            Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedSetup_SendsNoAcceptEncoding()
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { Compressed = true });

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Setup, StreamUri = Uri, Transport = "T" }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual("SETUP rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nTransport: T\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    [DataRow(RtspRequestKind.Setup, TimeConditionKind.IfModifiedSince, "SETUP rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nTransport: T\r\nUser-Agent: agent\r\nIf-Modified-Since: Thu, 02 Jan 2020 03:04:05 GMT\r\nX-A: 1\r\n\r\n")]
    [DataRow(RtspRequestKind.Describe, TimeConditionKind.IfUnmodifiedSince, "DESCRIBE rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\nUser-Agent: agent\r\nIf-Unmodified-Since: Thu, 02 Jan 2020 03:04:05 GMT\r\nX-A: 1\r\n\r\n")]
    [DataRow(RtspRequestKind.Options, TimeConditionKind.IfModifiedSince, "OPTIONS rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: agent\r\nX-A: 1\r\n\r\n")]
    [DataRow(RtspRequestKind.Play, TimeConditionKind.IfModifiedSince, "PLAY rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: agent\r\nX-A: 1\r\n\r\n")]
    public async Task ExecuteAsync_TimeCondition_IsSentAfterAuthorizationOnSetupAndDescribeOnly(RtspRequestKind kind, TimeConditionKind conditionKind, string expected)
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { UserAgent = "agent", Headers = ["X-A: 1"] }, timeCondition: new TimeCondition(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero), conditionKind));

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = kind, StreamUri = Uri, Transport = "T" }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.DiffText("request sent", expected, server.Sent);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_ToolOptionsWithTimeCondition_SendsNone()
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(timeCondition: new TimeCondition(DateTimeOffset.UnixEpoch, TimeConditionKind.IfModifiedSince));
        var handler = new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), new RecordingAuthenticator());

        TransferResult result = await handler.ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.22.0\r\n\r\n", Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_DescribeWithCustomIfModifiedSince_SendsOnlyTheCustomOne()
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(new HttpRequestOptions { Headers = ["if-modified-since: never"] }, timeCondition: new TimeCondition(DateTimeOffset.UnixEpoch, TimeConditionKind.IfModifiedSince));

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Describe, StreamUri = Uri }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(
            "DESCRIBE rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\nif-modified-since: never\r\n\r\n",
            Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    [DataRow(1200110860800L, "Sat, 01 Jan 40000 00:00:00 GMT")]
    [DataRow(1200116055845L, "Wed, 01 Mar 40000 03:04:05 GMT")]
    public async Task ExecuteAsync_SetupWithTimeConditionPastYear9999_SendsTheWholeYear(long unixSeconds, string date)
    {
        var server = new ScriptedConnection(Bytes(Ok));
        TransferContext context = Context(timeCondition: TimeCondition.FromUnixSeconds(unixSeconds, TimeConditionKind.IfModifiedSince));

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Setup, StreamUri = Uri, Transport = "T" }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(
            "SETUP rtsp://127.0.0.1:47950/media RTSP/1.0\r\nCSeq: 1\r\nTransport: T\r\nIf-Modified-Since: " + date + "\r\n\r\n",
            Encoding.Latin1.GetString(server.Sent));
    }

    [TestMethod]
    [DataRow(RtspRequestKind.Describe, "v=0\r\ns=x\r\n")]
    [DataRow(RtspRequestKind.GetParameter, "v=0\r\ns=x\r\n")]
    [DataRow(RtspRequestKind.Options, "")]
    [DataRow(RtspRequestKind.Setup, "")]
    public async Task ExecuteAsync_ReplyBody_IsWrittenToTheOutputForDescribeAndGetParameterOnly(RtspRequestKind kind, string expected)
    {
        // Upstream case 3100's 200 reply carries its SDP body to stdout (BL-2789).
        var server = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 10\r\n\r\nv=0\r\n"), Bytes("s=x\r\n"));
        TransferContext context = Context();
        var settings = new RtspRequestSettings { Kind = kind, StreamUri = Uri, Transport = "T", Body = Bytes("p") };

        TransferResult result = await Handler(server, settings).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(10, result.BytesTransferred);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(((MemoryStream)context.Output).ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_ToolOptionsReplyBody_IsNotWrittenToTheOutput()
    {
        var server = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 5\r\n\r\nv=0\r\n"));
        TransferContext context = Context();
        var handler = new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), new RecordingAuthenticator());

        TransferResult result = await handler.ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual(0, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_DescribeReplyBodyWriteFails_FailsWith23()
    {
        var server = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 5\r\n\r\nv=0\r\n"));
        TransferContext context = new() { Url = CurlUrl.Parse(Uri), Output = new FailingStream(new IOException("full")) };
        Diagnostics.ArrangeContext(context);

        TransferResult result = await Handler(server, new RtspRequestSettings { Kind = RtspRequestKind.Describe, StreamUri = Uri }).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 5 returned 0", result.ErrorMessage);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>A handle's session state after a <c>SETUP</c> reply's <c>Transport</c> set up interleaved channel 0.</summary>
    private static RtspSessionState ChannelZero()
    {
        var session = new RtspSessionState();
        session.AcceptTransport("RTP/AVP/TCP;interleaved=0");
        return session;
    }

    private RtspProtocolHandler Handler(IConnection connection, RtspRequestSettings settings, RtspSessionState? state = null) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator(), null, settings, state);

    private TransferContext Context(HttpRequestOptions? http = null, string? rangeText = null, TimeCondition? timeCondition = null)
    {
        TransferContext context = new() { Url = CurlUrl.Parse(Uri), Output = new MemoryStream(), Http = http, RangeText = rangeText, TimeCondition = timeCondition };
        Diagnostics.ArrangeContext(context);
        return context;
    }
}
