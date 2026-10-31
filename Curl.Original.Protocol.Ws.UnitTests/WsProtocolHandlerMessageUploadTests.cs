using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws.Fakes;

namespace Curl.Original.Protocol.Ws;

/// <summary>
/// Pins an upload that gives whole messages (<see cref="IWsUploadMessageSource" />, BL-4253): each message goes out as
/// one final masked frame of its own opcode, in order, before the handler receives.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerMessageUploadTests
{
    private const string Head101 = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_TwoTextMessages_SendsTwoFinalMaskedTextFramesInOrder()
    {
        MessageUploadStream upload = new(Text("a"), Text("b"));

        (TransferResult result, byte[] afterRequest, _) = await RunAsync(upload);

        CollectionAssert.AreEqual(Frames(Frame(WsOpcode.Text, "a"), Frame(WsOpcode.Text, "b")), afterRequest);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, upload.ReadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextAndBinaryMessages_SendsEachWithItsOwnOpcode()
    {
        MessageUploadStream upload = new(Text("hi"), new WsUploadMessage(WsOpcode.Binary, new byte[] { 1, 2, 3 }));

        (_, byte[] afterRequest, _) = await RunAsync(upload);

        byte[] binary = WsFrameEncoder.Encode(WsOpcode.Binary, isFinal: true, new byte[] { 1, 2, 3 }, new FixedRandomSource());
        CollectionAssert.AreEqual(Frames(Frame(WsOpcode.Text, "hi"), binary), afterRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageUpload_ReportsTheSumOfThePayloadsAsUploadSizeAndProgress()
    {
        MessageUploadStream upload = new(Text("abc"), Text("de"));

        (TransferResult result, _, RecordingProgress progress) = await RunAsync(upload);

        Assert.AreEqual(5L, result.Report!.UploadSize);
        CollectionAssert.Contains(progress.Reports.ToList(), "up 5/5");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoMessages_SendsNothingAndSaysItIsCompletelyUploaded()
    {
        RecordingTransferEvents events = new();

        (TransferResult result, byte[] afterRequest, _) = await RunAsync(new MessageUploadStream(), events: events);

        Assert.AreEqual(0, afterRequest.Length);
        CollectionAssert.Contains(events.Info, "We are completely uploaded and fine");
        Assert.AreEqual(0L, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageUploadTraced_WritesEachFrameEncodedWithItsOpcode()
    {
        RecordingTransferEvents events = new();

        await RunAsync(new MessageUploadStream(Text("abc")), events: events, tracesFrames: true);

        CollectionAssert.Contains(events.Info, "[WS] WS-ENC: sending [TEXT payload=0/3]");
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageSourceUnderRawMode_SendsTheStreamBytesUnframed()
    {
        MessageUploadStream upload = new(Encoding.Latin1.GetBytes("raw"), Text("a"));

        (_, byte[] afterRequest, _) = await RunAsync(upload, rawMode: true);

        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes("raw"), afterRequest);
    }

    private static WsUploadMessage Text(string payload) => new(WsOpcode.Text, Encoding.UTF8.GetBytes(payload));

    private static async Task<(TransferResult Result, byte[] AfterRequest, RecordingProgress Progress)> RunAsync(
        Stream upload,
        bool rawMode = false,
        RecordingTransferEvents? events = null,
        bool tracesFrames = false)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Head101 + "\x81\x02ok"));
        RecordingProgress progress = new();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"),
            Output = new MemoryStream(),
            Upload = upload,
            Progress = progress,
            Events = events ?? (ITransferEvents)NoTransferEvents.Instance,
        };
        TransferResult result = await new WsProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator(), new FixedRandomSource())
        {
            RawMode = rawMode,
            TracesFrames = tracesFrames,
        }.ExecuteAsync(context);
        byte[] sent = connection.Sent;
        int requestEnd = Encoding.Latin1.GetString(sent).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        return (result, sent[requestEnd..], progress);
    }

    private static byte[] Frame(WsOpcode opcode, string payload) =>
        WsFrameEncoder.Encode(opcode, isFinal: true, Encoding.UTF8.GetBytes(payload), new FixedRandomSource());

    private static byte[] Frames(params byte[][] frames) => [.. frames.SelectMany(frame => frame)];

    /// <summary>An upload stream that gives whole messages, and counts the reads of its own bytes.</summary>
    private sealed class MessageUploadStream(byte[] bytes, params WsUploadMessage[] messages) : MemoryStream(bytes), IWsUploadMessageSource
    {
        public MessageUploadStream(params WsUploadMessage[] messages)
            : this([], messages)
        {
        }

        public IReadOnlyList<WsUploadMessage> Messages => messages;

        public int ReadCount { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
