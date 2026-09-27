using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

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

    [TestMethod]
    public async Task ExecuteAsync_SilentServerNoLimits_SendsWriteRequest50Times7SecondsApartThenCouldntConnect()
    {
        var channel = Channel();

        var result = await Run(channel, Context());

        AssertWriteRequestsSent(channel, timeoutSeconds: 6, count: 50, intervalSeconds: 7);
        Assert.AreEqual(TimeSpan.FromSeconds(350), clock.Now);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerConnectTimeout10_SendsWriteRequest3Times4SecondsApartThenCouldntConnectAt12()
    {
        var channel = Channel();

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertWriteRequestsSent(channel, timeoutSeconds: 3, count: 3, intervalSeconds: 4);
        Assert.AreEqual(TimeSpan.FromSeconds(12), clock.Now);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerMaxTime5_SendsWriteRequestTimeout1At0And2And4ThenOperationTimedOutAt5()
    {
        var channel = Channel();

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertWriteRequestsSent(channel, timeoutSeconds: 1, count: 3, intervalSeconds: 2);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerMaxTime2OperationStarted1500MillisecondsEarlier_OperationTimedOutAfter500MillisecondsReporting2000()
    {
        var channel = Channel();

        var result = await Run(
            channel,
            Context(maxTime: TimeSpan.FromSeconds(2), operationStarted: -TimeSpan.FromMilliseconds(1500).Ticks));

        Assert.AreEqual(TimeSpan.FromMilliseconds(500), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 2000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterAck0NoLimits_ResendsData1At6And12And18ThenTimesOutAt24()
    {
        var channel = Channel(Ack(0));

        var result = await Run(channel, Context());

        AssertWriteRequestTimeout(channel, 6);
        AssertData1SentAt(channel, 0, 6, 12, 18);
        Assert.AreEqual(TimeSpan.FromSeconds(24), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Timeout was reached", result.ErrorMessage);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterAck0ConnectTimeout10_SendsTimeout3ThenResendsData1On15SecondSchedule()
    {
        var channel = Channel(Ack(0));

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertWriteRequestTimeout(channel, 3);
        AssertData1SentAt(channel, 0, 6, 12, 18);
        Assert.AreEqual(TimeSpan.FromSeconds(24), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Timeout was reached", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterAck0MaxTime5_ResendsData1At2And4ThenOperationTimedOutWith0BytesReceived()
    {
        var channel = Channel(Ack(0));

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertWriteRequestTimeout(channel, 1);
        AssertData1SentAt(channel, 0, 2, 4);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_AckFromUnknownEndPoint_SendsItNothingAndEndsWithRecvError()
    {
        var channel = Channel(Ack(0), (Ack(1).Datagram, StrangerEndPoint));

        var result = await Run(channel, Context());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Data received from another address", result.ErrorMessage);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        Assert.HasCount(2, channel.Sent);
        Assert.IsFalse(channel.Sent.Any(sent => StrangerEndPoint.Equals(sent.Destination)));
    }

    [TestMethod]
    public async Task ExecuteAsync_AckOfWrongBlock_ResendsTheLastBlockAtOnceThenCompletes()
    {
        var channel = Channel(Ack(0), Ack(0), Ack(1), Ack(2));

        var result = await Run(channel, Context());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1000, result.BytesTransferred);
        CollectionAssert.AreEqual(new ushort[] { 1, 1, 2 }, DataBlocksSent(channel));
        CollectionAssert.AreEqual(channel.Sent[1].Datagram, channel.Sent[2].Datagram);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
    }

    [TestMethod]
    public async Task ExecuteAsync_FourAcksOfWrongBlock_ResendsThreeTimesThenGivesUpWithSendError()
    {
        var channel = Channel(Ack(0), Ack(0), Ack(0), Ack(0), Ack(0));

        var result = await Run(channel, Context());

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("tftp_tx: giving up waiting for block 1 ack", result.ErrorMessage);
        CollectionAssert.AreEqual(new ushort[] { 1, 1, 1, 1 }, DataBlocksSent(channel));
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
    }

    [TestMethod]
    public async Task ExecuteAsync_AckOfWrongBlockBeforeAck0_ResendsTheWriteRequestsFirstFourBytesThenContinues()
    {
        var channel = Channel(Ack(5), Ack(0), Ack(1), Ack(2));

        var result = await Run(channel, Context());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(channel.Sent[0].Datagram[..4], channel.Sent[1].Datagram);
        Assert.AreEqual(ServerEndPoint, channel.Sent[1].Destination);
        CollectionAssert.AreEqual(new ushort[] { 1, 2 }, DataBlocksSent(channel));
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortReplyToWriteRequest_ResendsItAtOnceAndEndsWithItsMessage()
    {
        var channel = Channel(([0, 4], TransferEndPoint));

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertWriteRequestsSentAt(channel, 0, 0, 4);
        Assert.AreEqual(TimeSpan.FromSeconds(8), clock.Now);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FourTooShortDatagramsAfterAck0_ResendsData1ThreeTimesThenTimesOutWithTheirMessage()
    {
        (byte[], EndPoint) tooShort = ([0, 4], TransferEndPoint);
        var channel = Channel(Ack(0), tooShort, tooShort, tooShort, tooShort);

        var result = await Run(channel, Context());

        AssertData1SentAt(channel, 0, 0, 0, 0);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramThenFourAcksOfWrongBlock_GivesUpWithSendErrorAndTheTooShortMessage()
    {
        var channel = Channel(Ack(0), ([0, 4], TransferEndPoint), Ack(0), Ack(0), Ack(0));

        var result = await Run(channel, Context());

        AssertData1SentAt(channel, 0, 0, 0, 0);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooShortDatagramThenSilenceMaxTime5_OperationTimedOutWithTheTooShortMessage()
    {
        var channel = Channel(Ack(0), ([0, 4], TransferEndPoint));

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertData1SentAt(channel, 0, 0, 2, 4);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Received too short packet", result.ErrorMessage);
    }

    private static async Task<TransferResult> Run(FallsSilentDatagramChannel channel, TransferContext context) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(context);

    private static byte[] WriteRequest(int timeoutSeconds) =>
        [0, 2, .. Encoding.ASCII.GetBytes($"dest.txt\0octet\0tsize\01000\0blksize\0512\0timeout\0{timeoutSeconds}\0")];

    private static (byte[] Datagram, EndPoint Source) Ack(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    /// <summary>
    /// Asserts the channel was sent exactly <paramref name="count" /> write requests, each
    /// byte-identical with <paramref name="timeoutSeconds" /> as its <c>timeout</c>, to the
    /// server endpoint, <paramref name="intervalSeconds" /> apart from t = 0.
    /// </summary>
    private static void AssertWriteRequestsSent(
        FallsSilentDatagramChannel channel,
        int timeoutSeconds,
        int count,
        int intervalSeconds)
    {
        Assert.HasCount(count, channel.Sent);
        AssertWriteRequestTimeout(channel, timeoutSeconds);
        AssertWriteRequestsSentAt(channel, [.. Enumerable.Range(0, count).Select(index => index * intervalSeconds)]);
    }

    /// <summary>
    /// Asserts the channel was sent only the write request, byte-identical each time, to
    /// the server endpoint, at each of <paramref name="seconds" />.
    /// </summary>
    private static void AssertWriteRequestsSentAt(FallsSilentDatagramChannel channel, params int[] seconds)
    {
        CollectionAssert.AreEqual(
            seconds.Select(second => TimeSpan.FromSeconds(second)).ToArray(),
            channel.Sent.Select(sent => sent.At).ToArray());
        foreach (var (datagram, destination, _) in channel.Sent)
        {
            CollectionAssert.AreEqual(channel.Sent[0].Datagram, datagram);
            Assert.AreEqual(ServerEndPoint, destination);
        }
    }

    private static void AssertWriteRequestTimeout(FallsSilentDatagramChannel channel, int timeoutSeconds) =>
        CollectionAssert.AreEqual(WriteRequest(timeoutSeconds), channel.Sent[0].Datagram);

    /// <summary>
    /// Asserts that after the write request at t = 0 the channel was sent only DATA 1, the
    /// upload's first 512 bytes, to the transfer endpoint, at each of <paramref name="seconds" />.
    /// </summary>
    private static void AssertData1SentAt(FallsSilentDatagramChannel channel, params int[] seconds)
    {
        CollectionAssert.AreEqual(
            seconds.Select(second => TimeSpan.FromSeconds(second)).ToArray(),
            channel.Sent.Skip(1).Select(sent => sent.At).ToArray());
        byte[] data1 = [0, 3, 0, 1, .. Upload[..512]];
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

    private FallsSilentDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script) =>
        new(ServerEndPoint, clock, script);

    private TransferContext Context(TimeSpan? connectTimeout = null, TimeSpan? maxTime = null, long? operationStarted = null) =>
        new()
        {
            Url = CurlUrl.Parse("tftp://h/dest.txt"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Upload),
            ConnectTimeout = connectTimeout,
            MaxTime = maxTime,
            OperationStarted = operationStarted,
            TimeProvider = clock,
        };
}
