using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins what a <c>tftp://</c> transfer does with a packet whose opcode its state does not
/// handle, as curl 8.21.0 does (measured by BL-1435 with <c>Record-CurlExchange.ps1 -Tftp
/// -TftpReply '&lt;step&gt;=PACKET &lt;hex&gt;'</c>): exit 71 for a download, and before the
/// server has answered for an upload too; once an upload has been answered, <c>-v</c> lines
/// and the upload carries on to its ACK.
/// </summary>
[TestClass]
public sealed class TftpUnexpectedOpcodeTests
{
    private const string Trying = "  Trying 127.0.0.1:69...";

    private const string Established = "Established connection to 127.0.0.1 (127.0.0.1 port 69) from  port 0 ";

    private const string StartTimeouts = "set timeouts for state 0; Total 300000, retry 6 maxtry 50";

    private const string ShuttingDown = "shutting down connection #0";

    private const string UnexpectedPacket = "Internal error: Unexpected packet";

    private const string SendFirst = "tftp_send_first: internal error";

    private const string ReceiveInternalError = "tftp_rx: internal error";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    private static readonly string FullBlock = new('x', 512);

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
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Packet(opcode), Data(1, "end"));

        var result = await Handler(channel).ExecuteAsync(Context(events, clock));

        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(UnexpectedPacket, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { Trying, Established, StartTimeouts, SendFirst, ShuttingDown }, events.Steps);
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
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Data(1, FullBlock), Packet(opcode), Data(2, "end"));
        var context = Context(events, clock);

        var result = await Handler(channel).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(UnexpectedPacket, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { UnexpectedPacket, ReceiveInternalError, ShuttingDown }, events.Steps.TakeLast(3).ToArray());
        Assert.HasCount(2, channel.Sent);
        Assert.AreEqual(512, ((MemoryStream)context.Output).Length);
    }

    /// <summary>Measured: an ACK after DATA 1 is exit 71 <c>tftp_rx: internal error</c>, with no unexpected-packet line.</summary>
    [TestMethod]
    public async Task ExecuteAsync_DownloadReceivingAnAckAfterDataOne_Exits71WithTheReceiveInternalError()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Data(1, FullBlock), Packet(4, 1), Data(2, "end"));

        var result = await Handler(channel).ExecuteAsync(Context(events, clock));

        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(ReceiveInternalError, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { ReceiveInternalError, ShuttingDown }, events.Steps.TakeLast(2).ToArray());
        CollectionAssert.DoesNotContain(events.Steps, UnexpectedPacket);
        Assert.HasCount(2, channel.Sent);
    }

    /// <summary>
    /// Not modelled (curl reads them as its INIT, TIMEOUT and transmit events): opcode 0 or
    /// an ACK as the first reply, and opcode 7 at any point, are ignored and the download
    /// completes.
    /// </summary>
    /// <param name="first">The first reply's opcode.</param>
    /// <param name="afterDataOne">The opcode of the packet after DATA 1.</param>
    [TestMethod]
    [DataRow((ushort)0, (ushort)7)]
    [DataRow((ushort)4, (ushort)7)]
    [DataRow((ushort)7, (ushort)7)]
    public async Task ExecuteAsync_DownloadReceivingAnOpcodeCurlReadsAsAnotherEvent_IgnoresItAndCompletes(ushort first, ushort afterDataOne)
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(
            ServerEndPoint,
            clock,
            Packet(first),
            Data(1, FullBlock),
            Packet(afterDataOne),
            Data(2, "end"));
        var context = Context(events, clock);

        var result = await Handler(channel).ExecuteAsync(context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(515, ((MemoryStream)context.Output).Length);
        CollectionAssert.DoesNotContain(events.Steps, UnexpectedPacket);
    }

    /// <summary>Measured: a DATA packet while an upload waits for ACK 1 is noted as event 3 and the upload completes.</summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadReceivingDataWhileWaitingForItsAck_ReportsTheTransmitInternalErrorAndCompletes()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Packet(4, 0), Packet(3, 1), Packet(4, 1));

        var result = await Handler(channel).ExecuteAsync(UploadContext(events, clock));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "tftp_tx: internal error, event: 3", ShuttingDown }, events.Steps.TakeLast(2).ToArray());
        CollectionAssert.DoesNotContain(events.Steps, UnexpectedPacket);
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
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Packet(4, 0), Packet(opcode), Packet(4, 1));

        var result = await Handler(channel).ExecuteAsync(UploadContext(events, clock));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { UnexpectedPacket, $"tftp_tx: internal error, event: {opcode}", ShuttingDown },
            events.Steps.TakeLast(3).ToArray());
        Assert.HasCount(2, channel.Sent);
    }

    /// <summary>Measured: opcode 9 as an upload's first reply is exit 71 after the send-first line.</summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWhoseFirstReplyIsAnUnknownOpcode_Exits71WithUnexpectedPacketAfterTheSendFirstLine()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Packet(9), Packet(4, 0), Packet(4, 1));

        var result = await Handler(channel).ExecuteAsync(UploadContext(events, clock));

        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(UnexpectedPacket, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { Trying, Established, StartTimeouts, SendFirst, ShuttingDown }, events.Steps);
        Assert.HasCount(1, channel.Sent);
    }

    /// <summary>
    /// Not modelled (curl turns the upload into a receive): a DATA packet as an upload's first
    /// reply is ignored and the upload completes.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWhoseFirstReplyIsData_IgnoresItAndCompletes()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Packet(3, 1), Packet(4, 0), Packet(4, 1));

        var result = await Handler(channel).ExecuteAsync(UploadContext(events, clock));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.DoesNotContain(events.Steps, SendFirst);
    }

    private static TftpProtocolHandler Handler(IDatagramChannel channel) =>
        new(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)));

    private static TransferContext Context(ITransferEvents events, TimeProvider clock, Stream? upload = null) =>
        new()
        {
            Url = CurlUrl.Parse("tftp://127.0.0.1/file.txt"),
            Output = new MemoryStream(),
            Events = events,
            TimeProvider = clock,
            TftpNoOptions = true,
            Upload = upload,
        };

    private static TransferContext UploadContext(ITransferEvents events, TimeProvider clock) =>
        Context(events, clock, new MemoryStream("upload me"u8.ToArray()));

    private static (byte[]? Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[]? Datagram, EndPoint Source) Packet(ushort opcode, ushort field = 0) =>
        ([(byte)(opcode >> 8), (byte)opcode, (byte)(field >> 8), (byte)field], TransferEndPoint);
}
