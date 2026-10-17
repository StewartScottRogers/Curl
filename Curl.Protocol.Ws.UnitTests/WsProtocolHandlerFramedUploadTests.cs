using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins a read callback's upload (<see cref="IWsUploadFrameSource" />) against curl 8.22.0's <c>cr_ws_read</c> in
/// <c>lib/ws.c</c> (BL-2611, ADR-0669): each read becomes one binary frame of its length, unless
/// <c>curl_ws_start_frame</c> started a frame, whose payload the following reads fill.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerFramedUploadTests
{
    private const string Head101 = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n";

    private const int UploadReadSize = 64 * 1024;

    [TestMethod]
    public async Task ExecuteAsync_ReadCallbackUpload_SendsEachReadAsOneBinaryFrame()
    {
        WsUploadFrames frames = new();
        ScriptedFrameSourceStream upload = new(frames, ScriptedFrameSourceStream.Gives("abc"), ScriptedFrameSourceStream.Gives("de"));

        (TransferResult result, byte[] afterRequest) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frames(Frame(WsOpcode.Binary, "abc"), Frame(WsOpcode.Binary, "de")), afterRequest);
        CollectionAssert.AreEqual(new[] { UploadReadSize, UploadReadSize, UploadReadSize }, upload.AskedLengths);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(17L, result.Report!.UploadSize);
        Assert.IsFalse(frames.CanStartFrame);
    }

    [TestMethod]
    public async Task ExecuteAsync_FrameStartedInARead_FillsItWithTheReadsThatFollow()
    {
        WsUploadFrames frames = new();
        bool couldStart = false;
        ScriptedFrameSourceStream upload = new(
            frames,
            buffer =>
            {
                couldStart = frames.CanStartFrame;
                frames.StartFrame(WsOpcode.Text, isFinal: true, 5, new FixedRandomSource());
                return ScriptedFrameSourceStream.Gives("hel")(buffer);
            },
            ScriptedFrameSourceStream.Gives("lo"));

        (_, byte[] afterRequest) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frame(WsOpcode.Text, "hello"), afterRequest);
        CollectionAssert.AreEqual(new[] { UploadReadSize, 2, UploadReadSize }, upload.AskedLengths);
        Assert.IsTrue(couldStart);
    }

    [TestMethod]
    public async Task ExecuteAsync_FrameStartedInAReadOfNothing_SendsItsHeadAndReadsOn()
    {
        WsUploadFrames frames = new();
        ScriptedFrameSourceStream upload = new(
            frames,
            _ =>
            {
                frames.StartFrame(WsOpcode.Text, isFinal: true, 2, new FixedRandomSource());
                return 0;
            },
            ScriptedFrameSourceStream.Gives("ok"));

        (_, byte[] afterRequest) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frame(WsOpcode.Text, "ok"), afterRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadGivesMoreThanTheStartedFrameHolds_SendsOnlyWhatItHolds()
    {
        // ws_enc_write_payload writes no more than ws->enc.payload_remain; the rest of the read is dropped.
        WsUploadFrames frames = new();
        ScriptedFrameSourceStream upload = new(
            frames,
            buffer =>
            {
                frames.StartFrame(WsOpcode.Text, isFinal: true, 1, new FixedRandomSource());
                return ScriptedFrameSourceStream.Gives("xyz")(buffer);
            });

        (_, byte[] afterRequest) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frame(WsOpcode.Text, "x"), afterRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_FramedUploadReadFails_SendsTheFramesReadBeforeTheFailure()
    {
        ScriptedFrameSourceStream upload = new(new WsUploadFrames(), ScriptedFrameSourceStream.Gives("a"), _ => throw new IOException("gone"));

        (TransferResult result, byte[] afterRequest) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frame(WsOpcode.Binary, "a"), afterRequest);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_FrameSourceWithNoFrames_SendsTheWholeUploadAsOneFrame()
    {
        ScriptedFrameSourceStream upload = new(null, ScriptedFrameSourceStream.Gives("ab"), ScriptedFrameSourceStream.Gives("c"));

        (_, byte[] afterRequest) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frame(WsOpcode.Binary, "abc"), afterRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_FrameSourceUnderRawMode_SendsTheBytesUnframed()
    {
        ScriptedFrameSourceStream upload = new(new WsUploadFrames(), ScriptedFrameSourceStream.Gives("ab"));

        (_, byte[] afterRequest) = await RunAsync(upload, rawMode: true);

        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("ab"), afterRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_FramedUploadTraced_WritesEachBinaryFrameEncoded()
    {
        ScriptedFrameSourceStream upload = new(new WsUploadFrames(), ScriptedFrameSourceStream.Gives("abc"));
        RecordingTransferEvents events = new();

        await RunAsync(upload, events: events, tracesFrames: true);

        CollectionAssert.Contains(events.Info, "[WS] WS-ENC: sending [BIN payload=0/3]");
    }

    private static async Task<(TransferResult Result, byte[] AfterRequest)> RunAsync(Stream upload, bool rawMode = false, RecordingTransferEvents? events = null, bool tracesFrames = false)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Head101 + "\x81\x02ok"));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"),
            Output = new MemoryStream(),
            Upload = upload,
            Events = events ?? (ITransferEvents)NoTransferEvents.Instance,
        };
        TransferResult result = await new WsProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator(), new FixedRandomSource())
        {
            RawMode = rawMode,
            TracesFrames = tracesFrames,
        }.ExecuteAsync(context);
        byte[] sent = connection.Sent;
        int requestEnd = Encoding.Latin1.GetString(sent).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        return (result, sent[requestEnd..]);
    }

    private static byte[] Frame(WsOpcode opcode, string payload) =>
        WsFrameEncoder.Encode(opcode, isFinal: true, Encoding.Latin1.GetBytes(payload), new FixedRandomSource());

    private static byte[] Frames(params byte[][] frames) => [.. frames.SelectMany(frame => frame)];
}
