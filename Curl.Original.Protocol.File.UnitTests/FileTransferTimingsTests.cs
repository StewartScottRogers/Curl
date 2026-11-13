using System.Text;
using Curl.Original.Protocol.File.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins that a <c>file://</c> transfer reports non-zero timings for <c>%{time_*}</c> and the
/// speeds that follow from them, as curl's FILE handler does (BL-5101).
/// </summary>
[TestClass]
public sealed class FileTransferTimingsTests
{
    private static string OsPath => "/dir/w.out".Replace('/', Path.DirectorySeparatorChar);

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsNonZeroTimings()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes("HELLO\n"));
        var clock = new TickingClock();
        var context = new TransferContext { Output = new MemoryStream(), Url = CurlUrl.Parse("file:///dir/w.out"), TimeProvider = clock };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        AssertTimed(result);
        Assert.AreEqual(6L, result.Report!.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsNonZeroTimings()
    {
        var fileSystem = new FakeFileSystem();
        var clock = new TickingClock();
        var context = new TransferContext
        {
            Output = new MemoryStream(),
            Url = CurlUrl.Parse("file:///dir/w.out"),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes("HELLO\n")),
            TimeProvider = clock,
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        AssertTimed(result);
        Assert.AreEqual(6L, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedDownload_StillReportsTimingsAndKeepsTheSize()
    {
        var clock = new TickingClock();
        var context = new TransferContext { Output = new MemoryStream(), Url = CurlUrl.Parse("file:///dir/missing"), TimeProvider = clock };

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        Assert.AreNotEqual(CurlExitCode.Ok, result.ExitCode);
        AssertTimed(result);
        Assert.AreEqual(result.BytesTransferred, result.Report!.DownloadSize);
    }

    private static void AssertTimed(TransferResult result)
    {
        var timings = result.Report?.Timings;
        Assert.IsNotNull(timings);
        Assert.IsNotNull(timings.Connect?.NameResolved);
        Assert.IsTrue(timings.Connect.NameResolved > timings.Started);
        Assert.IsTrue(timings.RequestReady > timings.Connect.NameResolved);
        Assert.IsTrue(timings.FirstByteReceived > timings.Started);
        Assert.IsTrue(timings.Completed > timings.Started);
    }

    private sealed class TickingClock : TimeProvider
    {
        private long now;

        public override long TimestampFrequency => 1_000_000;

        public override long GetTimestamp() => now += 100;
    }
}
