using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

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
    private const int LoggedLineLimit = 40;

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsTheRequestTheAgreedOptionsAndTheEndAtInfo()
    {
        var log = new RecordingDiagnosticLog();
        var channel = Channel(OptionAcknowledgement("tsize\05\0blksize\0512\0timeout\06\0"), Data(1, "hello"));

        await RunAsync(channel, Context(log), log);

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Diagnostics.Assert("info line count", 3, info.Length);
        Assert.HasCount(3, info);
        Diagnostics.Assert("info 0", "read request sent for file.txt: blksize 512, timeout 6", info[0]);
        Assert.AreEqual("read request sent for file.txt: blksize 512, timeout 6", info[0]);
        Diagnostics.Assert("info 1", "options agreed: tsize 5, blksize 512, timeout 6", info[1]);
        Assert.AreEqual("options agreed: tsize 5, blksize 512, timeout 6", info[1]);
        Diagnostics.Assert("info 2 starts with", "transfer done: 5 bytes in ", info[2]);
        Assert.StartsWith("transfer done: 5 bytes in ", info[2]);
        Diagnostics.Assert("warning line count", 0, log.MessagesAt(DiagnosticLogLevel.Warning).Length);
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
        Diagnostics.Assert("every line is the tftp component", true, log.Lines.All(line => line.Component == DiagnosticLogComponents.Tftp));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Tftp));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoOptions_LogsARequestWithoutOptions()
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "tftp://h/file.txt");
        Diagnostics.Arrange("no options", true);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = new MemoryStream(),
            TftpNoOptions = true,
            DiagnosticLog = log,
        };

        await RunAsync(Channel(Data(1, "hello")), context, log);

        string first = log.MessagesAt(DiagnosticLogLevel.Info)[0];
        Diagnostics.Assert("first info line", "read request sent for file.txt: no options", first);
        Assert.AreEqual("read request sent for file.txt: no options", log.MessagesAt(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsEachPacketAtVerbose()
    {
        var log = new RecordingDiagnosticLog();
        var channel = Channel(OptionAcknowledgement("blksize\0512\0"), Data(1, "hello"));

        await RunAsync(channel, Context(log), log);

        string[] expected = ["received OACK, 14 bytes", "received DATA block 1, 9 bytes"];
        Diagnostics.Assert("verbose lines", string.Join(" | ", expected), string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(expected, log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerChangesBlockSize_LogsAWarning()
    {
        var log = new RecordingDiagnosticLog();
        var channel = Channel(OptionAcknowledgement("blksize\0256\0"), Data(1, "hello"));

        await RunAsync(channel, Context(log), log);

        string[] expected = ["server changed or ignored the blksize asked for: using 256"];
        Diagnostics.Assert("warning lines", string.Join(" | ", expected), string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(expected, log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerChangesBlockSizeAtErrorLevel_LogsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Error);
        var channel = Channel(OptionAcknowledgement("blksize\0256\0"), Data(1, "hello"));

        await RunAsync(channel, Context(log), log);

        Diagnostics.Assert("recorded line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorPacket_LogsItsCodeTextAndExitCodeAtError()
    {
        var log = new RecordingDiagnosticLog();
        var channel = Channel(([0, 5, 0, 1, .. "File not found"u8, 0], TransferEndPoint));

        await RunAsync(channel, Context(log), log);

        string[] expected =
        [
            "server sent TFTP error 1: File not found",
            "transfer failed with TftpNotFound (exit 68): TFTP: File Not Found",
        ];
        Diagnostics.Assert("error lines", string.Join(" | ", expected), string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Error)));
        CollectionAssert.AreEqual(expected, log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServer_LogsEachRetransmissionAtWarning()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingDiagnosticLog();
        var channel = new FallsSilentDatagramChannel(ServerEndPoint, clock);
        Diagnostics.Arrange("server", "falls silent after the request");
        Diagnostics.Arrange("clock", "ManualTimeProvider");
        Diagnostics.Arrange("connect timeout", TimeSpan.FromSeconds(10));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = new MemoryStream(),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            TimeProvider = clock,
            DiagnosticLog = log,
        };

        TransferResult result;
        using (Diagnostics.Phase("transfer"))
        {
            result = await Handler(channel).ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        Diagnostics.Act("datagrams sent", channel.Sent.Count);
        for (int index = 0; index < Math.Min(channel.Sent.Count, LoggedLineLimit); index++)
        {
            var (datagram, _, at) = channel.Sent[index];
            TftpTestDiagnostics.Datagram(
                Diagnostics,
                string.Create(CultureInfo.InvariantCulture, $"sent {index} at {at.TotalMilliseconds} ms"),
                datagram);
        }

        LogLines(log);
        string[] expected =
        [
            "no answer: sending the last packet again, retry 2 of 3",
            "no answer: sending the last packet again, retry 3 of 3",
        ];
        Diagnostics.Assert("warning lines", string.Join(" | ", expected), string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(expected, log.MessagesAt(DiagnosticLogLevel.Warning));
        Diagnostics.Assert(
            "error line",
            "transfer failed with CouldntConnect (exit 7): Could not connect to server",
            log.MessagesAt(DiagnosticLogLevel.Error).Single());
        Assert.AreEqual(
            "transfer failed with CouldntConnect (exit 7): Could not connect to server",
            log.MessagesAt(DiagnosticLogLevel.Error).Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_RecordsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Error);

        await RunAsync(Channel(OptionAcknowledgement("blksize\0512\0"), Data(1, "hello")), Context(log), log);
        await RunAsync(Channel(([0, 5, 0, 1, .. "gone"u8, 0], TransferEndPoint)), Context(log), log);

        Diagnostics.Assert("recorded line count", 2, log.Lines.Count);
        Assert.HasCount(2, log.Lines);
        Diagnostics.Assert("every line is at error level", true, log.Lines.All(line => line.Level == DiagnosticLogLevel.Error));
        Assert.IsTrue(log.Lines.All(line => line.Level == DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_ThroughHttpProxy_SetsTheLogOnTheProxyTarget()
    {
        var log = new RecordingDiagnosticLog();
        var proxyConnector = new RecordingConnector(ConnectResult.Refused("Failed to connect"));
        Diagnostics.Arrange("url", "tftp://h/file.txt");
        Diagnostics.Arrange("proxy", "Http 127.0.0.1:18331");
        Diagnostics.Arrange("proxy connector", "refuses: Failed to connect");
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = new MemoryStream(),
            Proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18331, null),
            DiagnosticLog = log,
        };

        TransferResult result;
        using (Diagnostics.Phase("transfer"))
        {
            result = await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(Channel())), proxyConnector)
                .ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        LogLines(log);
        Diagnostics.Act("proxy targets", proxyConnector.Targets.Count);
        Diagnostics.Assert("target log is the context log", true, ReferenceEquals(log, proxyConnector.Targets.Single().DiagnosticLog));
        Assert.AreSame(log, proxyConnector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 4, 0, 7 }, "received ACK block 7, 4 bytes")]
    [DataRow(new byte[] { 0, 5, 0, 2 }, "received ERROR code 2, 4 bytes")]
    [DataRow(new byte[] { 0, 9, 0, 0 }, "received opcode 9, 4 bytes")]
    public void Received_Packet_NamesItsOpcode(byte[] packet, string expected)
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("packet", TftpTestDiagnostics.Describe(packet));
        TftpTestDiagnostics.Datagram(Diagnostics, "packet", packet);

        new TftpTransferLog(log).Received(packet);

        LogLines(log);
        Diagnostics.Assert("log message", expected, log.Lines.Count == 1 ? log.Lines[0].Message : "(" + log.Lines.Count + " lines)");
        Assert.AreEqual(expected, log.Lines.Single().Message);
    }

    [TestMethod]
    public void ErrorPacket_TextWithoutTerminator_LogsTheWholeText()
    {
        var log = new RecordingDiagnosticLog();
        byte[] packet = [0, 5, 0, 3, .. "full"u8];
        Diagnostics.Arrange("error packet without terminator", TftpTestDiagnostics.Describe(packet));
        TftpTestDiagnostics.Datagram(Diagnostics, "error packet without terminator", packet);

        new TftpTransferLog(log).ErrorPacket(packet);

        LogLines(log);
        Diagnostics.Assert("log message", "server sent TFTP error 3: full", log.Lines.Count == 1 ? log.Lines[0].Message : "(" + log.Lines.Count + " lines)");
        Assert.AreEqual("server sent TFTP error 3: full", log.Lines.Single().Message);
    }

    private static TftpProtocolHandler Handler(IDatagramChannel channel) =>
        new(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)));

    private async Task<TransferResult> RunAsync(ScriptedDatagramChannel channel, TransferContext context, RecordingDiagnosticLog log)
    {
        TransferResult result;
        using (Diagnostics.Phase("transfer"))
        {
            result = await Handler(channel).ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        TftpTestDiagnostics.Sent(Diagnostics, channel);
        LogLines(log);
        return result;
    }

    private void LogLines(RecordingDiagnosticLog log)
    {
        Diagnostics.Act("diagnostic log lines recorded", log.Lines.Count);
        for (int index = 0; index < Math.Min(log.Lines.Count, LoggedLineLimit); index++)
        {
            var (level, component, message) = log.Lines[index];
            Diagnostics.Act(
                string.Create(CultureInfo.InvariantCulture, $"log line {index}"),
                $"{level} {component}: {message}");
        }
    }

    private TransferContext Context(IDiagnosticLog log)
    {
        Diagnostics.Arrange("url", "tftp://h/file.txt");
        return new() { Url = CurlUrl.Parse("tftp://h/file.txt"), Output = new MemoryStream(), DiagnosticLog = log };
    }

    private ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script)
    {
        for (int index = 0; index < script.Length; index++)
        {
            TftpTestDiagnostics.Scripted(Diagnostics, index, script[index].Datagram, script[index].Source);
        }

        return new(ServerEndPoint, script);
    }

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);
}
