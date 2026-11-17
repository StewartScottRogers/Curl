using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Tftp;

/// <summary>
/// Pins that a <c>tftp://</c> download reports the bytes it received as
/// <c>%{size_download}</c> through <see cref="TransferReport.DownloadSize" /> while keeping
/// its <see cref="TransferReport.Timings" /> (BL-5326).
/// </summary>
[TestClass]
public sealed class TftpDownloadSizeReportTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsTheBytesReceivedAsDownloadSizeWithTimingsAndASpeed()
    {
        var clock = new SteppingTimeProvider();
        var channel = new FallsSilentDatagramChannel(
            ServerEndPoint,
            clock.Inner,
            [Data(1, new string('a', 512)), Data(2, new string('b', 88))]);

        var result = await RunAsync(channel, clock, blockSize: null);

        AssertSizeAndSpeed(result, 600);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWithNegotiatedBlockSize_ReportsTheBytesReceivedAsDownloadSize()
    {
        var clock = new SteppingTimeProvider();
        var channel = new FallsSilentDatagramChannel(
            ServerEndPoint,
            clock.Inner,
            [
                ([0, 6, .. "blksize\01024\0"u8.ToArray()], TransferEndPoint),
                Data(1, new string('a', 1024)),
                Data(2, new string('b', 1000)),
            ]);

        var result = await RunAsync(channel, clock, blockSize: 1024);

        AssertSizeAndSpeed(result, 2024);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedDownload_KeepsItsErrorExitCodeAndTimings()
    {
        var clock = new SteppingTimeProvider();
        var channel = new FallsSilentDatagramChannel(
            ServerEndPoint,
            clock.Inner,
            [([0, 5, 0, 1, .. "File not found\0"u8.ToArray()], TransferEndPoint)]);

        var result = await RunAsync(channel, clock, blockSize: null);

        Assert.AreEqual(CurlExitCode.TftpNotFound, result.ExitCode);
        Assert.IsNotNull(result.Report?.Timings);
    }

    private static void AssertSizeAndSpeed(TransferResult result, long expected)
    {
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNotNull(result.Report);
        Assert.AreEqual(expected, result.Report.DownloadSize);
        Assert.AreEqual(0, result.Report.UploadSize);
        Assert.IsNotNull(result.Report.Timings);
        var seconds = (result.Report.Timings.Completed - result.Report.Timings.Started) / (double)TimeSpan.TicksPerSecond;
        Assert.IsTrue(seconds > 0);
        Assert.IsTrue(result.Report.DownloadSize / seconds > 0);
    }

    private static (byte[] Datagram, EndPoint Source) Data(int block, string payload) =>
        ([0, 3, 0, (byte)block, .. System.Text.Encoding.ASCII.GetBytes(payload)], TransferEndPoint);

    private static async Task<TransferResult> RunAsync(IDatagramChannel channel, TimeProvider clock, int? blockSize) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("tftp://h/file.txt"),
                Output = new MemoryStream(),
                TftpBlockSize = blockSize,
                TimeProvider = clock,
            });

    /// <summary>A clock whose timestamp moves 10 ms on every read, over a <see cref="ManualTimeProvider" /> that serves the timers.</summary>
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
