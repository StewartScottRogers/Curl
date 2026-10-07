using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

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

    private const int LoggedItemLimit = 8;

    private const int LoggedStepLimit = 40;

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsCurlsLinesAndTheBlockReceived()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(
            clock,
            OptionAcknowledgement("tsize\06\0blksize\0512\0timeout\06\0"),
            Data(1, "hello\n"));

        var result = await RunAsync(Handler(channel), channel, Context(events, clock), events);

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        string[] expected =
        [
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
        ];
        Diagnostics.Assert("event steps", Show(expected), Show(events.Steps));
        CollectionAssert.AreEqual(expected, events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWithBlockSize1024_ReportsTheBlockSizeRequestedAndParsed()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(
            clock,
            OptionAcknowledgement("tsize\06\0blksize\01024\0timeout\06\0"),
            Data(1, "hello\n"));

        await RunAsync(Handler(channel), channel, Context(events, clock, blockSize: 1024), events);

        string[] expected =
        [
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
        ];
        Diagnostics.Assert("event steps", Show(expected), Show(events.Steps));
        CollectionAssert.AreEqual(expected, events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWithNoOptions_ReportsTheBlockBeforeConnectedForReceive()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(clock, Data(1, "hello\n"));

        await RunAsync(Handler(channel), channel, Context(events, clock, noOptions: true), events);

        string[] expected =
        [
            Trying,
            Established,
            StartTimeouts,
            "<= hello\n",
            "Connected for receive",
            "set timeouts for state 1; Total 0, retry 5 maxtry 3",
            ShuttingDown,
        ];
        Diagnostics.Assert("event steps", Show(expected), Show(events.Steps));
        CollectionAssert.AreEqual(expected, events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsCurlsLinesAndNoDataBlock()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(
            clock,
            OptionAcknowledgement("tsize\09\0blksize\0512\0timeout\06\0"),
            Acknowledgement(1));

        var result = await RunAsync(Handler(channel), channel, UploadContext(events, clock), events);

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        string[] expected =
        [
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
        ];
        Diagnostics.Assert("event steps", Show(expected), Show(events.Steps));
        CollectionAssert.AreEqual(expected, events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorPacket_ReportsTheServersTextAndExits68()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(
            clock,
            ([0, 5, 0, 1, .. "File not found"u8, 0], TransferEndPoint));

        var result = await RunAsync(Handler(channel), channel, Context(events, clock), events);

        Diagnostics.Assert("exit code", CurlExitCode.TftpNotFound, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpNotFound, result.ExitCode);
        string[] expected = [Trying, Established, StartTimeouts, "TFTP error: File not found", ShuttingDown];
        Diagnostics.Assert("event steps", Show(expected), Show(events.Steps));
        CollectionAssert.AreEqual(expected, events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadErrorPacket_ReportsTheServersText()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(
            clock,
            ([0, 5, 0, 2, .. "Access violation"u8, 0], TransferEndPoint));

        await RunAsync(Handler(channel), channel, UploadContext(events, clock), events);

        string[] expected = [Trying, Established, StartTimeouts, "TFTP error: Access violation", ShuttingDown];
        Diagnostics.Assert("event steps", Show(expected), Show(events.Steps));
        CollectionAssert.AreEqual(expected, events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWhoseBlockOneIsLate_ReportsTheTimeoutBeforeTheBlock()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(
            clock,
            OptionAcknowledgement("tsize\02\0blksize\0512\0timeout\06\0"),
            (null, TransferEndPoint),
            Data(1, "a\n"));

        await RunAsync(Handler(channel), channel, Context(events, clock), events);

        string[] expected =
        [
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
        ];
        Diagnostics.Assert("event steps", Show(expected), Show(events.Steps));
        CollectionAssert.AreEqual(expected, events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWhoseAckIsLate_ReportsTheTimeoutForTheNextBlock()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(
            clock,
            OptionAcknowledgement("tsize\09\0blksize\0512\0timeout\06\0"),
            (null, TransferEndPoint),
            Acknowledgement(1));

        await RunAsync(Handler(channel), channel, UploadContext(events, clock), events);

        string[] expected =
        [
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
        ];
        Diagnostics.Assert("event steps", Show(expected), Show(events.Steps));
        CollectionAssert.AreEqual(expected, events.Steps);
    }

    /// <summary>
    /// Pinned from curl 8.21.0's <c>tftp_tx</c> (lib/tftp.c lines 371-393): an ACK for a
    /// block other than the one last sent is reported, and the last DATA packet is re-sent.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWhoseDataOneIsAnsweredWithAckZero_ReportsTheUnexpectedAckAndResendsDataOne()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(
            clock,
            Acknowledgement(0),
            Acknowledgement(0),
            Acknowledgement(1));

        var result = await RunAsync(Handler(channel), channel, UploadContext(events, clock, noOptions: true), events);

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        Diagnostics.Assert("bytes transferred", 9, result.BytesTransferred);
        Assert.AreEqual(9, result.BytesTransferred);
        string[] expected = ["Connected for transmit", "set timeouts for state 2; Total 0, retry 5 maxtry 3", "Received ACK for block 0, expecting 1", ShuttingDown];
        Diagnostics.Assert("event steps after the first 3", Show(expected), Show(events.Steps.Skip(3)));
        CollectionAssert.AreEqual(expected, events.Steps.Skip(3).ToArray());
        byte[] dataOne = [0, 3, 0, 1, .. "upload me"u8.ToArray()];
        TftpTestDiagnostics.Datagram(Diagnostics, "expected data one", dataOne);
        Diagnostics.Assert("datagrams sent", 3, channel.Sent.Count);
        Assert.HasCount(3, channel.Sent);
        Diagnostics.Diff("sent 1", dataOne, channel.Sent[1]);
        CollectionAssert.AreEqual(dataOne, channel.Sent[1]);
        Diagnostics.Diff("sent 2", dataOne, channel.Sent[2]);
        CollectionAssert.AreEqual(dataOne, channel.Sent[2]);
    }

    /// <summary>
    /// Pinned from curl 8.21.0's <c>tftp_rx</c> (lib/tftp.c lines 532-549): a repeat of the
    /// last block received is reported, with its full stop, and acknowledged again.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DownloadWhoseDataOneArrivesTwice_ReportsTheRepeatOnceAndAcksItTwice()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var first = new string('a', 512);
        var channel = Pausing(clock, Data(1, first), Data(1, first), Data(2, "end"));
        var context = Context(events, clock, noOptions: true);

        var result = await RunAsync(Handler(channel), channel, context, events);

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        string written = Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray());
        Diagnostics.Act("output length", written.Length);
        Diagnostics.Assert("output", first + "end", written);
        Assert.AreEqual(first + "end", written);
        int repeats = events.Steps.Count(step => step == "Received last DATA packet block 1 again.");
        Diagnostics.Assert("repeat reports", 1, repeats);
        Assert.AreEqual(1, repeats);
        string[] expected = ["Received last DATA packet block 1 again.", "<= end", ShuttingDown];
        Diagnostics.Assert("last 3 event steps", Show(expected), Show(events.Steps.TakeLast(3)));
        CollectionAssert.AreEqual(expected, events.Steps.TakeLast(3).ToArray());
        int acksForBlockOne = channel.Sent.Count(sent => sent.SequenceEqual(new byte[] { 0, 4, 0, 1 }));
        Diagnostics.Assert("ACK block 1 sent", 2, acksForBlockOne);
        Assert.AreEqual(2, acksForBlockOne);
    }

    /// <summary>
    /// Pinned from curl 8.21.0's <c>tftp_rx</c> (lib/tftp.c lines 532-549): a block that is
    /// neither the next nor the last is reported and otherwise ignored.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DownloadWhoseDataThreeArrivesAfterDataOne_ReportsItAcksNothingForItAndCompletesOnDataTwo()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var first = new string('a', 512);
        var channel = Pausing(clock, Data(1, first), Data(3, "x"), Data(2, "end"));
        var context = Context(events, clock, noOptions: true);

        var result = await RunAsync(Handler(channel), channel, context, events);

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        string written = Encoding.ASCII.GetString(((MemoryStream)context.Output).ToArray());
        Diagnostics.Act("output length", written.Length);
        Diagnostics.Assert("output", first + "end", written);
        Assert.AreEqual(first + "end", written);
        string[] expected = ["Received unexpected DATA packet block 3, expecting block 2", "<= end", ShuttingDown];
        Diagnostics.Assert("last 3 event steps", Show(expected), Show(events.Steps.TakeLast(3)));
        CollectionAssert.AreEqual(expected, events.Steps.TakeLast(3).ToArray());
        Diagnostics.Assert("datagrams sent", 3, channel.Sent.Count);
        Assert.HasCount(3, channel.Sent);
        Diagnostics.Diff("sent 1", new byte[] { 0, 4, 0, 1 }, channel.Sent[1]);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[1]);
        Diagnostics.Diff("sent 2", new byte[] { 0, 4, 0, 2 }, channel.Sent[2]);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 2 }, channel.Sent[2]);
    }

    /// <summary>
    /// Block numbers print as curl's unsigned 16-bit values: after block 65535 the next
    /// block expected is 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DownloadWhoseBlockOneArrivesAfterBlock65535_ReportsExpectingBlockZero()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var script = new List<(byte[]? Datagram, EndPoint Source)> { OptionAcknowledgement("blksize\08\0") };
        for (var block = 1; block <= ushort.MaxValue; block++)
        {
            script.Add(Data((ushort)block, "abcdefgh"));
        }

        script.Add(Data(1, "abcdefgh"));
        script.Add(Data(0, "end"));
        var channel = Pausing(clock, [.. script]);

        var result = await RunAsync(Handler(channel), channel, Context(events, clock, blockSize: 8), events);

        Diagnostics.Assert("success", true, result.IsSuccess);
        Assert.IsTrue(result.IsSuccess);
        string[] expected = ["<= abcdefgh", "Received unexpected DATA packet block 1, expecting block 0", "<= end", ShuttingDown];
        Diagnostics.Assert("last 4 event steps", Show(expected), Show(events.Steps.TakeLast(4)));
        CollectionAssert.AreEqual(expected, events.Steps.TakeLast(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTime_ReportsTheMillisecondsLeftAsTotal()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(clock, Data(1, "a"));

        await RunAsync(Handler(channel), channel, Context(events, clock, noOptions: true, maxTime: TimeSpan.FromSeconds(20)), events);

        const string StateZero = "set timeouts for state 0; Total 20000, retry 5 maxtry 4";
        const string StateOne = "set timeouts for state 1; Total 20000, retry 5 maxtry 4";
        Diagnostics.Assert("state 0 step reported", true, events.Steps.Contains(StateZero));
        CollectionAssert.Contains(events.Steps, StateZero);
        Diagnostics.Assert("state 1 step reported", true, events.Steps.Contains(StateOne));
        CollectionAssert.Contains(events.Steps, StateOne);
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionAcknowledgementToARequestWithoutOptions_ReportsTheDefaultBlockSizeAsRequested()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var download = Pausing(clock, OptionAcknowledgement("blksize\0512\0"), Data(1, "a"));
        var upload = Pausing(clock, OptionAcknowledgement("blksize\0512\0"), Acknowledgement(1));

        await RunAsync(Handler(download), download, Context(events, clock, noOptions: true), events);
        await RunAsync(Handler(upload), upload, UploadContext(events, clock, noOptions: true), events);

        int reports = events.Steps.Count(step => step == "blksize parsed from OACK (512) requested (512)");
        Diagnostics.Assert("blksize reports across both transfers", 2, reports);
        Assert.AreEqual(2, reports);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChannelThatWillNotOpen_ReportsNothing()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("connector", "fails to open with CouldntResolveHost: Could not resolve host: h");
        var connector = new RecordingDatagramConnector(DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: h"));
        var context = Context(events, new ManualTimeProvider());

        TransferResult result;
        using (Diagnostics.Phase("transfer"))
        {
            result = await new TftpProtocolHandler(connector).ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        Diagnostics.Act("event steps", Show(events.Steps));
        Diagnostics.Assert("event step count", 0, events.Steps.Count);
        Assert.IsEmpty(events.Steps);
    }

    [TestMethod]
    public void Connected_EndPointThatIsNotAnAddress_NamesItAsItIs()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("host", "h");
        Diagnostics.Arrange("end point", "DnsEndPoint h:69");

        new TftpTransferEvents(events).Connected("h", new DnsEndPoint("h", 69));

        Diagnostics.Act("event steps", Show(events.Steps));
        Diagnostics.Assert("step 1", "Established connection to h (Unspecified/h:69) from  port 0 ", events.Steps[1]);
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
        Diagnostics.Arrange("requested block size", 512);
        TftpTestDiagnostics.Datagram(Diagnostics, "option acknowledgement fields", Encoding.ASCII.GetBytes(options));
        var acknowledgement = TftpOptionAcknowledgement.Parse(Encoding.ASCII.GetBytes(options), 512, isDownload: true);

        new TftpTransferEvents(events).OptionsAcknowledged(acknowledgement.Options, 512);

        Diagnostics.Act("event steps", Show(events.Steps));
        Diagnostics.Assert("only step", expected, events.Steps.Count == 1 ? events.Steps[0] : Show(events.Steps));
        Assert.AreEqual(expected, events.Steps.Single());
    }

    [TestMethod]
    public void ErrorPacket_TextWithoutTerminator_ReportsNothing()
    {
        var events = new RecordingTransferEvents();
        byte[] packet = [0, 5, 0, 1, .. "gone"u8];
        TftpTestDiagnostics.Datagram(Diagnostics, "error packet without terminator", packet);
        Diagnostics.Arrange("terminator", "none");

        new TftpTransferEvents(events).ErrorPacket(packet);

        Diagnostics.Act("event steps", Show(events.Steps));
        Diagnostics.Assert("event step count", 0, events.Steps.Count);
        Assert.IsEmpty(events.Steps);
    }

    [TestMethod]
    public void DataReceived_EmptyBlock_ReportsNothing()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("block", "empty");

        new TftpTransferEvents(events).DataReceived([]);

        Diagnostics.Act("event steps", Show(events.Steps));
        Diagnostics.Assert("event step count", 0, events.Steps.Count);
        Assert.IsEmpty(events.Steps);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnAChannelOpenedAsConnection2_ShutsDownConnection2()
    {
        var events = new RecordingTransferEvents();
        var clock = new ManualTimeProvider();
        var channel = Pausing(clock, Data(1, "hello\n"));
        Diagnostics.Arrange("connection number", 2);
        var handler = new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel).WithConnectionNumber(2)));

        await RunAsync(handler, channel, Context(events, clock, noOptions: true), events);

        Diagnostics.Assert("last step", "shutting down connection #2", events.Steps[^1]);
        Assert.AreEqual("shutting down connection #2", events.Steps[^1]);
        Diagnostics.Assert("connection 0 shutdown reported", false, events.Steps.Contains(ShuttingDown));
        CollectionAssert.DoesNotContain(events.Steps, ShuttingDown);
    }

    private static TftpProtocolHandler Handler(IDatagramChannel channel) =>
        new(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)));

    private static string Show(IEnumerable<string> steps) =>
        string.Join(" | ", steps).Replace("\n", "\\n", StringComparison.Ordinal);

    private PausingDatagramChannel Pausing(ManualTimeProvider clock, params (byte[]? Datagram, EndPoint Source)[] script)
    {
        Diagnostics.Arrange("clock", "ManualTimeProvider, advanced only by scripted silences");
        Diagnostics.Arrange("scripted entries", script.Length);
        for (int index = 0; index < Math.Min(script.Length, LoggedItemLimit); index++)
        {
            var (datagram, source) = script[index];
            if (datagram is null)
            {
                Diagnostics.Arrange(
                    string.Create(CultureInfo.InvariantCulture, $"scripted {index} from {source}"),
                    "silence: the clock advances to the next timer");
            }
            else
            {
                TftpTestDiagnostics.Scripted(Diagnostics, index, datagram, source);
            }
        }

        return new PausingDatagramChannel(ServerEndPoint, clock, script);
    }

    private async Task<TransferResult> RunAsync(
        TftpProtocolHandler handler,
        PausingDatagramChannel channel,
        TransferContext context,
        RecordingTransferEvents events)
    {
        TransferResult result;
        using (Diagnostics.Phase("transfer"))
        {
            result = await handler.ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        Diagnostics.Act("datagrams sent", channel.Sent.Count);
        for (int index = 0; index < Math.Min(channel.Sent.Count, LoggedItemLimit); index++)
        {
            TftpTestDiagnostics.Datagram(Diagnostics, string.Create(CultureInfo.InvariantCulture, $"sent {index}"), channel.Sent[index]);
        }

        if (context.TimeProvider is ManualTimeProvider clock)
        {
            Diagnostics.Act("clock after the transfer (ms)", clock.Now.TotalMilliseconds);
        }

        Diagnostics.Act("event steps reported", events.Steps.Count);
        int firstLogged = Math.Max(0, events.Steps.Count - LoggedStepLimit);
        for (int index = firstLogged; index < events.Steps.Count; index++)
        {
            Diagnostics.Act(
                string.Create(CultureInfo.InvariantCulture, $"event step {index}"),
                Show([events.Steps[index]]));
        }

        return result;
    }

    private TransferContext Context(
        ITransferEvents events,
        TimeProvider clock,
        int? blockSize = null,
        bool noOptions = false,
        TimeSpan? maxTime = null,
        Stream? upload = null)
    {
        Diagnostics.Arrange("url", "tftp://127.0.0.1/file.txt");
        Diagnostics.Arrange("block size", blockSize?.ToString(CultureInfo.InvariantCulture) ?? "(default)");
        Diagnostics.Arrange("no options", noOptions);
        Diagnostics.Arrange("max time", maxTime?.ToString() ?? "(none)");
        Diagnostics.Arrange("upload bytes", (upload as MemoryStream)?.Length.ToString(CultureInfo.InvariantCulture) ?? "(not an upload)");
        return new()
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
    }

    private TransferContext UploadContext(ITransferEvents events, TimeProvider clock, bool noOptions = false) =>
        Context(events, clock, noOptions: noOptions, upload: new MemoryStream("upload me"u8.ToArray()));

    private static (byte[]? Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static (byte[]? Datagram, EndPoint Source) Acknowledgement(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    private static (byte[]? Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);
}
