using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws.Fakes;

namespace Curl.Original.Protocol.Ws;

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
    public async Task ExecuteAsync_FrameSourceWithNoFrames_FillsEachReadBeforeFramingIt()
    {
        ScriptedFrameSourceStream upload = new(null, ScriptedFrameSourceStream.Gives("ab"), ScriptedFrameSourceStream.Gives("c"));

        (_, byte[] afterRequest) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frame(WsOpcode.Binary, "abc"), afterRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_ToolUploadOf200000Bytes_SendsCurlsFramesOf65536And3392Bytes()
    {
        // Measured with curl 8.22.0 -v --trace-config ws -T <200 000-byte file> ws://127.0.0.1:18683/ (BL-2683):
        // WS-ENC sending [BIN payload=0/65536] three times, then [BIN payload=0/3392];
        // "upload completely sent off: 200050 bytes" and %{size_upload} 200050, the frame heads counted.
        byte[] file = new byte[200_000];
        for (int index = 0; index < file.Length; index++)
        {
            file[index] = (byte)index;
        }

        RecordingTransferEvents events = new();

        (TransferResult result, byte[] afterRequest) = await RunAsync(new MemoryStream(file), events: events, tracesFrames: true);

        byte[] expected = Frames(
            BinaryFrame(file.AsSpan(0, 65_536)),
            BinaryFrame(file.AsSpan(65_536, 65_536)),
            BinaryFrame(file.AsSpan(131_072, 65_536)),
            BinaryFrame(file.AsSpan(196_608, 3_392)));
        CollectionAssert.AreEqual(expected, afterRequest);
        CollectionAssert.AreEqual(
            new[] { "0/65536", "0/65536", "0/65536", "0/3392" },
            events.Info.Where(line => line.StartsWith("[WS] WS-ENC: sending", StringComparison.Ordinal)).Select(line => line[^8..^1].TrimStart('=')).ToArray());
        CollectionAssert.Contains(events.Info, "upload completely sent off: 200050 bytes");
        Assert.AreEqual(200_050L, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ToolUploadReadInParts_FillsEachFrameTo65536Bytes()
    {
        // The tool's fread fills its buffer from a pipe, so short reads of standard input still make 64 KiB frames.
        ScriptedFrameSourceStream upload = new(
            null,
            ScriptedFrameSourceStream.Gives(new string('a', 40_000)),
            ScriptedFrameSourceStream.Gives(new string('b', 25_536)),
            ScriptedFrameSourceStream.Gives("c"));

        (_, byte[] afterRequest) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frames(Frame(WsOpcode.Binary, new string('a', 40_000) + new string('b', 25_536)), Frame(WsOpcode.Binary, "c")), afterRequest);
        CollectionAssert.AreEqual(new[] { UploadReadSize, 25_536, UploadReadSize, UploadReadSize - 1, UploadReadSize }, upload.AskedLengths);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyToolUpload_SendsNoFrameAndSaysItIsCompletelyUploaded()
    {
        // Measured with curl 8.22.0 -v -T <empty file> ws://...: nothing after the request,
        // "We are completely uploaded and fine" and %{size_upload} 0.
        RecordingTransferEvents events = new();

        (TransferResult result, byte[] afterRequest) = await RunAsync(new MemoryStream(), events: events);

        Assert.AreEqual(0, afterRequest.Length);
        CollectionAssert.Contains(events.Info, "We are completely uploaded and fine");
        Assert.AreEqual(0L, result.Report!.UploadSize);
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

    private static byte[] BinaryFrame(ReadOnlySpan<byte> payload) =>
        WsFrameEncoder.Encode(WsOpcode.Binary, isFinal: true, payload, new FixedRandomSource());

    private static byte[] Frames(params byte[][] frames) => [.. frames.SelectMany(frame => frame)];
}
