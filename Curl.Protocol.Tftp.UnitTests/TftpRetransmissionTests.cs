using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins what a <c>tftp://</c> download does when the server goes silent, repeats itself
/// or is joined by a stranger, against curl 8.21.0 measured on 2026-09-26 with loopback
/// UDP servers: when the last packet is re-sent, how often, and the exit code and message
/// the transfer ends with. Every wait runs on a <see cref="ManualTimeProvider" />, so the
/// times asserted are the whole seconds curl's rules give rather than its wall-clock
/// jitter.
/// </summary>
[TestClass]
public sealed class TftpRetransmissionTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    /// <summary>A port nothing in the transfer asked to hear from.</summary>
    private static readonly IPEndPoint StrangerEndPoint = new(IPAddress.Loopback, 50999);

    private readonly ManualTimeProvider clock = new();

    [TestMethod]
    public async Task ExecuteAsync_SilentServerNoLimits_SendsReadRequest50Times7SecondsApartThenCouldntConnect()
    {
        var channel = Channel();
        var connector = new RecordingDatagramConnector(DatagramOpenResult.Opened(channel));

        var result = await new TftpProtocolHandler(connector).ExecuteAsync(Context());

        Assert.HasCount(1, connector.Opens);
        AssertReadRequestsSent(channel, timeoutSeconds: 6, count: 50, intervalSeconds: 7);
        Assert.AreEqual(TimeSpan.FromSeconds(350), clock.Now);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerZeroLimits_TakesZeroAsNoLimit()
    {
        var channel = Channel();

        var result = await Run(channel, Context(connectTimeout: TimeSpan.Zero, maxTime: TimeSpan.Zero));

        AssertReadRequestsSent(channel, timeoutSeconds: 6, count: 50, intervalSeconds: 7);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerConnectTimeout10_SendsReadRequest3Times4SecondsApartThenCouldntConnectAt12()
    {
        var channel = Channel();

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertReadRequestsSent(channel, timeoutSeconds: 3, count: 3, intervalSeconds: 4);
        Assert.AreEqual(TimeSpan.FromSeconds(12), clock.Now);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterFirstBlockConnectTimeout10_ResendsAck3Times6SecondsApartThenTimesOutAt24()
    {
        var channel = Channel(Data(1, Payload(512, 'a')));

        var result = await Run(channel, Context(connectTimeout: TimeSpan.FromSeconds(10)));

        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 6, 12, 18);
        Assert.AreEqual(TimeSpan.FromSeconds(24), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Timeout was reached", result.ErrorMessage);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterFirstBlockMaxTimeOverAnHour_ResendsAckOn15SecondScheduleThenTimesOut()
    {
        var channel = Channel(Data(1, Payload(512, 'a')));

        var result = await Run(channel, Context(maxTime: TimeSpan.FromHours(2)));

        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 6, 12, 18);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Timeout was reached", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServerMaxTime5_SendsReadRequestTimeout1At0And2And4ThenOperationTimedOutAt5()
    {
        var channel = Channel();

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(5)));

        AssertReadRequestsSent(channel, timeoutSeconds: 1, count: 3, intervalSeconds: 2);
        Assert.AreEqual(TimeSpan.FromSeconds(5), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 5000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentAfterFirstBlockMaxTime20_ResendsAckOnScheduleFromTimeLeftThenOperationTimedOutAt20()
    {
        var channel = Channel(Data(1, Payload(512, 'a')));

        var result = await Run(channel, Context(maxTime: TimeSpan.FromSeconds(20)));

        Assert.IsTrue(Encoding.ASCII.GetString(channel.Sent[0].Datagram).EndsWith("timeout\u00005\u0000", StringComparison.Ordinal));
        AssertAcknowledgementsOfBlock1At(channel, 0, 0, 6, 12, 18);
        Assert.AreEqual(TimeSpan.FromSeconds(20), clock.Now);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 20000 milliseconds with 512 bytes received", result.ErrorMessage);
        Assert.AreEqual(512, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_DuplicateBlock_AcksItAgainWithoutWritingItTwiceThenCompletes()
    {
        var first = Payload(512, 'a');
        var channel = Channel(Data(1, first), Data(1, first), Data(2, "end"));
        var output = new MemoryStream();

        var result = await Run(channel, Context(output: output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(515, result.BytesTransferred);
        Assert.AreEqual(first + "end", Encoding.ASCII.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new ushort[] { 1, 1, 2 }, AcknowledgedBlocks(channel));
        Assert.IsTrue(channel.Sent.Skip(1).All(sent => TransferEndPoint.Equals(sent.Destination)));
    }

    [TestMethod]
    public async Task ExecuteAsync_DatagramFromUnknownEndPoint_SendsItNothingAndEndsWithRecvError()
    {
        var channel = Channel(Data(1, Payload(512, 'a')), (Data(2, "end").Datagram, StrangerEndPoint));

        var result = await Run(channel, Context());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Data received from another address", result.ErrorMessage);
        Assert.AreEqual(512, result.BytesTransferred);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
        CollectionAssert.AreEqual(new ushort[] { 1 }, AcknowledgedBlocks(channel));
        Assert.IsFalse(channel.Sent.Any(sent => StrangerEndPoint.Equals(sent.Destination)));
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledWhileWaiting_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var channel = Channel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await Run(channel, Context(cancellationToken: cancellation.Token)));
    }

    private static async Task<TransferResult> Run(FallsSilentDatagramChannel channel, TransferContext context) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(context);

    private static byte[] ReadRequest(int timeoutSeconds) =>
        [0, 1, .. Encoding.ASCII.GetBytes($"file.txt\0octet\0tsize\00\0blksize\0512\0timeout\0{timeoutSeconds}\0")];

    private static string Payload(int length, char fill) => new(fill, length);

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    /// <summary>
    /// Asserts the channel was sent exactly <paramref name="count" /> read requests, each
    /// byte-identical with <paramref name="timeoutSeconds" /> as its <c>timeout</c>, to the
    /// server endpoint, <paramref name="intervalSeconds" /> apart from t = 0.
    /// </summary>
    private static void AssertReadRequestsSent(
        FallsSilentDatagramChannel channel,
        int timeoutSeconds,
        int count,
        int intervalSeconds)
    {
        Assert.HasCount(count, channel.Sent);
        var expected = ReadRequest(timeoutSeconds);
        for (var index = 0; index < count; index++)
        {
            CollectionAssert.AreEqual(expected, channel.Sent[index].Datagram);
            Assert.AreEqual(ServerEndPoint, channel.Sent[index].Destination);
            Assert.AreEqual(TimeSpan.FromSeconds(index * intervalSeconds), channel.Sent[index].At);
        }
    }

    /// <summary>
    /// Asserts that after the read request at t = 0 the channel was sent only ACK 1, to the
    /// transfer endpoint, at each of <paramref name="seconds" />.
    /// </summary>
    private static void AssertAcknowledgementsOfBlock1At(FallsSilentDatagramChannel channel, params int[] seconds)
    {
        CollectionAssert.AreEqual(
            seconds.Select(second => TimeSpan.FromSeconds(second)).ToArray(),
            channel.Sent.Select(sent => sent.At).ToArray());
        foreach (var (datagram, destination, _) in channel.Sent.Skip(1))
        {
            CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, datagram);
            Assert.AreEqual(TransferEndPoint, destination);
        }
    }

    /// <summary>
    /// The block numbers of every acknowledgement sent, skipping the read request.
    /// </summary>
    private static ushort[] AcknowledgedBlocks(FallsSilentDatagramChannel channel) =>
        [.. channel.Sent.Skip(1).Select(sent =>
        {
            CollectionAssert.AreEqual(new byte[] { 0, 4 }, sent.Datagram[..2]);
            return (ushort)((sent.Datagram[2] << 8) | sent.Datagram[3]);
        })];

    private FallsSilentDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script) =>
        new(ServerEndPoint, clock, script);

    private TransferContext Context(
        TimeSpan? connectTimeout = null,
        TimeSpan? maxTime = null,
        Stream? output = null,
        CancellationToken cancellationToken = default) =>
        new()
        {
            Url = new Uri("tftp://h/file.txt"),
            Output = output ?? new MemoryStream(),
            ConnectTimeout = connectTimeout,
            MaxTime = maxTime,
            TimeProvider = clock,
            CancellationToken = cancellationToken,
        };
}
