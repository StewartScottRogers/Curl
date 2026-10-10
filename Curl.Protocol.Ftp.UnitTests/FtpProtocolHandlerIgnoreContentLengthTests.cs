using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>--ignore-content-length</c> on an <c>ftp://</c> download against curl 8.21.0
/// (BL-1982, upstream tests 1137 and 416): curl's <c>ftp_state_type_resp</c> skips
/// <c>SIZE</c> for <c>data->set.ignorecl</c>, so <c>TYPE I</c> is followed straight by
/// <c>RETR</c>, or by <c>REST</c> for a range from an offset, and the body is read to the
/// data connection's close.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerIgnoreContentLengthTests
{
    private const string Url = "ftp://127.0.0.1:18321/dir/f.txt";

    /// <summary>The server's replies from the greeting through <c>TYPE I</c>.</summary>
    private const string Typed = "220 Ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n250 OK\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>What curl sent up to and including <c>TYPE I</c>.</summary>
    private const string TypeSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\nEPSV\r\nTYPE I\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_IgnoreContentLength_SendsRetrWithNoSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ignore-content-length", true);

        FtpRun run = await RunAsync(diagnostics, Typed + Opened + Bye, "0123456789", c => new TransferContext { Url = c.Url, Output = c.Output, Http = new HttpRequestOptions { IgnoreContentLength = true } });

        string expectedSent = TypeSent + "RETR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("0123456789", run.OutputText);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_IgnoreContentLengthWithAnOffsetRange_SendsRestWithNoSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ignore-content-length", true);
        diagnostics.Arrange("range", "5-");

        FtpRun run = await RunAsync(diagnostics, Typed + "350 Restarting\r\n" + Opened + Bye, "56789", c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.FromOffset(5), Http = new HttpRequestOptions { IgnoreContentLength = true } });

        string expectedSent = TypeSent + "REST 5\r\nRETR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("56789", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpOptionsWithoutIgnoreContentLength_StillSendsSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("ignore-content-length", false);

        FtpRun run = await RunAsync(diagnostics, Typed + "213 10\r\n" + Opened + Bye, "0123456789", c => new TransferContext { Url = c.Url, Output = c.Output, Http = new HttpRequestOptions() });

        string expectedSent = TypeSent + "SIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    private static async Task<FtpRun> RunAsync(TestDiagnostics diagnostics, string replies, string data, Func<TransferContext, TransferContext> adjust)
    {
        diagnostics.ArrangeFtp(Url, replies, data);
        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, data, adjust);
        diagnostics.ActRun(run);
        return run;
    }
}
