using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins what a <c>tftp://</c> upload (<c>-T</c>) does when the server goes silent, answers
/// with the wrong block or a datagram too short to read, or is joined by a stranger,
/// against curl 8.21.0 measured on 2026-09-26 with loopback UDP servers and a 1000-byte
/// upload: the write request's <c>timeout</c>, which packet is re-sent and when, and the
/// exit code and message the upload ends with. Every wait runs on a
/// <see cref="ManualTimeProvider" />, so the times asserted are the whole seconds curl's
/// rules give rather than its wall-clock jitter.
/// </summary>
[TestClass]
public sealed class TftpUploadRetransmissionTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    /// <summary>A port nothing in the transfer asked to hear from.</summary>
    private static readonly IPEndPoint StrangerEndPoint = new(IPAddress.Loopback, 50999);

    private static readonly byte[] Upload = [.. Enumerable.Range(0, 1000).Select(index => (byte)index)];

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_SilentServerNoLimits_SendsWriteRequest50Times7SecondsApartThenCouldntConnect()
    {
        var channel = Channel();

        var result = await Run(channel, Context());

        AssertWriteRequestsSent(channel, timeoutSeconds: 6, count: 50, intervalSeconds: 7);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(350), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(350), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", "Could not connect to server", result.ErrorMessage);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerConnectTimeout10_SendsWriteRequest3Times4SecondsApartThenCouldntConnectAt12()
    {
        var channel = Channel();

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertWriteRequestsSent(channel, timeoutSeconds: 3, count: 3, intervalSeconds: 4);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(12), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(12), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", "Could not connect to server", result.ErrorMessage);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerMaxTime5_SendsWriteRequestTimeout1At0And2And4ThenOperationTimedOutAt5()
    {
        var channel = Channel();

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertWriteRequestsSent(channel, timeoutSeconds: 1, count: 3, intervalSeconds: 2);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
        Assert.AreEqual("Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerMaxTime2OperationStarted1500MillisecondsEarlier_OperationTimedOutAfter500MillisecondsReporting2000()
    {
        var channel = Channel();

        var result = await Run(
            channel,
            Context(maxTime: TimeSpan.FromSeconds(2), operationStarted: -TimeSpan.FromMilliseconds(1500).Ticks));

        Diagnostics.Assert("clock", TimeSpan.FromMilliseconds(500), clock.Now);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "Operation timed out after 2000 milliseconds with 0 bytes received", result.ErrorMessage);
        Assert.AreEqual("Operation timed out after 2000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterAck0NoLimits_ResendsData1At6And12And18ThenTimesOutAt24()
    {
        var channel = Channel(Ack(0));

        var result = await Run(channel, Context());

        AssertWriteRequestTimeout(channel, 6);
        AssertData1SentAt(channel, 0, 6, 12, 18);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(24), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(24), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "Timeout was reached", result.ErrorMessage);
        Assert.AreEqual("Timeout was reached", result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 512, result.BytesTransferred);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterAck0ConnectTimeout10_SendsTimeout3ThenResendsData1On15SecondSchedule()
    {
        var channel = Channel(Ack(0));

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertWriteRequestTimeout(channel, 3);
        AssertData1SentAt(channel, 0, 6, 12, 18);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(24), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(24), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "Timeout was reached", result.ErrorMessage);
        Assert.AreEqual("Timeout was reached", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterAck0MaxTime5_ResendsData1At2And4ThenOperationTimedOutWith0BytesReceived()
    {
        var channel = Channel(Ack(0));

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertWriteRequestTimeout(channel, 1);
        AssertData1SentAt(channel, 0, 2, 4);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
        Assert.AreEqual("Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 512, result.BytesTransferred);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_AckFromUnknownEndPoint_SendsItNothingAndEndsWithRecvError()
    {
        var channel = Channel(Ack(0), (Ack(1).Datagram, StrangerEndPoint));

        var result = await Run(channel, Context());

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Assert("error message", "Data received from another address", result.ErrorMessage);
        Assert.AreEqual("Data received from another address", result.ErrorMessage);
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        Diagnostics.Assert("datagrams sent", 2, channel.Sent.Count);
        Assert.HasCount(2, channel.Sent);
        Diagnostics.Assert("any datagram sent to the stranger", false, channel.Sent.Any(sent => StrangerEndPoint.Equals(sent.Destination)));
        Assert.IsFalse(channel.Sent.Any(sent => StrangerEndPoint.Equals(sent.Destination)));
    }

    [TestMethod]
    public async Task ExecuteAsync_AckOfWrongBlock_ResendsTheLastBlockAtOnceThenCompletes()
    {
        var channel = Channel(Ack(0), Ack(0), Ack(1), Ack(2));

        var result = await Run(channel, Context());

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 1000, result.BytesTransferred);
        Assert.AreEqual(1000, result.BytesTransferred);
        Diagnostics.Assert("DATA blocks sent", "1 1 2", Blocks(DataBlocksSent(channel)));
        CollectionAssert.AreEqual(new ushort[] { 1, 1, 2 }, DataBlocksSent(channel));
        Diagnostics.Diff("DATA 1 re-sent byte-identical", channel.Sent[1].Datagram, channel.Sent[2].Datagram);
        CollectionAssert.AreEqual(channel.Sent[1].Datagram, channel.Sent[2].Datagram);
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
    }

    [TestMethod]
    public async Task ExecuteAsync_FourAcksOfWrongBlock_ResendsThreeTimesThenGivesUpWithSendError()
    {
        var channel = Channel(Ack(0), Ack(0), Ack(0), Ack(0), Ack(0));

        var result = await Run(channel, Context());

        Diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Diagnostics.Assert("error message", "tftp_tx: giving up waiting for block 1 ack", result.ErrorMessage);
        Assert.AreEqual("tftp_tx: giving up waiting for block 1 ack", result.ErrorMessage);
        Diagnostics.Assert("DATA blocks sent", "1 1 1 1", Blocks(DataBlocksSent(channel)));
        CollectionAssert.AreEqual(new ushort[] { 1, 1, 1, 1 }, DataBlocksSent(channel));
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
    }

    [TestMethod]
    public async Task ExecuteAsync_AckOfWrongBlockBeforeAck0_ResendsTheWriteRequestsFirstFourBytesThenContinues()
    {
        var channel = Channel(Ack(5), Ack(0), Ack(1), Ack(2));

        var result = await Run(channel, Context());

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("sent 1 is the write request's first four bytes", channel.Sent[0].Datagram[..4], channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(channel.Sent[0].Datagram[..4], channel.Sent[1].Datagram);
        Diagnostics.Assert("sent 1 destination", ServerEndPoint, channel.Sent[1].Destination);
        Assert.AreEqual(ServerEndPoint, channel.Sent[1].Destination);
        Diagnostics.Assert("DATA blocks sent", "1 2", Blocks(DataBlocksSent(channel)));
        CollectionAssert.AreEqual(new ushort[] { 1, 2 }, DataBlocksSent(channel));
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortReplyToWriteRequest_ResendsItAtOnceAndEndsWithItsMessage()
    {
        var channel = Channel(([0, 4], TransferEndPoint));

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertWriteRequestsSentAt(channel, 0, 0, 4);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(8), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(8), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", "Received too short packet", result.ErrorMessage);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FourTooShortDatagramsAfterAck0_ResendsData1ThreeTimesThenTimesOutWithTheirMessage()
    {
        (byte[], EndPoint) tooShort = ([0, 4], TransferEndPoint);
        var channel = Channel(Ack(0), tooShort, tooShort, tooShort, tooShort);

        var result = await Run(channel, Context());

        AssertData1SentAt(channel, 0, 0, 0, 0);
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "Received too short packet", result.ErrorMessage);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramThenFourAcksOfWrongBlock_GivesUpWithSendErrorAndTheTooShortMessage()
    {
        var channel = Channel(Ack(0), ([0, 4], TransferEndPoint), Ack(0), Ack(0), Ack(0));

        var result = await Run(channel, Context());

        AssertData1SentAt(channel, 0, 0, 0, 0);
        Diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Diagnostics.Assert("error message", "Received too short packet", result.ErrorMessage);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramThenSilenceMaxTime5_OperationTimedOutWithTheTooShortMessage()
    {
        var channel = Channel(Ack(0), ([0, 4], TransferEndPoint));

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertData1SentAt(channel, 0, 0, 2, 4);
        Diagnostics.Assert("clock", TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Diagnostics.Assert("error message", "Received too short packet", result.ErrorMessage);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
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

        AssertWriteRequestsSentAt(channel, 0, 0, 0);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("error message", "Received too short packet", result.ErrorMessage);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
        Diagnostics.Assert("too-short events", 3, events.Steps.Count(step => step == "Received too short packet"));
        Assert.AreEqual(3, events.Steps.Count(step => step == "Received too short packet"));
    }

    [TestMethod]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.ConnectionRefused)]
    public async Task ExecuteAsync_ReceiveRefusedAfterAck0_ResendsData1AtOnceAndPinsNoEndPoint(SocketError error)
    {
        Diagnostics.Arrange("refusal socket error", error);
        var channel = Channel(FallsSilentDatagramChannel.Refusal(error), Ack(0), FallsSilentDatagramChannel.Refusal(error), Ack(1), Ack(2));

        var result = await Run(channel, Context());

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("error message", null, result.ErrorMessage);
        Assert.IsNull(result.ErrorMessage);
        Diagnostics.Diff("DATA 1 re-sent byte-identical", channel.Sent[0].Datagram, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(channel.Sent[0].Datagram, channel.Sent[1].Datagram);
        Diagnostics.Assert("sent 1 destination", ServerEndPoint, channel.Sent[1].Destination);
        Assert.AreEqual(ServerEndPoint, channel.Sent[1].Destination);
        Diagnostics.Assert("DATA blocks sent", "1 1 2", Blocks(DataBlocksSent(channel)));
        CollectionAssert.AreEqual(new ushort[] { 1, 1, 2 }, DataBlocksSent(channel));
    }

    private static string Blocks(IEnumerable<ushort> blocks) =>
        string.Join(" ", blocks.Select(block => block.ToString(CultureInfo.InvariantCulture)));

    private static string Seconds(IEnumerable<TimeSpan> times) =>
        string.Join(" ", times.Select(time => time.TotalSeconds.ToString(CultureInfo.InvariantCulture)));

    private static async Task<TransferResult> ExecuteHandler(FallsSilentDatagramChannel channel, TransferContext context) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(context);

    /// <summary>Runs the handler in a phase, then writes the exit code, error, clock and every datagram sent with its time.</summary>
    private async Task<TransferResult> Run(FallsSilentDatagramChannel channel, TransferContext context)
    {
        TransferResult result;
        using (Diagnostics.Phase("execute tftp upload"))
        {
            result = await ExecuteHandler(channel, context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        Diagnostics.Act("clock after the transfer", clock.Now);
        Diagnostics.Act("datagrams sent", channel.Sent.Count);
        for (int index = 0; index < Math.Min(channel.Sent.Count, 8); index++)
        {
            var (datagram, destination, at) = channel.Sent[index];
            TftpTestDiagnostics.Datagram(
                Diagnostics,
                string.Create(CultureInfo.InvariantCulture, $"sent {index} to {destination} at {at.TotalSeconds}s"),
                datagram);
        }

        if (channel.Sent.Count > 8)
        {
            Diagnostics.Act("datagrams not shown", channel.Sent.Count - 8);
        }

        return result;
    }

    private static byte[] WriteRequest(int timeoutSeconds) =>
        [0, 2, .. Encoding.ASCII.GetBytes($"dest.txt\0octet\0tsize\01000\0blksize\0512\0timeout\0{timeoutSeconds}\0")];

    private static (byte[] Datagram, EndPoint Source) Ack(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    /// <summary>
    /// Asserts the channel was sent exactly <paramref name="count" /> write requests, each
    /// byte-identical with <paramref name="timeoutSeconds" /> as its <c>timeout</c>, to the
    /// server endpoint, <paramref name="intervalSeconds" /> apart from t = 0.
    /// </summary>
    private void AssertWriteRequestsSent(
        FallsSilentDatagramChannel channel,
        int timeoutSeconds,
        int count,
        int intervalSeconds)
    {
        Diagnostics.Assert("datagrams sent", count, channel.Sent.Count);
        Assert.HasCount(count, channel.Sent);
        AssertWriteRequestTimeout(channel, timeoutSeconds);
        AssertWriteRequestsSentAt(channel, [.. Enumerable.Range(0, count).Select(index => index * intervalSeconds)]);
    }

    /// <summary>
    /// Asserts the channel was sent only the write request, byte-identical each time, to
    /// the server endpoint, at each of <paramref name="seconds" />.
    /// </summary>
    private void AssertWriteRequestsSentAt(FallsSilentDatagramChannel channel, params int[] seconds)
    {
        var expectedTimes = seconds.Select(second => TimeSpan.FromSeconds(second)).ToArray();
        var actualTimes = channel.Sent.Select(sent => sent.At).ToArray();
        Diagnostics.Assert("write request send times (seconds)", Seconds(expectedTimes), Seconds(actualTimes));
        CollectionAssert.AreEqual(expectedTimes, actualTimes);
        Diagnostics.Assert(
            "every datagram is the first write request, sent to the server",
            true,
            channel.Sent.All(sent => sent.Datagram.AsSpan().SequenceEqual(channel.Sent[0].Datagram) && ServerEndPoint.Equals(sent.Destination)));
        foreach (var (datagram, destination, _) in channel.Sent)
        {
            CollectionAssert.AreEqual(channel.Sent[0].Datagram, datagram);
            Assert.AreEqual(ServerEndPoint, destination);
        }
    }

    private void AssertWriteRequestTimeout(FallsSilentDatagramChannel channel, int timeoutSeconds)
    {
        Diagnostics.Arrange("expected write request timeout option (seconds)", timeoutSeconds);
        Diagnostics.Diff("write request", WriteRequest(timeoutSeconds), channel.Sent[0].Datagram);
        CollectionAssert.AreEqual(WriteRequest(timeoutSeconds), channel.Sent[0].Datagram);
    }

    /// <summary>
    /// Asserts that after the write request at t = 0 the channel was sent only DATA 1, the
    /// upload's first 512 bytes, to the transfer endpoint, at each of <paramref name="seconds" />.
    /// </summary>
    private void AssertData1SentAt(FallsSilentDatagramChannel channel, params int[] seconds)
    {
        var expectedTimes = seconds.Select(second => TimeSpan.FromSeconds(second)).ToArray();
        var actualTimes = channel.Sent.Skip(1).Select(sent => sent.At).ToArray();
        Diagnostics.Assert("DATA 1 send times (seconds)", Seconds(expectedTimes), Seconds(actualTimes));
        CollectionAssert.AreEqual(expectedTimes, actualTimes);
        byte[] data1 = [0, 3, 0, 1, .. Upload[..512]];
        Diagnostics.Assert(
            "every datagram after the request is DATA 1, sent to the transfer endpoint",
            true,
            channel.Sent.Skip(1).All(sent => sent.Datagram.AsSpan().SequenceEqual(data1) && TransferEndPoint.Equals(sent.Destination)));
        foreach (var (datagram, destination, _) in channel.Sent.Skip(1))
        {
            CollectionAssert.AreEqual(data1, datagram);
            Assert.AreEqual(TransferEndPoint, destination);
        }
    }

    /// <summary>
    /// The block numbers of every DATA packet sent, in order.
    /// </summary>
    private static ushort[] DataBlocksSent(FallsSilentDatagramChannel channel) =>
        [.. channel.Sent
            .Where(sent => sent.Datagram[1] == 3)
            .Select(sent => (ushort)((sent.Datagram[2] << 8) | sent.Datagram[3]))];

    /// <summary>Builds the scripted channel and writes each scripted datagram as ARRANGE and BYTES lines.</summary>
    private FallsSilentDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script)
    {
        for (int index = 0; index < script.Length; index++)
        {
            TftpTestDiagnostics.Scripted(Diagnostics, index, script[index].Datagram, script[index].Source);
        }

        return new(ServerEndPoint, clock, script);
    }

    private TransferContext Context(
        TimeSpan? connectTimeout = null,
        TimeSpan? maxTime = null,
        long? operationStarted = null,
        RecordingTransferEvents? events = null)
    {
        Diagnostics.Arrange("url", "tftp://h/dest.txt");
        Diagnostics.Arrange("upload size", Upload.Length);
        Diagnostics.Arrange("connect timeout", connectTimeout?.ToString("c", CultureInfo.InvariantCulture) ?? "(none)");
        Diagnostics.Arrange("max time", maxTime?.ToString("c", CultureInfo.InvariantCulture) ?? "(none)");
        Diagnostics.Arrange("operation started (ticks)", operationStarted?.ToString(CultureInfo.InvariantCulture) ?? "(none)");
        return new()
        {
            Events = events ?? new RecordingTransferEvents(),
            Url = CurlUrl.Parse("tftp://h/dest.txt"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Upload),
            ConnectTimeout = connectTimeout,
            MaxTime = maxTime,
            OperationStarted = operationStarted,
            TimeProvider = clock,
        };
    }
}
