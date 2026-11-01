using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins the sizes an <c>ftp://</c> transfer's <see cref="TransferReport" /> carries, the source
/// of <c>%{size_download}</c>, <c>%{speed_download}</c>, <c>%{size_upload}</c> and
/// <c>%{speed_upload}</c> (BL-3166): the bytes this transfer received for a <c>RETR</c>,
/// <c>LIST</c> or <c>NLST</c>, or sent for a <c>STOR</c>.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerSizeDownloadTests
{
    private const string RootUrl = "ftp://127.0.0.1:18321/";

    private const string FileUrl = "ftp://127.0.0.1:18321/dir/f.txt";

    private const string Login = "220 Ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Passive = "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n";

    /// <summary>The replies to <c>CWD dir</c>, <c>EPSV</c>, <c>TYPE I</c> and <c>SIZE</c> for the ten-byte file.</summary>
    private const string Sized = Login + "250 OK\r\n" + Passive + "213 10\r\n";

    private const string Opened = "150 Opening data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string Names = "a.txt\r\nb.txt\r\n";

    private const string Listing = "-rw-r--r-- 1 u g 10 Jan 01 00:00 a.txt\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_Retr_ReportsTheBytesWrittenAsTheDownloadSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(FileUrl, Sized + Opened, "0123456789");

        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, Sized + Opened, "0123456789");

        AssertSizes(diagnostics, run, run.OutputText.Length, 0);
        Assert.AreEqual(10L, run.Report!.DownloadSize);
    }

    [TestMethod]
    [DataRow(false, Listing, DisplayName = "LIST")]
    [DataRow(true, Names, DisplayName = "NLST (-l)")]
    public async Task ExecuteAsync_Listing_ReportsTheListingBytesAsTheDownloadSize(bool listOnly, string listing)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(RootUrl, Login + Passive + Opened, listing);

        FtpRun run = await FtpRun.ExecuteAsync(RootUrl, Login + Passive + Opened, listing, c => new TransferContext { Url = c.Url, Output = c.Output, ListOnly = listOnly });

        Assert.AreEqual(FtpAsciiOutput.Of(listing), run.OutputText);
        AssertSizes(diagnostics, run, listing.Length, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumedDownload_ReportsOnlyTheBytesReceivedThisTransfer()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Sized + "350 Restarting\r\n" + Opened;
        diagnostics.ArrangeFtp(FileUrl, replies, "3456789");

        // Measured on 2026-10-22: curl 8.22.0 -C 3 -w '%{size_download}' against
        // Record-CurlExchange.ps1 -Ftp -FtpData 0123456789 writes 7, the bytes after the offset.
        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, replies, "3456789", c => new TransferContext { Url = c.Url, Output = c.Output, ResumeFrom = 3 });

        Assert.AreEqual("3456789", run.OutputText);
        AssertSizes(diagnostics, run, 7, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsTheBytesSentAsTheUploadSizeAndNoDownload()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = "220 Ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n" + Passive + "150 Opening\r\n226 Transfer complete\r\n221 Bye\r\n";
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18321/f.txt", replies);

        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/f.txt", replies, "", c => new TransferContext { Url = c.Url, Output = c.Output, Upload = new MemoryStream(Encoding.Latin1.GetBytes("hello world\n")) });

        AssertSizes(diagnostics, run, 0, 12);
    }

    private static void AssertSizes(TestDiagnostics diagnostics, FtpRun run, long download, long upload)
    {
        diagnostics.ActRun(run);
        string sizes = $"{run.Report!.DownloadSize} {run.Report.UploadSize}";
        diagnostics.Assert("download and upload sizes", $"{download} {upload}", sizes);
        Assert.IsTrue(run.Result.IsSuccess);
        Assert.AreEqual($"{download} {upload}", sizes);
    }
}
