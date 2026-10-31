using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins <c>-X</c> (<c>CURLOPT_CUSTOMREQUEST</c>, carried as
/// <see cref="HttpRequestOptions.CustomMethod" />) on an <c>ftp://</c> transfer against
/// curl 8.21.0 as measured with <c>Record-CurlExchange.ps1 -Ftp</c> (BL-3165): a listing
/// sends the command verbatim in place of <c>LIST</c> or <c>NLST</c>, even under <c>-l</c>
/// and with the directory appended under <c>--ftp-method nocwd</c>, while a file download
/// keeps <c>SIZE</c> and <c>RETR</c>.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerListingRequestTests
{
    private const string RootUrl = "ftp://127.0.0.1:18321/";

    private const string FileUrl = "ftp://127.0.0.1:18321/dir/f.txt";

    private const string Login = "220 Ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Passive = "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n";

    private const string Opened = "150 Opening data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    /// <summary>What curl sends for a listing of the root, up to and including <c>TYPE A</c>.</summary>
    private const string RootTypeSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nEPSV\r\nTYPE A\r\n";

    private const string Names = "a.txt\r\nb.txt\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("NLST", DisplayName = "-X NLST")]
    [DataRow("LIST -a", DisplayName = "-X 'LIST -a'")]
    public async Task ExecuteAsync_CustomRequestOnAListing_SendsTheCommandInPlaceOfList(string command)
    {
        FtpRun run = await RunAsync(RootUrl, Login + Passive + Opened, Names, c => new TransferContext { Url = c.Url, Output = c.Output, Http = new HttpRequestOptions { CustomMethod = command } });

        AssertSent(RootTypeSent + command + "\r\nQUIT\r\n", run);
        Assert.AreEqual(FtpAsciiOutput.Of(Names), run.OutputText);
        Assert.AreEqual(TransferResult.Success(Names.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomRequestUnderListOnly_SendsTheCommandNotNlst()
    {
        FtpRun run = await RunAsync(RootUrl, Login + Passive + Opened, Names, c => new TransferContext { Url = c.Url, Output = c.Output, ListOnly = true, Http = new HttpRequestOptions { CustomMethod = "LIST -a" } });

        AssertSent(RootTypeSent + "LIST -a\r\nQUIT\r\n", run);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomRequestUnderNoCwd_AppendsTheDirectory()
    {
        FtpRun run = await RunAsync("ftp://127.0.0.1:18321/sub/", Login + Passive + Opened, Names, c => new TransferContext { Url = c.Url, Output = c.Output, FtpFileMethod = FtpFileMethod.NoCwd, Http = new HttpRequestOptions { CustomMethod = "NLST" } });

        AssertSent(RootTypeSent + "NLST sub\r\nQUIT\r\n", run);
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomRequestOnAFileDownload_StillSendsRetr()
    {
        const string data = "0123456789";
        string replies = Login + "250 OK\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 10\r\n" + Opened;

        FtpRun run = await RunAsync(FileUrl, replies, data, c => new TransferContext { Url = c.Url, Output = c.Output, Http = new HttpRequestOptions { CustomMethod = "NLST" } });

        AssertSent("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run);
        Assert.AreEqual(data, run.OutputText);
    }

    [TestMethod]
    [DataRow(false, "LIST", DisplayName = "no -X")]
    [DataRow(true, "NLST", DisplayName = "no -X, -l")]
    public async Task ExecuteAsync_NoCustomRequest_SendsListOrNlst(bool listOnly, string expectedVerb)
    {
        FtpRun run = await RunAsync(RootUrl, Login + Passive + Opened, Names, c => new TransferContext { Url = c.Url, Output = c.Output, ListOnly = listOnly, Http = new HttpRequestOptions() });

        AssertSent(RootTypeSent + expectedVerb + "\r\nQUIT\r\n", run);
    }

    private void AssertSent(string expected, FtpRun run)
    {
        TestDiagnostics.For(TestContext).DiffSent(expected, run.Sent);
        Assert.AreEqual(expected, run.Sent);
    }

    private async Task<FtpRun> RunAsync(string url, string replies, string data, Func<TransferContext, TransferContext> adjust)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(url, replies, data);
        FtpRun run = await FtpRun.ExecuteAsync(url, replies, data, adjust);
        diagnostics.ActRun(run);
        return run;
    }
}
