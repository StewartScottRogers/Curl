using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Tftp;

/// <summary>
/// Pins that a <c>tftp://</c> download reports no connect time, a start-transfer before the total
/// and a post-transfer after the start, as curl does, and that an upload keeps its timings (BL-5387).
/// </summary>
[TestClass]
public sealed class TftpDownloadStartAndPostTransferTimingsTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public async Task ExecuteAsync_Download_HasNoConnectTime()
    {
        var timings = await DownloadTimingsAsync();

        Assert.IsNull(timings.Connect!.Connected);
    }

    [TestMethod]
    public async Task ExecuteAsync_Download_StartsTransferBeforeTheTotal()
    {
        var timings = await DownloadTimingsAsync();

        Assert.IsTrue(timings.FirstByteReceived > timings.Started);
        Assert.IsTrue(timings.FirstByteReceived < timings.Completed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsAPostTransferAfterTheStart()
    {
        var timings = await DownloadTimingsAsync();

        Assert.IsTrue(timings.RequestSent > timings.FirstByteReceived);
        Assert.IsTrue(timings.RequestSent <= timings.Completed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_KeepsItsConnectAndHasNoStartOrPostTransfer()
    {
        var clock = new SteppingTimeProvider();
        var result = await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(new AcknowledgingDatagramChannel(ServerEndPoint, TransferEndPoint))))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("tftp://h/dest.txt"),
                Output = new MemoryStream(),
                Upload = new MemoryStream(new byte[600]),
                TimeProvider = clock,
            });

        var timings = result.Report!.Timings!;
        Assert.IsTrue(timings.Connect!.Connected > timings.Started);
        Assert.IsNull(timings.FirstByteReceived);
        Assert.IsNull(timings.RequestSent);
    }

    private static async Task<TransferTimings> DownloadTimingsAsync()
    {
        var clock = new SteppingTimeProvider();
        var channel = new FallsSilentDatagramChannel(ServerEndPoint, clock.Inner, [([0, 3, 0, 1, .. "hello"u8.ToArray()], TransferEndPoint)]);
        var result = await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("tftp://h/dest.txt"),
                Output = new MemoryStream(),
                TimeProvider = clock,
            });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return result.Report!.Timings!;
    }

    private sealed class SteppingTimeProvider : TimeProvider
    {
        private long reads;

        internal ManualTimeProvider Inner { get; } = new();

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Inner.GetTimestamp() + (++reads * TimeSpan.TicksPerMillisecond * 10);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            Inner.CreateTimer(callback, state, dueTime, period);
    }
}
