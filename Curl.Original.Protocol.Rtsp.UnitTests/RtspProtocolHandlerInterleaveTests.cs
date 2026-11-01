using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Rtsp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Pins how <see cref="RtspProtocolHandler" /> hands interleaved <c>$</c> RTP frames to its
/// interleave sink during a <c>RECEIVE</c> and before a reply, as curl 8.22.0's
/// <c>rtsp_filter_rtp</c> and <c>rtp_client_write</c> do (BL-2477, ADR-0612).
/// </summary>
[TestClass]
public sealed class RtspProtocolHandlerInterleaveTests
{
    private const string FrameA = "$\0\0\u0004abcd";

    private const string FrameB = "$\u0001\0\u0002xy";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReceiveAsync_TwoFramesAndNoSink_WritesBothToTheBodyOutputAndSendsNothing()
    {
        var server = new ScriptedConnection(Bytes(FrameA + FrameB));
        TransferContext context = Context();

        TransferResult result = await Handler(null).ReceiveAsync(server, context, ChannelsZeroAndOne());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(14L, result.BytesTransferred);
        CollectionAssert.AreEqual(Bytes(FrameA + FrameB), ((MemoryStream)context.Output).ToArray());
        Assert.IsEmpty(server.Sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_FrameSplitAcrossReads_HandsItOnceJoined()
    {
        var sink = new RecordingInterleaveSink();
        var server = new ScriptedConnection(Bytes("junk$\0"), Bytes("\0\u0004ab"), Bytes("cd" + FrameB + "$\0\0\u0009cut"));
        TransferContext context = Context();

        TransferResult result = await Handler(sink).ReceiveAsync(server, context, ChannelsZeroAndOne());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, sink.Frames);
        CollectionAssert.AreEqual(Bytes(FrameA), sink.Frames[0]);
        CollectionAssert.AreEqual(Bytes(FrameB), sink.Frames[1]);
        Assert.AreEqual(0L, context.Output.Length);
    }

    [TestMethod]
    public async Task ReceiveAsync_ReadEndsOnAWholeFrame_StopsWithoutReadingAgain()
    {
        var sink = new RecordingInterleaveSink();
        var server = new ScriptedConnection(Bytes(FrameA), Bytes(FrameB));

        TransferResult result = await Handler(sink).ReceiveAsync(server, Context(), ChannelsZeroAndOne());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(1, sink.Frames);
        CollectionAssert.AreEqual(Bytes(FrameA), sink.Frames[0]);
    }

    [TestMethod]
    public async Task ExchangeAsync_FramesAfterTheReplyBody_HandsThemAndCarriesACutFrameToTheNextReceive()
    {
        var sink = new RecordingInterleaveSink();
        var server = new ScriptedConnection(
            Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\nhi" + FrameA + "$\u0001\0"),
            Bytes("\u0002xy"));
        RtspProtocolHandler handler = Handler(sink);
        RtspSessionState session = ChannelsZeroAndOne();
        session.StartTransfer(null, connectionReused: false);

        TransferResult reply = await handler.ExchangeAsync(server, Context(), session);
        TransferResult receive = await handler.ReceiveAsync(server, Context(), session);

        Diagnostics.ActResult(receive);
        Assert.AreEqual(CurlExitCode.Ok, reply.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, receive.ExitCode);
        Assert.HasCount(2, sink.Frames);
        CollectionAssert.AreEqual(Bytes(FrameA), sink.Frames[0]);
        CollectionAssert.AreEqual(Bytes(FrameB), sink.Frames[1]);
        Assert.IsEmpty(session.CarriedBytes);
    }

    [TestMethod]
    public async Task ExchangeAsync_StartOfTheNextReplyAfterThisOne_IsReadByTheNextExchange()
    {
        var server = new ScriptedConnection(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\nRTSP/1.0 200 OK\r\nCSeq: 2\r\n\r\n"));
        RtspProtocolHandler handler = Handler(new RecordingInterleaveSink());
        RtspSessionState session = ChannelsZeroAndOne();
        session.StartTransfer(null, connectionReused: false);

        TransferResult first = await handler.ExchangeAsync(server, Context(), session);
        TransferResult second = await handler.ExchangeAsync(server, Context(), session);

        Diagnostics.ActResult(second);
        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
    }

    [TestMethod]
    public async Task ReceiveAsync_SinkTakesTooFewBytes_FailsWith23()
    {
        var events = new RecordingTransferEvents();
        var server = new ScriptedConnection(Bytes(FrameA));

        TransferResult result = await Handler(new RecordingInterleaveSink(shortBy: 1)).ReceiveAsync(server, Context(events), ChannelsZeroAndOne());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failed writing RTP data", result.ErrorMessage);
        Assert.Contains("Failed writing RTP data", events.Info);
    }

    [TestMethod]
    public async Task ReceiveAsync_ReadFails_FailsWith56()
    {
        var server = new FailingConnection(readFailure: new IOException("reset"));

        TransferResult result = await Handler(new RecordingInterleaveSink()).ReceiveAsync(server, Context(), ChannelsZeroAndOne());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_FramesBeforeTheReplyWithASink_HandsThemAndReadsTheReply()
    {
        var sink = new RecordingInterleaveSink();
        var headers = new MemoryStream();
        var server = new ScriptedConnection(Bytes(FrameA + "$\u0001"), Bytes("\0\u0002xyRTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n"));

        TransferResult result = await Handler(sink, server).ExecuteAsync(Context(headerOutput: headers));

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, sink.Frames);
        CollectionAssert.AreEqual(Bytes(FrameB), sink.Frames[1]);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n", Encoding.Latin1.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_FrameBeforeTheReplyAndSinkTakesTooFew_FailsWith23()
    {
        var server = new ScriptedConnection(Bytes(FrameA + "RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n"));

        TransferResult result = await Handler(new RecordingInterleaveSink(shortBy: 4), server).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failed writing RTP data", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnlyAPartialFrameBeforeTheClose_FailsWith52()
    {
        var server = new ScriptedConnection(Bytes("$\0\0\u0009abcdef"));

        TransferResult result = await Handler(new RecordingInterleaveSink(), server).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
    }

    [TestMethod]
    public async Task ExchangeAsync_FramesBetweenTwoReplies_HandsThemToTheSink()
    {
        var sink = new RecordingInterleaveSink();
        var server = new ScriptedConnection(
            Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;interleaved=0-1\r\n\r\n"),
            Bytes(FrameA + FrameB + "RTSP/1.0 200 OK\r\nCSeq: 2\r\n\r\n"));
        RtspProtocolHandler handler = Handler(sink);
        var session = new RtspSessionState(RtspProtocolHandler.FirstSequenceNumber);

        TransferResult first = await handler.ExchangeAsync(server, Context(), session);
        TransferResult second = await handler.ExchangeAsync(server, Context(), session);

        Diagnostics.ActResult(second);
        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.HasCount(2, sink.Frames);
        CollectionAssert.AreEqual(Bytes(FrameA), sink.Frames[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_FramesBeforeTheReplyWithNoSink_SkipsThemAsCurlDoes()
    {
        // Measured on curl 8.22.0 with Record-CurlExchange.ps1 (BL-2477): `curl -s -i` against this
        // reply wrote only the head to stdout and exited 0.
        var output = new MemoryStream();
        var server = new ScriptedConnection(Bytes(FrameA + FrameB + "RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\nhi$\0\0\u0003zzz"));
        TransferContext context = Context(headerOutput: output);

        TransferResult result = await Handler(null, server).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\n", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(0L, context.Output.Length);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static RtspProtocolHandler Handler(IRtspInterleaveSink? sink, IConnection? connection = null) =>
        new(new RecordingConnector(ConnectResult.Connected(connection ?? new ScriptedConnection())), new RecordingAuthenticator(), sink, null, ChannelsZeroAndOne());

    /// <summary>A new handle's session state after a reply's <c>Transport</c> set up interleaved channels 0 and 1.</summary>
    private static RtspSessionState ChannelsZeroAndOne()
    {
        var session = new RtspSessionState();
        session.AcceptTransport("RTP/AVP/TCP;interleaved=0-1");
        return session;
    }

    private TransferContext Context(RecordingTransferEvents? events = null, Stream? headerOutput = null)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            HeaderOutput = headerOutput,
            Events = events ?? (ITransferEvents)NoTransferEvents.Instance,
        };
        Diagnostics.ArrangeContext(context);
        return context;
    }
}
