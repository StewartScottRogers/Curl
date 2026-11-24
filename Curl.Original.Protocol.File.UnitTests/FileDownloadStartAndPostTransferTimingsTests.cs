using System.Text;
using Curl.Original.Protocol.File.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins that a <c>file://</c> download reports no connect time, a start-transfer before the total
/// and a post-transfer after it starts, as curl does, and that an upload keeps its timings (BL-5387).
/// </summary>
[TestClass]
public sealed class FileDownloadStartAndPostTransferTimingsTests
{
    private static string OsPath => "/dir/w.out".Replace('/', Path.DirectorySeparatorChar);

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
    }

    [TestMethod]
    public async Task ExecuteAsync_Download_PreTransferEqualsPostTransferAtTheTotalAndStartTransferIsNotLater()
    {
        var timings = await DownloadTimingsAsync();

        Assert.AreEqual(timings.RequestSent, timings.RequestReady);
        Assert.AreEqual(timings.Completed, timings.RequestReady);
        Assert.IsTrue(timings.FirstByteReceived <= timings.RequestReady);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_HasNoConnectOrStartTransferButAPostTransfer()
    {
        var context = new TransferContext
        {
            Output = new MemoryStream(),
            Url = CurlUrl.Parse("file:///dir/w.out"),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes("HELLO\n")),
            TimeProvider = new TickingClock(),
        };

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        var timings = result.Report!.Timings!;
        Assert.IsNull(timings.Connect!.Connected);
        Assert.IsNull(timings.FirstByteReceived);
        Assert.AreEqual(timings.Completed, timings.RequestSent);
    }

    private static async Task<TransferTimings> DownloadTimingsAsync()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes("HELLO\n"));
        var context = new TransferContext { Output = new MemoryStream(), Url = CurlUrl.Parse("file:///dir/w.out"), TimeProvider = new TickingClock() };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return result.Report!.Timings!;
    }

    private sealed class TickingClock : TimeProvider
    {
        private long now;

        public override long TimestampFrequency => 1_000_000;

        public override long GetTimestamp() => now += 100;
    }
}
