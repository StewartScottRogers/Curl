using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins an <c>ftp://</c> download whose <c>-r</c> text names no range
/// (<see cref="ITransferContext.RangeText" /> set, <see cref="ITransferContext.Range" />
/// <see langword="null" />) against curl 8.21.0, measured 2026-10-03 with
/// <c>Record-CurlExchange.ps1 -Ftp -CurlArgs '-sv','-r','5-2',...</c> (BL-1333): it logs in,
/// sends <c>PWD</c> and <c>EPSV</c>, opens the data connection, then sends no <c>TYPE</c>,
/// <c>SIZE</c> or <c>RETR</c> and exits 0 with nothing written; <c>abc</c> and <c>-0</c>
/// end the same, and so does a listing.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerUnparsedRangeTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:18733/f.txt";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Passive = "229 Entering Extended Passive Mode (|||53563|)\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string LoginSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    [TestMethod]
    [DataRow("5-2", DisplayName = "last before first")]
    [DataRow("abc", DisplayName = "no dash")]
    [DataRow("-0", DisplayName = "empty suffix")]
    public async Task ExecuteAsync_RangeTextNamingNoRange_EndsAfterTheDataConnectionOpensWithExit0(string rangeText)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("rangeText", rangeText);

        var events = new RecordingTransferEvents();

        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Passive + Bye,
            "0123456789",
            c => new TransferContext { Url = c.Url, Output = c.Output, RangeText = rangeText, Events = events });
        diagnostics.ActRun(run);

        diagnostics.DiffSent(LoginSent + "EPSV\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LoginSent + "EPSV\r\nQUIT\r\n", run.Sent);
        diagnostics.Assert("connect targets", 2, run.Connector.Targets.Count);
        Assert.HasCount(2, run.Connector.Targets);
        Assert.AreEqual(53563, run.Connector.Targets[1].Port);
        diagnostics.Diff("output", string.Empty, run.OutputText);
        Assert.AreEqual(string.Empty, run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(0), run.Result);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        diagnostics.Assert("result", CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "Remembering we are in directory \"\"", "Connection #0 to host 127.0.0.1:18733 left intact" },
            events.Info.TakeLast(2).ToArray());
        diagnostics.Assert("matching info lines", 0, events.Info.Count(line => line.Contains("Requested range was not delivered", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.Contains("Requested range was not delivered", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingWithRangeTextNamingNoRange_EndsBeforeList()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -sv -r 5-2 ftp://127.0.0.1:port/ - measured the same as a file.
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18733/");
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18733/",
            LoggedIn + Passive + Bye,
            "f.txt\r\n",
            c => new TransferContext { Url = c.Url, Output = c.Output, RangeText = "5-2" });
        diagnostics.ActRun(run);

        diagnostics.DiffSent(LoginSent + "EPSV\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LoginSent + "EPSV\r\nQUIT\r\n", run.Sent);
        diagnostics.Diff("output", string.Empty, run.OutputText);
        Assert.AreEqual(string.Empty, run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(0), run.Result);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeTextWithItsParsedRange_DownloadsTheWindow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -r 2-3: REST 2, two bytes kept, then ABOR.
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Passive + "200 Type set\r\n213 10\r\n350 Restarting\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n502 Command not implemented\r\n" + Bye,
            "23456789",
            c => new TransferContext { Url = c.Url, Output = c.Output, RangeText = "2-3", Range = ByteRange.Bounded(2, 3) });
        diagnostics.ActRun(run);

        diagnostics.DiffSent(LoginSent + "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nREST 2\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LoginSent + "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nREST 2\r\nRETR f.txt\r\nABOR\r\nQUIT\r\n", run.Sent);
        diagnostics.Diff("output", "23", run.OutputText);
        Assert.AreEqual("23", run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(2), run.Result);
        Assert.AreEqual(TransferResult.Success(2), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoRangeText_DownloadsTheWholeFile()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + Passive + "200 Type set\r\n213 10\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n" + Bye,
            "0123456789");
        diagnostics.ActRun(run);

        diagnostics.DiffSent(LoginSent + "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(LoginSent + "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        diagnostics.Diff("output", "0123456789", run.OutputText);
        Assert.AreEqual("0123456789", run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(10), run.Result);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }
}
