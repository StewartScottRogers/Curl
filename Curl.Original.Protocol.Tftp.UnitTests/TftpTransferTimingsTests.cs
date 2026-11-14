using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Tftp;

/// <summary>
/// Pins that a <c>tftp://</c> download and upload report the timings behind
/// <c>%{time_*}</c> and <c>%{speed_*}</c> through <see cref="TransferReport.Timings" /> (BL-5102).
/// </summary>
[TestClass]
public sealed class TftpTransferTimingsTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsNonZeroTimings()
    {
        var clock = new SteppingTimeProvider();
        var channel = new FallsSilentDatagramChannel(ServerEndPoint, clock.Inner, [([0, 3, 0, 1, .. "hello"u8.ToArray()], TransferEndPoint)]);
        var result = await RunAsync(channel, clock, new MemoryStream(), upload: false);

        AssertTimings(result);
        Assert.IsNotNull(result.Report?.Timings?.FirstByteReceived);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsNonZeroTimingsAndTheSizeToDivideBy()
    {
        var clock = new SteppingTimeProvider();
        var channel = new AcknowledgingDatagramChannel(ServerEndPoint, TransferEndPoint);
        var result = await RunAsync(channel, clock, new MemoryStream(new byte[600]), upload: true);

        AssertTimings(result);
        Assert.AreEqual(600, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedTransfer_ReportsTimingsWithoutAFirstByte()
    {
        var clock = new SteppingTimeProvider();
        var channel = new FallsSilentDatagramChannel(ServerEndPoint, clock.Inner, []);
        var result = await RunAsync(channel, clock, new MemoryStream(), upload: false);

        Assert.AreNotEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNotNull(result.Report?.Timings);
        Assert.IsNull(result.Report.Timings.FirstByteReceived);
    }

    private static void AssertTimings(TransferResult result)
    {
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var timings = result.Report?.Timings;
        Assert.IsNotNull(timings);
        Assert.IsTrue(timings.Completed > timings.Started);
        Assert.IsNotNull(timings.Connect);
        Assert.IsTrue(timings.Connect.NameResolved > timings.Started);
        Assert.IsTrue(timings.Connect.Connected > timings.Started);
        Assert.IsTrue(timings.RequestReady > timings.Started);
    }

    private static async Task<TransferResult> RunAsync(IDatagramChannel channel, TimeProvider clock, MemoryStream stream, bool upload) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("tftp://h/dest.txt"),
                Output = upload ? new MemoryStream() : stream,
                Upload = upload ? stream : null,
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
