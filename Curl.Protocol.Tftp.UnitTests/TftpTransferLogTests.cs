using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins the lines a <c>tftp://</c> transfer writes to Curl's diagnostic log under the
/// <c>tftp</c> component (ADR-0222, BL-927): the request and the options agreed at
/// <c>info</c>, a retransmission and a changed block size at <c>warning</c>, an ERROR
/// packet and the failure that ends the transfer at <c>error</c>, and each packet at
/// <c>verbose</c>.
/// </summary>
[TestClass]
public sealed class TftpTransferLogTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsTheRequestTheAgreedOptionsAndTheEndAtInfo()
    {
        var log = new RecordingDiagnosticLog();
        var channel = Channel(OptionAcknowledgement("tsize\05\0blksize\0512\0timeout\06\0"), Data(1, "hello"));

        await Handler(channel).ExecuteAsync(Context(log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Assert.HasCount(3, info);
        Assert.AreEqual("read request sent for file.txt: blksize 512, timeout 6", info[0]);
        Assert.AreEqual("options agreed: tsize 5, blksize 512, timeout 6", info[1]);
        Assert.StartsWith("transfer done: 5 bytes in ", info[2]);
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Tftp));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoOptions_LogsARequestWithoutOptions()
    {
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = new MemoryStream(),
            TftpNoOptions = true,
            DiagnosticLog = log,
        };

        await Handler(Channel(Data(1, "hello"))).ExecuteAsync(context);

        Assert.AreEqual("read request sent for file.txt: no options", log.MessagesAt(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsEachPacketAtVerbose()
    {
        var log = new RecordingDiagnosticLog();
        var channel = Channel(OptionAcknowledgement("blksize\0512\0"), Data(1, "hello"));

        await Handler(channel).ExecuteAsync(Context(log));

        CollectionAssert.AreEqual(
            new[] { "received OACK, 14 bytes", "received DATA block 1, 9 bytes" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerChangesBlockSize_LogsAWarning()
    {
        var log = new RecordingDiagnosticLog();
        var channel = Channel(OptionAcknowledgement("blksize\01024\0"), Data(1, "hello"));

        await Handler(channel).ExecuteAsync(Context(log));

        CollectionAssert.AreEqual(
            new[] { "server changed or ignored the blksize asked for: using 1024" },
            log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerChangesBlockSizeAtErrorLevel_LogsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        var channel = Channel(OptionAcknowledgement("blksize\01024\0"), Data(1, "hello"));

        await Handler(channel).ExecuteAsync(Context(log));

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorPacket_LogsItsCodeTextAndExitCodeAtError()
    {
        var log = new RecordingDiagnosticLog();
        var channel = Channel(([0, 5, 0, 1, .. "File not found"u8, 0], TransferEndPoint));

        await Handler(channel).ExecuteAsync(Context(log));

        CollectionAssert.AreEqual(
            new[]
            {
                "server sent TFTP error 1: File not found",
                "transfer failed with TftpNotFound (exit 68): TFTP: File Not Found",
            },
            log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServer_LogsEachRetransmissionAtWarning()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingDiagnosticLog();
        var channel = new FallsSilentDatagramChannel(ServerEndPoint, clock);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = new MemoryStream(),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            TimeProvider = clock,
            DiagnosticLog = log,
        };

        await Handler(channel).ExecuteAsync(context);

        CollectionAssert.AreEqual(
            new[]
            {
                "no answer: sending the last packet again, retry 2 of 3",
                "no answer: sending the last packet again, retry 3 of 3",
            },
            log.MessagesAt(DiagnosticLogLevel.Warning));
        Assert.AreEqual(
            "transfer failed with CouldntConnect (exit 7): Could not connect to server",
            log.MessagesAt(DiagnosticLogLevel.Error).Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_RecordsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await Handler(Channel(OptionAcknowledgement("blksize\0512\0"), Data(1, "hello"))).ExecuteAsync(Context(log));
        await Handler(Channel(([0, 5, 0, 1, .. "gone"u8, 0], TransferEndPoint))).ExecuteAsync(Context(log));

        Assert.HasCount(2, log.Lines);
        Assert.IsTrue(log.Lines.All(line => line.Level == DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_ThroughHttpProxy_SetsTheLogOnTheProxyTarget()
    {
        var log = new RecordingDiagnosticLog();
        var proxyConnector = new RecordingConnector(ConnectResult.Refused("Failed to connect"));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = new MemoryStream(),
            Proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18331, null),
            DiagnosticLog = log,
        };

        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(Channel())), proxyConnector)
            .ExecuteAsync(context);

        Assert.AreSame(log, proxyConnector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 4, 0, 7 }, "received ACK block 7, 4 bytes")]
    [DataRow(new byte[] { 0, 5, 0, 2 }, "received ERROR code 2, 4 bytes")]
    [DataRow(new byte[] { 0, 9, 0, 0 }, "received opcode 9, 4 bytes")]
    public void Received_Packet_NamesItsOpcode(byte[] packet, string expected)
    {
        var log = new RecordingDiagnosticLog();

        new TftpTransferLog(log).Received(packet);

        Assert.AreEqual(expected, log.Lines.Single().Message);
    }

    [TestMethod]
    public void ErrorPacket_TextWithoutTerminator_LogsTheWholeText()
    {
        var log = new RecordingDiagnosticLog();

        new TftpTransferLog(log).ErrorPacket([0, 5, 0, 3, .. "full"u8]);

        Assert.AreEqual("server sent TFTP error 3: full", log.Lines.Single().Message);
    }

    private static TftpProtocolHandler Handler(IDatagramChannel channel) =>
        new(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)));

    private static TransferContext Context(IDiagnosticLog log) =>
        new() { Url = CurlUrl.Parse("tftp://h/file.txt"), Output = new MemoryStream(), DiagnosticLog = log };

    private static ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script) =>
        new(ServerEndPoint, script);

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);
}
