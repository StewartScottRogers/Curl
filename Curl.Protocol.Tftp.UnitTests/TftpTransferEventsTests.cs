using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins the <c>-v</c> info lines and <c>--trace</c> data blocks a <c>tftp://</c> transfer
/// reports to <see cref="ITransferEvents" />, in curl 8.21.0's order and words (measured by
/// BL-933 with <c>Record-CurlExchange.ps1 -Tftp</c>). The clock is frozen, so the
/// <c>Total</c> of state 0 is the whole 300000 ms default connect timeout, where the
/// measured runs showed 299998-300000.
/// </summary>
[TestClass]
public sealed class TftpTransferEventsTests
{
    private const string Trying = "  Trying 127.0.0.1:69...";

    private const string Established = "Established connection to 127.0.0.1 (127.0.0.1 port 69) from  port 0 ";

    private const string StartTimeouts = "set timeouts for state 0; Total 300000, retry 6 maxtry 50";

    private const string ShuttingDown = "shutting down connection #0";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsCurlsLinesAndTheBlockReceived()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(
            ServerEndPoint,
            clock,
            OptionAcknowledgement("tsize\06\0blksize\0512\0timeout\06\0"),
            Data(1, "hello\n"));

        var result = await Handler(channel).ExecuteAsync(Context(events, clock));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[]
            {
                Trying,
                Established,
                StartTimeouts,
                "got option=(tsize) value=(6)",
                "tsize parsed from OACK (6)",
                "got option=(blksize) value=(512)",
                "blksize parsed from OACK (512) requested (512)",
                "got option=(timeout) value=(6)",
                "Connected for receive",
                "set timeouts for state 1; Total 0, retry 5 maxtry 3",
                "<= hello\n",
                ShuttingDown,
            },
            events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWithBlockSize1024_ReportsTheBlockSizeRequestedAndParsed()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(
            ServerEndPoint,
            clock,
            OptionAcknowledgement("tsize\06\0blksize\01024\0timeout\06\0"),
            Data(1, "hello\n"));

        await Handler(channel).ExecuteAsync(Context(events, clock, blockSize: 1024));

        CollectionAssert.AreEqual(
            new[]
            {
                Trying,
                Established,
                StartTimeouts,
                "got option=(tsize) value=(6)",
                "tsize parsed from OACK (6)",
                "got option=(blksize) value=(1024)",
                "blksize parsed from OACK (1024) requested (1024)",
                "got option=(timeout) value=(6)",
                "Connected for receive",
                "set timeouts for state 1; Total 0, retry 5 maxtry 3",
                "<= hello\n",
                ShuttingDown,
            },
            events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWithNoOptions_ReportsTheBlockBeforeConnectedForReceive()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Data(1, "hello\n"));

        await Handler(channel).ExecuteAsync(Context(events, clock, noOptions: true));

        CollectionAssert.AreEqual(
            new[]
            {
                Trying,
                Established,
                StartTimeouts,
                "<= hello\n",
                "Connected for receive",
                "set timeouts for state 1; Total 0, retry 5 maxtry 3",
                ShuttingDown,
            },
            events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsCurlsLinesAndNoDataBlock()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(
            ServerEndPoint,
            clock,
            OptionAcknowledgement("tsize\09\0blksize\0512\0timeout\06\0"),
            Acknowledgement(1));

        var result = await Handler(channel).ExecuteAsync(UploadContext(events, clock));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[]
            {
                Trying,
                Established,
                StartTimeouts,
                "got option=(tsize) value=(9)",
                "got option=(blksize) value=(512)",
                "blksize parsed from OACK (512) requested (512)",
                "got option=(timeout) value=(6)",
                "Connected for transmit",
                "set timeouts for state 2; Total 0, retry 5 maxtry 3",
                ShuttingDown,
            },
            events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorPacket_ReportsTheServersTextAndExits68()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(
            ServerEndPoint,
            clock,
            ([0, 5, 0, 1, .. "File not found"u8, 0], TransferEndPoint));

        var result = await Handler(channel).ExecuteAsync(Context(events, clock));

        Assert.AreEqual(CurlExitCode.TftpNotFound, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { Trying, Established, StartTimeouts, "TFTP error: File not found", ShuttingDown },
            events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadErrorPacket_ReportsTheServersText()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(
            ServerEndPoint,
            clock,
            ([0, 5, 0, 2, .. "Access violation"u8, 0], TransferEndPoint));

        await Handler(channel).ExecuteAsync(UploadContext(events, clock));

        CollectionAssert.AreEqual(
            new[] { Trying, Established, StartTimeouts, "TFTP error: Access violation", ShuttingDown },
            events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWhoseBlockOneIsLate_ReportsTheTimeoutBeforeTheBlock()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(
            ServerEndPoint,
            clock,
            OptionAcknowledgement("tsize\02\0blksize\0512\0timeout\06\0"),
            (null, TransferEndPoint),
            Data(1, "a\n"));

        await Handler(channel).ExecuteAsync(Context(events, clock));

        CollectionAssert.AreEqual(
            new[]
            {
                Trying,
                Established,
                StartTimeouts,
                "got option=(tsize) value=(2)",
                "tsize parsed from OACK (2)",
                "got option=(blksize) value=(512)",
                "blksize parsed from OACK (512) requested (512)",
                "got option=(timeout) value=(6)",
                "Connected for receive",
                "set timeouts for state 1; Total 0, retry 5 maxtry 3",
                "Timeout waiting for block 1 ACK. Retries = 1",
                "<= a\n",
                ShuttingDown,
            },
            events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWhoseAckIsLate_ReportsTheTimeoutForTheNextBlock()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(
            ServerEndPoint,
            clock,
            OptionAcknowledgement("tsize\09\0blksize\0512\0timeout\06\0"),
            (null, TransferEndPoint),
            Acknowledgement(1));

        await Handler(channel).ExecuteAsync(UploadContext(events, clock));

        CollectionAssert.AreEqual(
            new[]
            {
                Trying,
                Established,
                StartTimeouts,
                "got option=(tsize) value=(9)",
                "got option=(blksize) value=(512)",
                "blksize parsed from OACK (512) requested (512)",
                "got option=(timeout) value=(6)",
                "Connected for transmit",
                "set timeouts for state 2; Total 0, retry 5 maxtry 3",
                "Timeout waiting for block 2 ACK. Retries = 1",
                ShuttingDown,
            },
            events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTime_ReportsTheMillisecondsLeftAsTotal()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Data(1, "a"));

        await Handler(channel).ExecuteAsync(Context(events, clock, noOptions: true, maxTime: TimeSpan.FromSeconds(20)));

        CollectionAssert.Contains(events.Steps, "set timeouts for state 0; Total 20000, retry 5 maxtry 4");
        CollectionAssert.Contains(events.Steps, "set timeouts for state 1; Total 20000, retry 5 maxtry 4");
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionAcknowledgementToARequestWithoutOptions_ReportsTheDefaultBlockSizeAsRequested()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var download = new PausingDatagramChannel(ServerEndPoint, clock, OptionAcknowledgement("blksize\0512\0"), Data(1, "a"));
        var upload = new PausingDatagramChannel(ServerEndPoint, clock, OptionAcknowledgement("blksize\0512\0"), Acknowledgement(1));

        await Handler(download).ExecuteAsync(Context(events, clock, noOptions: true));
        await Handler(upload).ExecuteAsync(UploadContext(events, clock, noOptions: true));

        Assert.AreEqual(2, events.Steps.Count(step => step == "blksize parsed from OACK (512) requested (512)"));
    }

    [TestMethod]
    public async Task ExecuteAsync_ChannelThatWillNotOpen_ReportsNothing()
    {
        var events = new RecordingTransferEvents();
        var connector = new RecordingDatagramConnector(DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: h"));

        await new TftpProtocolHandler(connector).ExecuteAsync(Context(events, new ManualTimeProvider()));

        Assert.IsEmpty(events.Steps);
    }

    [TestMethod]
    public void Connected_EndPointThatIsNotAnAddress_NamesItAsItIs()
    {
        var events = new RecordingTransferEvents();

        new TftpTransferEvents(events).Connected("h", new DnsEndPoint("h", 69));

        Assert.AreEqual("Established connection to h (Unspecified/h:69) from  port 0 ", events.Steps[1]);
    }

    [TestMethod]
    [DataRow("blksize\0big\0", "got option=(blksize) value=(big)")]
    [DataRow("blksize\04\0", "got option=(blksize) value=(4)")]
    [DataRow("tsize\0many\0", "got option=(tsize) value=(many)")]
    [DataRow("blksize\04\0timeout\06\0", "got option=(blksize) value=(4)", DisplayName = "options after a rejected one are not reported")]
    public void OptionsAcknowledged_ValueThatDoesNotParse_ReportsOnlyTheOption(string options, string expected)
    {
        var events = new RecordingTransferEvents();
        var acknowledgement = TftpOptionAcknowledgement.Parse(Encoding.ASCII.GetBytes(options), 512, isDownload: true);

        new TftpTransferEvents(events).OptionsAcknowledged(acknowledgement.Options, 512);

        Assert.AreEqual(expected, events.Steps.Single());
    }

    [TestMethod]
    public void ErrorPacket_TextWithoutTerminator_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        new TftpTransferEvents(events).ErrorPacket([0, 5, 0, 1, .. "gone"u8]);

        Assert.IsEmpty(events.Steps);
    }

    [TestMethod]
    public void DataReceived_EmptyBlock_ReportsNothing()
    {
        var events = new RecordingTransferEvents();

        new TftpTransferEvents(events).DataReceived([]);

        Assert.IsEmpty(events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnAChannelOpenedAsConnection2_ShutsDownConnection2()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = new PausingDatagramChannel(ServerEndPoint, clock, Data(1, "hello\n"));
        var handler = new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel).WithConnectionNumber(2)));

        await handler.ExecuteAsync(Context(events, clock, noOptions: true));

        Assert.AreEqual("shutting down connection #2", events.Steps[^1]);
        CollectionAssert.DoesNotContain(events.Steps, ShuttingDown);
    }

    private static TftpProtocolHandler Handler(IDatagramChannel channel) =>
        new(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)));

    private static TransferContext Context(
        ITransferEvents events,
        TimeProvider clock,
        int? blockSize = null,
        bool noOptions = false,
        TimeSpan? maxTime = null,
        Stream? upload = null) =>
        new()
        {
            Url = CurlUrl.Parse("tftp://127.0.0.1/file.txt"),
            Output = new MemoryStream(),
            Events = events,
            TimeProvider = clock,
            TftpBlockSize = blockSize,
            TftpNoOptions = noOptions,
            MaxTime = maxTime,
            Upload = upload,
        };

    private static TransferContext UploadContext(ITransferEvents events, TimeProvider clock, bool noOptions = false) =>
        Context(events, clock, noOptions: noOptions, upload: new MemoryStream("upload me"u8.ToArray()));

    private static (byte[]? Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[]? Datagram, EndPoint Source) Acknowledgement(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    private static (byte[]? Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);
}
