using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins a control-connection reply line holding a NUL byte against curl 8.21.0 (the Schannel
/// build), measured with <c>Record-CurlExchange.ps1 -Ftp</c> (BL-1117): exit 8
/// <c>Nul byte in server response line</c>, the line never reported to <c>-v</c>, and no
/// <c>QUIT</c>. Bytes on the data connection are not reply lines, so a NUL there is data.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerNulByteReplyTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:18321/file.txt";

    private static readonly TransferResult NulByteFailure =
        TransferResult.Failure(CurlExitCode.WeirdServerReply, "Nul byte in server response line");

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheGreeting_FailsWithExit8AndReportsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -v ftp://..., greeting "220 hel\0lo": exit 8, no "<" line, no QUIT.
        var events = new RecordingTransferEvents();
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220 hel\0lo\r\n", adjust: c => WithEvents(c, events));
        diagnostics.ActRun(run);

        diagnostics.Assert("result", NulByteFailure, run.Result);
        Assert.AreEqual(NulByteFailure, run.Result);
        diagnostics.DiffSent(string.Empty, run.Sent);
        Assert.AreEqual(string.Empty, run.Sent);
        diagnostics.Assert("events.Headers count", 0, events.Headers.Count);
        Assert.IsEmpty(events.Headers);
        diagnostics.Assert("events.Info count", 0, events.Info.Count);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheReplyToUser_FailsWithExit8AfterUserAndSendsNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -v ftp://..., greeting "220 hi", USER answered "331 pa\0ss": exit 8, no QUIT.
        var events = new RecordingTransferEvents();
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220 hi\r\n331 pa\0ss\r\n", adjust: c => WithEvents(c, events));
        diagnostics.ActRun(run);

        diagnostics.Assert("result", NulByteFailure, run.Result);
        Assert.AreEqual(NulByteFailure, run.Result);
        diagnostics.DiffSent("USER anonymous\r\n", run.Sent);
        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        diagnostics.Diff("lines", string.Join('\n', new[] { "< 220 hi\r\n", "> USER anonymous\r\n" }), string.Join('\n', events.Transcript));
        CollectionAssert.AreEqual(new[] { "< 220 hi\r\n", "> USER anonymous\r\n" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheReplyToPass_FailsWithExit8AfterPassAndSendsNoPwd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // Upstream test2108 (curl 8.21.0): PASS answered "230 logged\0 in": exit 8, USER and PASS sent, nothing after.
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220 hi\r\n331 pass\r\n230 logged\0 in\r\n");
        diagnostics.ActRun(run);

        diagnostics.Assert("result", NulByteFailure, run.Result);
        Assert.AreEqual(NulByteFailure, run.Result);
        diagnostics.DiffSent("USER anonymous\r\nPASS ftp@example.com\r\n", run.Sent);
        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInAGreetingContinuationLine_FailsWithExit8()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var events = new RecordingTransferEvents();
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220-wel\0come\r\n220 ready\r\n", adjust: c => WithEvents(c, events));
        diagnostics.ActRun(run);

        diagnostics.Assert("result", NulByteFailure, run.Result);
        Assert.AreEqual(NulByteFailure, run.Result);
        diagnostics.DiffSent(string.Empty, run.Sent);
        Assert.AreEqual(string.Empty, run.Sent);
        diagnostics.Assert("events.Headers count", 0, events.Headers.Count);
        Assert.IsEmpty(events.Headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInDownloadedData_IsWrittenUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            "220 hi\r\n331 Password required\r\n230 Logged in\r\n257 \"/\"\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 5\r\n150 Opening\r\n226 Transfer complete\r\n221 Bye\r\n",
            "ab\0cd");
        diagnostics.ActRun(run);

        diagnostics.Diff("output", "ab\0cd", run.OutputText);
        Assert.AreEqual("ab\0cd", run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheReplyToQuit_StillSucceeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // Decided (BL-1117): as with an oversized one, an unreadable reply to QUIT changes nothing.
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            "220 hi\r\n331 Password required\r\n230 Logged in\r\n257 \"/\"\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 1\r\n150 Opening\r\n226 Transfer complete\r\n221 B\0ye\r\n",
            "x");
        diagnostics.ActRun(run);

        StringAssert.EndsWith(run.Sent, "QUIT\r\n", StringComparison.Ordinal);
        diagnostics.Assert("result", TransferResult.Success(1), run.Result);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    private static TransferContext WithEvents(TransferContext context, ITransferEvents events) =>
        new() { Url = context.Url, Output = context.Output, Events = events };
}
