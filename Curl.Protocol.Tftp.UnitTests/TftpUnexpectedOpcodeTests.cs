using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins what a <c>tftp://</c> transfer does with a packet whose opcode its state does not
/// handle, as curl 8.21.0 does (measured by BL-1435 with <c>Record-CurlExchange.ps1 -Tftp
/// -TftpReply '&lt;step&gt;=PACKET &lt;hex&gt;'</c>): exit 71 for a download, and before the
/// server has answered for an upload too; once an upload has been answered, <c>-v</c> lines
/// and the upload carries on to its ACK. BL-1444 measured the opcodes curl reads as other
/// events: 0 and 7 as the first reply re-send the request, 7 later is a timeout, and an ACK
/// as a download's first reply or DATA as an upload's switches the transfer's direction.
/// </summary>
[TestClass]
public sealed class TftpUnexpectedOpcodeTests
{
    private const string Trying = "  Trying 127.0.0.1:69...";

    private const string Established = "Established connection to 127.0.0.1 (127.0.0.1 port 69) from  port 0 ";

    private const string StartTimeouts = "set timeouts for state 0; Total 300000, retry 6 maxtry 50";

    private const string ShuttingDown = "shutting down connection #0";

    private const string ReceiveTimeouts = "set timeouts for state 1; Total 0, retry 5 maxtry 3";

    private const string TransmitTimeouts = "set timeouts for state 2; Total 0, retry 5 maxtry 3";

    private const string UnexpectedPacket = "Internal error: Unexpected packet";

    private const string SendFirst = "tftp_send_first: internal error";

    private const string ReceiveInternalError = "tftp_rx: internal error";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    private static readonly string FullBlock = new('x', 512);

    private static readonly (byte[]? Datagram, EndPoint Source) Silence = (null, TransferEndPoint);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    /// <summary>Measured: an RRQ, a WRQ or opcode 9 as the first reply is exit 71, one <c>-v</c> line, nothing sent.</summary>
    /// <param name="opcode">The reply's opcode.</param>
    [TestMethod]
    [DataRow((ushort)1)]
    [DataRow((ushort)2)]
    [DataRow((ushort)9)]
    public async Task ExecuteAsync_DownloadWhoseFirstReplyIsAnUnknownOpcode_Exits71WithUnexpectedPacketAfterTheSendFirstLine(ushort opcode)
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        Diagnostics.Arrange("first reply opcode", opcode);
        var channel = Channel(clock, Packet(opcode), Data(1, "end"));

        var result = await Run(channel, Context(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("error message", UnexpectedPacket, result.ErrorMessage);
        Assert.AreEqual(UnexpectedPacket, result.ErrorMessage);
        var expectedSteps = new[] { Trying, Established, StartTimeouts, SendFirst, ShuttingDown };
        Diagnostics.Assert("events", Steps(expectedSteps), Steps(events.Steps));
        CollectionAssert.AreEqual(expectedSteps, events.Steps);
        Diagnostics.Assert("datagrams sent", 1, channel.Sent.Count);
        Assert.HasCount(1, channel.Sent);
    }

    /// <summary>Measured: opcode 0, 1, 2 or 9 after DATA 1 is exit 71 with both lines, the first kept as the message.</summary>
    /// <param name="opcode">The packet's opcode.</param>
    [TestMethod]
    [DataRow((ushort)0)]
    [DataRow((ushort)1)]
    [DataRow((ushort)2)]
    [DataRow((ushort)9)]
    public async Task ExecuteAsync_DownloadReceivingAnUnknownOpcodeAfterDataOne_Exits71WithUnexpectedPacketAndBothLines(ushort opcode)
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        Diagnostics.Arrange("second reply opcode", opcode);
        var channel = Channel(clock, Data(1, FullBlock), Packet(opcode), Data(2, "end"));
        var context = Context(events, clock);

        var result = await Run(channel, context, events);

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("error message", UnexpectedPacket, result.ErrorMessage);
        Assert.AreEqual(UnexpectedPacket, result.ErrorMessage);
        var expectedSteps = new[] { UnexpectedPacket, ReceiveInternalError, ShuttingDown };
        Diagnostics.Assert("last 3 events", Steps(expectedSteps), Steps(events.Steps.TakeLast(3)));
        CollectionAssert.AreEqual(expectedSteps, events.Steps.TakeLast(3).ToArray());
        Diagnostics.Assert("datagrams sent", 2, channel.Sent.Count);
        Assert.HasCount(2, channel.Sent);
        Diagnostics.Assert("output length", 512, ((MemoryStream)context.Output).Length);
        Assert.AreEqual(512, ((MemoryStream)context.Output).Length);
    }

    /// <summary>Measured: an ACK after DATA 1 is exit 71 <c>tftp_rx: internal error</c>, with no unexpected-packet line.</summary>
    [TestMethod]
    public async Task ExecuteAsync_DownloadReceivingAnAckAfterDataOne_Exits71WithTheReceiveInternalError()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(clock, Data(1, FullBlock), Packet(4, 1), Data(2, "end"));

        var result = await Run(channel, Context(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("error message", ReceiveInternalError, result.ErrorMessage);
        Assert.AreEqual(ReceiveInternalError, result.ErrorMessage);
        var expectedSteps = new[] { ReceiveInternalError, ShuttingDown };
        Diagnostics.Assert("last 2 events", Steps(expectedSteps), Steps(events.Steps.TakeLast(2)));
        CollectionAssert.AreEqual(expectedSteps, events.Steps.TakeLast(2).ToArray());
        Diagnostics.Assert("events contain the unexpected-packet line", false, events.Steps.Contains(UnexpectedPacket));
        CollectionAssert.DoesNotContain(events.Steps, UnexpectedPacket);
        Diagnostics.Assert("datagrams sent", 2, channel.Sent.Count);
        Assert.HasCount(2, channel.Sent);
    }

    /// <summary>
    /// Measured (BL-1444): opcode 0 or 7 as the first reply notes the unexpected packet,
    /// re-sends the read request and the download completes.
    /// </summary>
    /// <param name="opcode">The first reply's opcode.</param>
    [TestMethod]
    [DataRow((ushort)0)]
    [DataRow((ushort)7)]
    public async Task ExecuteAsync_DownloadWhoseFirstReplyIsCurlsInitOrTimeoutEvent_ResendsTheRequestAndCompletes(ushort opcode)
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        Diagnostics.Arrange("first reply opcode", opcode);
        var channel = Channel(clock, Packet(opcode), Data(1, FullBlock), Data(2, "end"));
        var context = Context(events, clock);

        var result = await Run(channel, context, events);

        Diagnostics.Assert("succeeded", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        Diagnostics.Assert("output length", 515, ((MemoryStream)context.Output).Length);
        Assert.AreEqual(515, ((MemoryStream)context.Output).Length);
        var expectedSteps = new[] { Trying, Established, StartTimeouts, UnexpectedPacket, "<= " + FullBlock, "Connected for receive", ReceiveTimeouts, "<= end", ShuttingDown };
        Diagnostics.Assert("events", Steps(expectedSteps), Steps(events.Steps));
        CollectionAssert.AreEqual(expectedSteps, events.Steps);
        Diagnostics.Assert("datagrams sent", 4, channel.Sent.Count);
        Assert.HasCount(4, channel.Sent);
        Diagnostics.Diff("request re-sent byte-identical", channel.Sent[0], channel.Sent[1]);
        CollectionAssert.AreEqual(channel.Sent[0], channel.Sent[1]);
        Diagnostics.Diff("sent 2 is ACK 1", new byte[] { 0, 4, 0, 1 }, channel.Sent[2]);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[2]);
    }

    /// <summary>Measured (BL-1444): opcode 7 after DATA 1 is taken as a timeout, re-sending ACK 1, and the download completes.</summary>
    [TestMethod]
    public async Task ExecuteAsync_DownloadReceivingCurlsTimeoutEventAfterDataOne_ResendsTheAckAndCompletes()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(clock, Data(1, FullBlock), Packet(7), Data(2, "end"));
        var context = Context(events, clock);

        var result = await Run(channel, context, events);

        Diagnostics.Assert("succeeded", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        Diagnostics.Assert("output length", 515, ((MemoryStream)context.Output).Length);
        Assert.AreEqual(515, ((MemoryStream)context.Output).Length);
        var expectedSteps = new[] { ReceiveTimeouts, UnexpectedPacket, "Timeout waiting for block 2 ACK. Retries = 1", "<= end", ShuttingDown };
        Diagnostics.Assert("last 5 events", Steps(expectedSteps), Steps(events.Steps.TakeLast(5)));
        CollectionAssert.AreEqual(expectedSteps, events.Steps.TakeLast(5).ToArray());
        Diagnostics.Assert("datagrams sent", 4, channel.Sent.Count);
        Assert.HasCount(4, channel.Sent);
        Diagnostics.Diff("sent 2 is ACK 1", new byte[] { 0, 4, 0, 1 }, channel.Sent[2]);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[2]);
    }

    /// <summary>
    /// curl keeps the first failure it noted, so a download whose retries run out after
    /// opcode 7 ends with exit 28 and the unexpected-packet message.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DownloadWhoseRetriesRunOutAfterCurlsTimeoutEvent_Exits28WithTheUnexpectedPacketMessage()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(
            clock, Data(1, FullBlock), Packet(7), Packet(7), Packet(7), Packet(7));

        var result = await Run(channel, Context(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", UnexpectedPacket, result.ErrorMessage);
        Assert.AreEqual(UnexpectedPacket, result.ErrorMessage);
        Diagnostics.Assert("second-last event", "Timeout waiting for block 2 ACK. Retries = 4", events.Steps[^2]);
        Assert.AreEqual("Timeout waiting for block 2 ACK. Retries = 4", events.Steps[^2]);
        Diagnostics.Assert("datagrams sent", 5, channel.Sent.Count);
        Assert.HasCount(5, channel.Sent);
    }

    /// <summary>
    /// Measured (BL-1444): an ACK 0 as a download's first reply connects for transmit and
    /// sends an empty DATA 1; the server's DATA 1 is noted as event 3, and with no ACK the
    /// retries run out with exit 28 and that message.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DownloadWhoseFirstReplyIsAnAck_TransmitsAnEmptyBlockAndExits28WithTheTransmitInternalError()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(
            clock, Packet(4, 0), Data(1, FullBlock), Silence, Silence, Silence, Silence);
        var context = Context(events, clock);

        var result = await Run(channel, context, events);

        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "tftp_tx: internal error, event: 3", result.ErrorMessage);
        Assert.AreEqual("tftp_tx: internal error, event: 3", result.ErrorMessage);
        Diagnostics.Assert("output length", 0, ((MemoryStream)context.Output).Length);
        Assert.AreEqual(0, ((MemoryStream)context.Output).Length);
        var expectedSteps = new[]
        {
            Trying, Established, StartTimeouts, "Connected for transmit", TransmitTimeouts, "tftp_tx: internal error, event: 3",
            "Timeout waiting for block 2 ACK. Retries = 1", "Timeout waiting for block 2 ACK. Retries = 2",
            "Timeout waiting for block 2 ACK. Retries = 3", "Timeout waiting for block 2 ACK. Retries = 4", ShuttingDown,
        };
        Diagnostics.Assert("events", Steps(expectedSteps), Steps(events.Steps));
        CollectionAssert.AreEqual(expectedSteps, events.Steps);
        Diagnostics.Assert("datagrams sent", 5, channel.Sent.Count);
        Assert.HasCount(5, channel.Sent);
        foreach (var sent in channel.Sent.Skip(1))
        {
            Diagnostics.Diff("re-sent empty DATA 1", new byte[] { 0, 3, 0, 1 }, sent);
            CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1 }, sent);
        }
    }

    /// <summary>Measured: a DATA packet while an upload waits for ACK 1 is noted as event 3 and the upload completes.</summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadReceivingDataWhileWaitingForItsAck_ReportsTheTransmitInternalErrorAndCompletes()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(clock, Packet(4, 0), Packet(3, 1), Packet(4, 1));

        var result = await Run(channel, UploadContext(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var expectedSteps = new[] { "tftp_tx: internal error, event: 3", ShuttingDown };
        Diagnostics.Assert("last 2 events", Steps(expectedSteps), Steps(events.Steps.TakeLast(2)));
        CollectionAssert.AreEqual(expectedSteps, events.Steps.TakeLast(2).ToArray());
        Diagnostics.Assert("events contain the unexpected-packet line", false, events.Steps.Contains(UnexpectedPacket));
        CollectionAssert.DoesNotContain(events.Steps, UnexpectedPacket);
        Diagnostics.Assert("datagrams sent", 2, channel.Sent.Count);
        Assert.HasCount(2, channel.Sent);
    }

    /// <summary>Measured: opcode 0, 1 or 9 while an upload waits for ACK 1 is noted twice and the upload completes.</summary>
    /// <param name="opcode">The packet's opcode.</param>
    [TestMethod]
    [DataRow((ushort)0)]
    [DataRow((ushort)1)]
    [DataRow((ushort)9)]
    public async Task ExecuteAsync_UploadReceivingAnUnknownOpcodeWhileWaitingForItsAck_ReportsBothLinesAndCompletes(ushort opcode)
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        Diagnostics.Arrange("second reply opcode", opcode);
        var channel = Channel(clock, Packet(4, 0), Packet(opcode), Packet(4, 1));

        var result = await Run(channel, UploadContext(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var expectedSteps = new[] { UnexpectedPacket, $"tftp_tx: internal error, event: {opcode}", ShuttingDown };
        Diagnostics.Assert("last 3 events", Steps(expectedSteps), Steps(events.Steps.TakeLast(3)));
        CollectionAssert.AreEqual(expectedSteps, events.Steps.TakeLast(3).ToArray());
        Diagnostics.Assert("datagrams sent", 2, channel.Sent.Count);
        Assert.HasCount(2, channel.Sent);
    }

    /// <summary>Measured: opcode 9 as an upload's first reply is exit 71 after the send-first line.</summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWhoseFirstReplyIsAnUnknownOpcode_Exits71WithUnexpectedPacketAfterTheSendFirstLine()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(clock, Packet(9), Packet(4, 0), Packet(4, 1));

        var result = await Run(channel, UploadContext(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Diagnostics.Assert("error message", UnexpectedPacket, result.ErrorMessage);
        Assert.AreEqual(UnexpectedPacket, result.ErrorMessage);
        var expectedSteps = new[] { Trying, Established, StartTimeouts, SendFirst, ShuttingDown };
        Diagnostics.Assert("events", Steps(expectedSteps), Steps(events.Steps));
        CollectionAssert.AreEqual(expectedSteps, events.Steps);
        Diagnostics.Assert("datagrams sent", 1, channel.Sent.Count);
        Assert.HasCount(1, channel.Sent);
    }

    /// <summary>
    /// Measured (BL-1444): opcode 0 or 7 as an upload's first reply notes the unexpected
    /// packet, re-sends the write request and the upload completes.
    /// </summary>
    /// <param name="opcode">The first reply's opcode.</param>
    [TestMethod]
    [DataRow((ushort)0)]
    [DataRow((ushort)7)]
    public async Task ExecuteAsync_UploadWhoseFirstReplyIsCurlsInitOrTimeoutEvent_ResendsTheRequestAndCompletes(ushort opcode)
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        Diagnostics.Arrange("first reply opcode", opcode);
        var channel = Channel(clock, Packet(opcode), Packet(4, 0), Packet(4, 1));

        var result = await Run(channel, UploadContext(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var expectedSteps = new[] { Trying, Established, StartTimeouts, UnexpectedPacket, "Connected for transmit", TransmitTimeouts, ShuttingDown };
        Diagnostics.Assert("events", Steps(expectedSteps), Steps(events.Steps));
        CollectionAssert.AreEqual(expectedSteps, events.Steps);
        Diagnostics.Assert("datagrams sent", 3, channel.Sent.Count);
        Assert.HasCount(3, channel.Sent);
        Diagnostics.Diff("request re-sent byte-identical", channel.Sent[0], channel.Sent[1]);
        CollectionAssert.AreEqual(channel.Sent[0], channel.Sent[1]);
    }

    /// <summary>Measured (BL-1444): opcode 7 while an upload waits for ACK 1 is taken as a timeout, re-sending DATA 1.</summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadReceivingCurlsTimeoutEventWhileWaitingForItsAck_ResendsTheBlockAndCompletes()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(clock, Packet(4, 0), Packet(7), Packet(4, 1));

        var result = await Run(channel, UploadContext(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var expectedSteps = new[] { TransmitTimeouts, UnexpectedPacket, "Timeout waiting for block 2 ACK. Retries = 1", ShuttingDown };
        Diagnostics.Assert("last 4 events", Steps(expectedSteps), Steps(events.Steps.TakeLast(4)));
        CollectionAssert.AreEqual(expectedSteps, events.Steps.TakeLast(4).ToArray());
        Diagnostics.Assert("datagrams sent", 3, channel.Sent.Count);
        Assert.HasCount(3, channel.Sent);
        Diagnostics.Diff("DATA 1 re-sent byte-identical", channel.Sent[1], channel.Sent[2]);
        CollectionAssert.AreEqual(channel.Sent[1], channel.Sent[2]);
    }

    /// <summary>
    /// Measured (BL-1444): a DATA packet as an upload's first reply connects for receive,
    /// writes the block to the output and acknowledges it, and a short block ends the
    /// transfer with exit 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWhoseFirstReplyIsData_ReceivesTheBlockAndCompletes()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(clock, Data(1, "A"), Packet(4, 0));
        var context = UploadContext(events, clock);

        var result = await Run(channel, context, events);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var output = Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray());
        Diagnostics.Diff("output text", "A", output);
        Assert.AreEqual("A", output);
        var expectedSteps = new[] { Trying, Established, StartTimeouts, "<= A", "Connected for receive", ReceiveTimeouts, ShuttingDown };
        Diagnostics.Assert("events", Steps(expectedSteps), Steps(events.Steps));
        CollectionAssert.AreEqual(expectedSteps, events.Steps);
        Diagnostics.Assert("datagrams sent", 2, channel.Sent.Count);
        Assert.HasCount(2, channel.Sent);
        Diagnostics.Diff("sent 1 is ACK 1", new byte[] { 0, 4, 0, 1 }, channel.Sent[1]);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[1]);
    }

    /// <summary>
    /// An upload keeps its notes as curl does: once <c>tftp_tx: internal error, event: 3</c>
    /// is noted, an upload whose retries then run out ends with exit 28 and that message.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWhoseRetriesRunOutAfterAnUnexpectedData_Exits28WithTheTransmitInternalError()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Channel(
            clock, Packet(4, 0), Packet(3, 1), Packet(7), Packet(7), Packet(7), Packet(7));

        var result = await Run(channel, UploadContext(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "tftp_tx: internal error, event: 3", result.ErrorMessage);
        Assert.AreEqual("tftp_tx: internal error, event: 3", result.ErrorMessage);
        Diagnostics.Assert("datagrams sent", 5, channel.Sent.Count);
        Assert.HasCount(5, channel.Sent);
    }

    private static TftpProtocolHandler Handler(IDatagramChannel channel) =>
        new(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)));

    private static string Steps(IEnumerable<string> steps) =>
        string.Join(" | ", steps.Select(step => step.Length > 60 ? step[..60] + "..." : step));

    private static (byte[]? Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[]? Datagram, EndPoint Source) Packet(ushort opcode, ushort field = 0) =>
        ([(byte)(opcode >> 8), (byte)opcode, (byte)(field >> 8), (byte)field], TransferEndPoint);

    /// <summary>Builds the scripted channel and writes each scripted datagram (or silence) as ARRANGE lines.</summary>
    private PausingDatagramChannel Channel(ManualTimeProvider clock, params (byte[]? Datagram, EndPoint Source)[] script)
    {
        for (int index = 0; index < script.Length; index++)
        {
            var (datagram, source) = script[index];
            if (datagram is null)
            {
                Diagnostics.Arrange($"scripted {index}", "silence: the ManualTimeProvider advances to the next timer");
            }
            else
            {
                TftpTestDiagnostics.Scripted(Diagnostics, index, datagram, source);
            }
        }

        return new PausingDatagramChannel(ServerEndPoint, clock, script);
    }

    /// <summary>Runs the handler in a phase, then writes the exit code, error, events, output and datagrams sent.</summary>
    private async Task<TransferResult> Run(PausingDatagramChannel channel, TransferContext context, RecordingTransferEvents events)
    {
        TransferResult result;
        using (Diagnostics.Phase("execute tftp transfer"))
        {
            result = await Handler(channel).ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        Diagnostics.Act("events", Steps(events.Steps));
        if (context.Output is MemoryStream output)
        {
            Diagnostics.Bytes("output written", output.ToArray());
        }

        Diagnostics.Act("datagrams sent", channel.Sent.Count);
        for (int index = 0; index < channel.Sent.Count; index++)
        {
            TftpTestDiagnostics.Datagram(Diagnostics, $"sent {index}", channel.Sent[index]);
        }

        return result;
    }

    private TransferContext Context(ITransferEvents events, TimeProvider clock, Stream? upload = null)
    {
        Diagnostics.Arrange("url", "tftp://127.0.0.1/file.txt");
        Diagnostics.Arrange("options", $"TftpNoOptions=true, upload={(upload is null ? "none" : "yes")}");
        return new()
        {
            Url = CurlUrl.Parse("tftp://127.0.0.1/file.txt"),
            Output = new MemoryStream(),
            Events = events,
            TimeProvider = clock,
            TftpNoOptions = true,
            Upload = upload,
        };
    }

    private TransferContext UploadContext(ITransferEvents events, TimeProvider clock)
    {
        var upload = "upload me"u8.ToArray();
        Diagnostics.Arrange("upload payload", Encoding.ASCII.GetString(upload));
        return Context(events, clock, new MemoryStream(upload));
    }
}
