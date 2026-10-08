using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins what a <c>tftp://</c> download does when the server goes silent, repeats itself,
/// sends a datagram under four bytes or is joined by a stranger, against curl 8.21.0 measured on 2026-09-26 with loopback
/// UDP servers: when the last packet is re-sent, how often, and the exit code and message
/// the transfer ends with. Every wait runs on a <see cref="ManualTimeProvider" />, so the
/// times asserted are the whole seconds curl's rules give rather than its wall-clock
/// jitter.
/// </summary>
[TestClass]
public sealed class TftpDownloadRetransmissionTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    /// <summary>A port nothing in the transfer asked to hear from.</summary>
    private static readonly IPEndPoint StrangerEndPoint = new(IPAddress.Loopback, 50999);

    /// <summary>A datagram under four bytes, from the transfer endpoint.</summary>
    private static readonly (byte[] Datagram, EndPoint Source) TooShort = ([0, 3], TransferEndPoint);

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_SilentServerNoLimits_SendsReadRequest50Times7SecondsApartThenCouldntConnect()
    {
        var channel = Channel();
        var connector = new RecordingDatagramConnector(DatagramOpenResult.Opened(channel));

        var result = await RunThrough(connector, channel, Context());

        Diagnostics.Assert("connector opens", 1, connector.Opens.Count);
        Assert.HasCount(1, connector.Opens);
        AssertReadRequestsSent(channel, timeoutSeconds: 6, count: 50, intervalSeconds: 7);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(350), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(350), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.CouldntConnect, "Could not connect to server");
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerZeroLimits_TakesZeroAsNoLimit()
    {
        var channel = Channel();

        var result = await Run(channel, Context(connectTimeout: TimeSpan.Zero, maxTime: TimeSpan.Zero));

        AssertReadRequestsSent(channel, timeoutSeconds: 6, count: 50, intervalSeconds: 7);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerConnectTimeout10_SendsReadRequest3Times4SecondsApartThenCouldntConnectAt12()
    {
        var channel = Channel();

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertReadRequestsSent(channel, timeoutSeconds: 3, count: 3, intervalSeconds: 4);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(12), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(12), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.CouldntConnect, "Could not connect to server");
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterFirstBlockConnectTimeout10_ResendsAck3Times6SecondsApartThenTimesOutAt24()
    {
        var channel = Channel(Data(1, Payload(512, 'a')));

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 6, 12, 18);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(24), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(24), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Timeout was reached");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Timeout was reached", result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 512L, result.BytesTransferred);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterFirstBlockMaxTimeOverAnHour_ResendsAckOn15SecondScheduleThenTimesOut()
    {
        var channel = Channel(Data(1, Payload(512, 'a')));

        var result = await Run(channel, Context(maxTime: TimeSpan.FromHours(2)));

        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 6, 12, 18);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Timeout was reached");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Timeout was reached", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerMaxTime5_SendsReadRequestTimeout1At0And2And4ThenOperationTimedOutAt5()
    {
        var channel = Channel();

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertReadRequestsSent(channel, timeoutSeconds: 1, count: 3, intervalSeconds: 2);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Operation timed out after 5000 milliseconds with 0 bytes received");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    /// <summary>
    /// The runner's <c>-m</c> watchdog cancels the transfer's token at the instant <c>-m</c>
    /// passes; its timer, created first, fires first, and the handler still ends with its own
    /// message (ADR-0117, Decision 4).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task ExecuteAsync_SilentServerCancelledAtTheInstantMaxTimePasses_EndsWithItsOwnOperationTimedOut()
    {
        var channel = Channel();
        using var runnerWatchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5), clock);
        Diagnostics.Arrange("runner watchdog", "cancels the token at 5 s on the manual clock");

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5), cancellationToken: runnerWatchdog.Token));

        Diagnostics.Assert("clock", TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Operation timed out after 5000 milliseconds with 0 bytes received");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerCancelledBeforeMaxTimePasses_LetsTheCancellationOut()
    {
        var channel = Channel();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(4500), clock);
        Diagnostics.Arrange("cancellation", "token cancels at 4500 ms on the manual clock, before max time 5 s");

        var thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5), cancellationToken: cancellation.Token)));

        Diagnostics.Assert("thrown type", typeof(OperationCanceledException), thrown.GetType());
    }

    /// <summary>
    /// A <c>-L -m 2</c> chain whose first hop took 1.5 s and ends on a silent
    /// <c>tftp://</c> hop: curl 8.21.0 printed <c>Operation timed out after 2011
    /// milliseconds with 0 bytes received</c> (BL-350 Notes), counting from the first
    /// request.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task ExecuteAsync_SilentServerMaxTime2OperationStarted1500MillisecondsEarlier_OperationTimedOutAfter500MillisecondsReporting2000()
    {
        var channel = Channel();

        var result = await Run(
            channel,
            Context(maxTime: TimeSpan.FromSeconds(2), operationStarted: -TimeSpan.FromMilliseconds(1500).Ticks));

        Diagnostics.Assert("clock", TimeSpan.FromMilliseconds(500), clock.Now);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Operation timed out after 2000 milliseconds with 0 bytes received");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 2000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterFirstBlockMaxTime20_ResendsAckOnScheduleFromTimeLeftThenOperationTimedOutAt20()
    {
        var channel = Channel(Data(1, Payload(512, 'a')));

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(20)));

        Diagnostics.Assert("read request ends with timeout 5", true, Encoding.ASCII.GetString(channel.Sent[0].Datagram).EndsWith("timeout\u00005\u0000", StringComparison.Ordinal));
        Assert.IsTrue(Encoding.ASCII.GetString(channel.Sent[0].Datagram).EndsWith("timeout\u00005\u0000", StringComparison.Ordinal));
        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 6, 12, 18);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(20), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(20), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Operation timed out after 20000 milliseconds with 512 bytes received");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 20000 milliseconds with 512 bytes received", result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 512L, result.BytesTransferred);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_DuplicateBlock_AcksItAgainWithoutWritingItTwiceThenCompletes()
    {
        var first = Payload(512, 'a');
        var channel = Channel(Data(1, first), Data(1, first), Data(2, "end"));
        var output = new MemoryStream();

        var result = await Run(channel, Context(output: output));

        DiagnoseOutcome(result, CurlExitCode.Ok, null);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 515L, result.BytesTransferred);
        Assert.AreEqual(515, result.BytesTransferred);
        Diagnostics.Bytes("output written", output.ToArray());
        Diagnostics.Diff("output written", first + "end", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(first + "end", Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.Assert("acknowledged blocks", "1 1 2", string.Join(" ", AcknowledgedBlocks(channel)));
        CollectionAssert.AreEqual(new ushort[] { 1, 1, 2 }, AcknowledgedBlocks(channel));
        Diagnostics.Assert("every ack goes to the transfer endpoint", true, channel.Sent.Skip(1).All(sent => TransferEndPoint.Equals(sent.Destination)));
        Assert.IsTrue(channel.Sent.Skip(1).All(sent => TransferEndPoint.Equals(sent.Destination)));
    }

    [TestMethod]
    public async Task ExecuteAsync_DatagramFromUnknownEndPoint_SendsItNothingAndEndsWithRecvError()
    {
        var channel = Channel(Data(1, Payload(512, 'a')), (Data(2, "end").Datagram, StrangerEndPoint));

        var result = await Run(channel, Context());

        DiagnoseOutcome(result, CurlExitCode.RecvError, "Data received from another address");
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Data received from another address", result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 512L, result.BytesTransferred);
        Assert.AreEqual(512, result.BytesTransferred);
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        Diagnostics.Assert("acknowledged blocks", "1", string.Join(" ", AcknowledgedBlocks(channel)));
        CollectionAssert.AreEqual(new ushort[] { 1 }, AcknowledgedBlocks(channel));
        Diagnostics.Assert("anything sent to the stranger", false, channel.Sent.Any(sent => StrangerEndPoint.Equals(sent.Destination)));
        Assert.IsFalse(channel.Sent.Any(sent => StrangerEndPoint.Equals(sent.Destination)));
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortReplyToReadRequest_ResendsItAtOnceAndEndsWithItsMessage()
    {
        var channel = Channel(TooShort);

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertReadRequestsSentAt(channel, timeoutSeconds: 3, 0, 0, 4);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(8), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(8), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.CouldntConnect, "Received too short packet");
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ThreeTooShortRepliesToReadRequest_ResendsItTwiceThenCouldntConnectAtOnce()
    {
        var channel = Channel(TooShort, TooShort, TooShort);

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertReadRequestsSentAt(channel, timeoutSeconds: 3, 0, 0, 0);
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        DiagnoseOutcome(result, CurlExitCode.CouldntConnect, "Received too short packet");
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortReplyToReadRequestMaxTime5_RetriesRunOutAt4BeforeMaxTime()
    {
        var channel = Channel(TooShort);

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertReadRequestsSentAt(channel, timeoutSeconds: 1, 0, 0, 2);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(4), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(4), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.CouldntConnect, "Received too short packet");
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortReplyToReadRequestThenLastBlock_ResendsItToServerAndCompletes()
    {
        var channel = Channel(TooShort, Data(1, "hello"));
        var output = new MemoryStream();

        var result = await Run(channel, Context(output: output));

        DiagnoseOutcome(result, CurlExitCode.Ok, null);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Diagnostics.Bytes("output written", output.ToArray());
        Diagnostics.Diff("output written", "hello", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.Assert("datagrams sent", 3, channel.Sent.Count);
        Assert.HasCount(3, channel.Sent);
        Diagnostics.Diff("resent read request", ReadRequest(6), channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(ReadRequest(6), channel.Sent[1].Datagram);
        Diagnostics.Assert("resent read request destination", ServerEndPoint, channel.Sent[1].Destination);
        Assert.AreEqual(ServerEndPoint, channel.Sent[1].Destination);
        Diagnostics.Diff("final ack", new byte[] { 0, 4, 0, 1 }, channel.Sent[2].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, channel.Sent[2].Datagram);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramAfterFirstBlockThenSilence_ResendsAckAtOnceThenOnScheduleAndTimesOut()
    {
        var channel = Channel(Data(1, Payload(512, 'a')), TooShort);

        var result = await Run(channel, Context());

        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 0, 6, 12);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(18), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(18), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Received too short packet");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 512L, result.BytesTransferred);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_FourTooShortDatagramsAfterFirstBlock_ResendsAckThreeTimesThenTimesOutAtOnce()
    {
        var channel = Channel(Data(1, Payload(512, 'a')), TooShort, TooShort, TooShort, TooShort);

        var result = await Run(channel, Context());

        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 0, 0, 0);
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Received too short packet");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramAfterFirstBlockMaxTime5_OperationTimedOutAt5WithItsMessage()
    {
        var channel = Channel(Data(1, Payload(512, 'a')), TooShort);

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 0, 2, 4);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        DiagnoseOutcome(result, CurlExitCode.OperationTimedOut, "Received too short packet");
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramAfterFirstBlockThenNextBlock_AcksAgainAndCompletes()
    {
        var channel = Channel(Data(1, Payload(512, 'a')), TooShort, Data(2, "end"));

        var result = await Run(channel, Context());

        DiagnoseOutcome(result, CurlExitCode.Ok, null);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 515L, result.BytesTransferred);
        Assert.AreEqual(515, result.BytesTransferred);
        Diagnostics.Assert("acknowledged blocks", "1 1 2", string.Join(" ", AcknowledgedBlocks(channel)));
        CollectionAssert.AreEqual(new ushort[] { 1, 1, 2 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramThenErrorPacket_EndsWithTheErrorsCodeAndTheTooShortMessage()
    {
        var error = (new byte[] { 0, 5, 0, 1, (byte)'n', (byte)'f', 0 }, (EndPoint)TransferEndPoint);
        var channel = Channel(Data(1, Payload(512, 'a')), TooShort, error);

        var result = await Run(channel, Context());

        DiagnoseOutcome(result, CurlExitCode.TftpNotFound, "Received too short packet");
        Assert.AreEqual(CurlExitCode.TftpNotFound, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramThenStranger_EndsWithRecvErrorAndTheTooShortMessage()
    {
        var channel = Channel(Data(1, Payload(512, 'a')), TooShort, (Data(2, "end").Datagram, StrangerEndPoint));

        var result = await Run(channel, Context());

        DiagnoseOutcome(result, CurlExitCode.RecvError, "Received too short packet");
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
        Diagnostics.Assert("anything sent to the stranger", false, channel.Sent.Any(sent => StrangerEndPoint.Equals(sent.Destination)));
        Assert.IsFalse(channel.Sent.Any(sent => StrangerEndPoint.Equals(sent.Destination)));
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionRefused)]
    public async Task ExecuteAsync_EveryReceiveRefused_ReportsTooShortResendsAtOnceThenCouldntConnect(SocketError error)
    {
        Diagnostics.Arrange("refusal socket error", error);
        var refusal = FallsSilentDatagramChannel.Refusal(error);
        var channel = Channel(refusal, refusal, refusal);
        var events = new RecordingTransferEvents();

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10), events: events));

        AssertReadRequestsSentAt(channel, timeoutSeconds: 3, 0, 0, 0);
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        DiagnoseOutcome(result, CurlExitCode.CouldntConnect, "Received too short packet");
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
        Diagnostics.Assert("too short events", 3, events.Steps.Count(step => step == "Received too short packet"));
        Assert.AreEqual(3, events.Steps.Count(step => step == "Received too short packet"));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedReceiveThenLastBlock_ResendsReadRequestToServerAndCompletes()
    {
        Diagnostics.Arrange("refusal socket error", SocketError.ConnectionReset);
        var channel = Channel(FallsSilentDatagramChannel.Refusal(SocketError.ConnectionReset), Data(1, "hello"));
        var output = new MemoryStream();

        var result = await Run(channel, Context(output: output));

        DiagnoseOutcome(result, CurlExitCode.Ok, null);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Bytes("output written", output.ToArray());
        Diagnostics.Diff("output written", "hello", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.Assert("datagrams sent", 3, channel.Sent.Count);
        Assert.HasCount(3, channel.Sent);
        Diagnostics.Assert("second datagram destination", ServerEndPoint, channel.Sent[1].Destination);
        Assert.AreEqual(ServerEndPoint, channel.Sent[1].Destination);
        Diagnostics.Assert("third datagram destination", TransferEndPoint, channel.Sent[2].Destination);
        Assert.AreEqual(TransferEndPoint, channel.Sent[2].Destination);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReceiveFailsWithOtherSocketError_ThrowsSocketException()
    {
        Diagnostics.Arrange("refusal socket error", SocketError.NetworkDown);
        var channel = Channel(FallsSilentDatagramChannel.Refusal(SocketError.NetworkDown));

        var thrown = await Assert.ThrowsExactlyAsync<SocketException>(async () => await Run(channel, Context()));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("socket error code", SocketError.NetworkDown, thrown.SocketErrorCode);
        Assert.AreEqual(SocketError.NetworkDown, thrown.SocketErrorCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledWhileWaiting_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Diagnostics.Arrange("cancellation", "token already cancelled before the transfer");
        var channel = Channel();

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await Run(channel, Context(cancellationToken: cancellation.Token)));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("thrown is an OperationCanceledException", true, thrown is OperationCanceledException);
    }

    private static byte[] ReadRequest(int timeoutSeconds) =>
        [0, 1, .. Encoding.ASCII.GetBytes($"file.txt\0octet\0tsize\00\0blksize\0512\0timeout\0{timeoutSeconds}\0")];

    private static string Payload(int length, char fill) => new(fill, length);

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static string Times(IEnumerable<TimeSpan> times) =>
        string.Join(" ", times.Select(time => time.ToString()));

    /// <summary>
    /// The block numbers of every acknowledgement sent, skipping the read request.
    /// </summary>
    private static ushort[] AcknowledgedBlocks(FallsSilentDatagramChannel channel) =>
        [.. channel.Sent.Skip(1).Select(sent =>
        {
            CollectionAssert.AreEqual(new byte[] { 0, 4 }, sent.Datagram[..2]);
            return (ushort)((sent.Datagram[2] << 8) | sent.Datagram[3]);
        })];

    private async Task<TransferResult> Run(FallsSilentDatagramChannel channel, TransferContext context) =>
        await RunThrough(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)), channel, context);

    private async Task<TransferResult> RunThrough(
        RecordingDatagramConnector connector,
        FallsSilentDatagramChannel channel,
        TransferContext context)
    {
        Diagnostics.Arrange("url", "tftp://h/file.txt");
        Diagnostics.Arrange("connect timeout", context.ConnectTimeout?.ToString() ?? "(unset)");
        Diagnostics.Arrange("max time", context.MaxTime?.ToString() ?? "(unset)");
        Diagnostics.Arrange("operation started (ticks from now)", context.OperationStarted?.ToString(CultureInfo.InvariantCulture) ?? "(unset)");
        Diagnostics.Arrange("manual clock start", clock.Now);
        try
        {
            TransferResult result;
            using (Diagnostics.Phase("transfer"))
            {
                result = await new TftpProtocolHandler(connector).ExecuteAsync(context);
            }

            TftpTestDiagnostics.Result(Diagnostics, result);
            return result;
        }
        finally
        {
            Diagnostics.Act("datagrams sent", channel.Sent.Count);
            for (var index = 0; index < channel.Sent.Count; index++)
            {
                var (datagram, destination, at) = channel.Sent[index];
                TftpTestDiagnostics.Datagram(
                    Diagnostics,
                    string.Create(CultureInfo.InvariantCulture, $"sent {index} to {destination} at {at}"),
                    datagram);
            }

            Diagnostics.Act("manual clock end", clock.Now);
        }
    }

    private void DiagnoseOutcome(TransferResult result, CurlExitCode exitCode, string? errorMessage)
    {
        Diagnostics.Assert("exit code", exitCode, result.ExitCode);
        Diagnostics.Assert("error message", errorMessage ?? "(none)", result.ErrorMessage ?? "(none)");
    }

    /// <summary>
    /// Asserts the channel was sent exactly <paramref name="count" /> read requests, each
    /// byte-identical with <paramref name="timeoutSeconds" /> as its <c>timeout</c>, to the
    /// server endpoint, <paramref name="intervalSeconds" /> apart from t = 0.
    /// </summary>
    private void AssertReadRequestsSent(
        FallsSilentDatagramChannel channel,
        int timeoutSeconds,
        int count,
        int intervalSeconds)
    {
        Diagnostics.Assert("read requests sent", count, channel.Sent.Count);
        Assert.HasCount(count, channel.Sent);
        var expected = ReadRequest(timeoutSeconds);
        for (var index = 0; index < count; index++)
        {
            CollectionAssert.AreEqual(expected, channel.Sent[index].Datagram);
            Assert.AreEqual(ServerEndPoint, channel.Sent[index].Destination);
            Assert.AreEqual(TimeSpan.FromSeconds(index * intervalSeconds), channel.Sent[index].At);
        }

        Diagnostics.Diff("read request", expected, channel.Sent[count - 1].Datagram);
        Diagnostics.Assert("last read request sent at", TimeSpan.FromSeconds((count - 1) * intervalSeconds), channel.Sent[count - 1].At);
    }

    /// <summary>
    /// Asserts the channel was sent only read requests, each byte-identical with
    /// <paramref name="timeoutSeconds" /> as its <c>timeout</c>, to the server endpoint, at
    /// each of <paramref name="seconds" />.
    /// </summary>
    private void AssertReadRequestsSentAt(
        FallsSilentDatagramChannel channel,
        int timeoutSeconds,
        params int[] seconds)
    {
        Diagnostics.Assert(
            "send times",
            Times(seconds.Select(second => TimeSpan.FromSeconds(second))),
            Times(channel.Sent.Select(sent => sent.At)));
        CollectionAssert.AreEqual(
            seconds.Select(second => TimeSpan.FromSeconds(second)).ToArray(),
            channel.Sent.Select(sent => sent.At).ToArray());
        var expected = ReadRequest(timeoutSeconds);
        foreach (var (datagram, destination, _) in channel.Sent)
        {
            Diagnostics.Diff("read request", expected, datagram);
            CollectionAssert.AreEqual(expected, datagram);
            Diagnostics.Assert("read request destination", ServerEndPoint, destination);
            Assert.AreEqual(ServerEndPoint, destination);
        }
    }

    /// <summary>
    /// Asserts that after the read request at t = 0 the channel was sent only ACK 1, to the
    /// transfer endpoint, at each of <paramref name="seconds" />.
    /// </summary>
    private void AssertAcknowledgementsOfBlock1At(FallsSilentDatagramChannel channel, params int[] seconds)
    {
        Diagnostics.Assert(
            "send times",
            Times(seconds.Select(second => TimeSpan.FromSeconds(second))),
            Times(channel.Sent.Select(sent => sent.At)));
        CollectionAssert.AreEqual(
            seconds.Select(second => TimeSpan.FromSeconds(second)).ToArray(),
            channel.Sent.Select(sent => sent.At).ToArray());
        foreach (var (datagram, destination, _) in channel.Sent.Skip(1))
        {
            Diagnostics.Diff("ack of block 1", new byte[] { 0, 4, 0, 1 }, datagram);
            CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, datagram);
            Diagnostics.Assert("ack destination", TransferEndPoint, destination);
            Assert.AreEqual(TransferEndPoint, destination);
        }
    }

    private FallsSilentDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script)
    {
        if (script.Length == 0)
        {
            Diagnostics.Arrange("script", "silent server: no datagrams");
        }

        for (var index = 0; index < script.Length; index++)
        {
            TftpTestDiagnostics.Scripted(Diagnostics, index, script[index].Datagram, script[index].Source);
        }

        return new(ServerEndPoint, clock, script);
    }

    private TransferContext Context(
        TimeSpan? connectTimeout = null,
        TimeSpan? maxTime = null,
        Stream? output = null,
        CancellationToken cancellationToken = default,
        long? operationStarted = null,
        RecordingTransferEvents? events = null) =>
        new()
        {
            Events = events ?? new RecordingTransferEvents(),
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = output ?? new MemoryStream(),
            ConnectTimeout = connectTimeout,
            MaxTime = maxTime,
            OperationStarted = operationStarted,
            TimeProvider = clock,
            CancellationToken = cancellationToken,
        };
}
