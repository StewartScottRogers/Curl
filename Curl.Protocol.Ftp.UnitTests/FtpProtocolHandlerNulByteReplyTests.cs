using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

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
    private const string Url = "ftp://127.0.0.1:18321/file.txt";

    private static readonly TransferResult NulByteFailure =
        TransferResult.Failure(CurlExitCode.WeirdServerReply, "Nul byte in server response line");

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheGreeting_FailsWithExit8AndReportsNothing()
    {
        // curl -v ftp://..., greeting "220 hel\0lo": exit 8, no "<" line, no QUIT.
        var events = new RecordingTransferEvents();
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220 hel\0lo\r\n", adjust: c => WithEvents(c, events));

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual(string.Empty, run.Sent);
        Assert.IsEmpty(events.Headers);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheReplyToUser_FailsWithExit8AfterUserAndSendsNoQuit()
    {
        // curl -v ftp://..., greeting "220 hi", USER answered "331 pa\0ss": exit 8, no QUIT.
        var events = new RecordingTransferEvents();
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220 hi\r\n331 pa\0ss\r\n", adjust: c => WithEvents(c, events));

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        CollectionAssert.AreEqual(new[] { "< 220 hi\r\n", "> USER anonymous\r\n" }, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInAGreetingContinuationLine_FailsWithExit8()
    {
        var events = new RecordingTransferEvents();
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220-wel\0come\r\n220 ready\r\n", adjust: c => WithEvents(c, events));

        Assert.AreEqual(NulByteFailure, run.Result);
        Assert.AreEqual(string.Empty, run.Sent);
        Assert.IsEmpty(events.Headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInDownloadedData_IsWrittenUnchanged()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            "220 hi\r\n331 Password required\r\n230 Logged in\r\n257 \"/\"\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 5\r\n150 Opening\r\n226 Transfer complete\r\n221 Bye\r\n",
            "ab\0cd");

        Assert.AreEqual("ab\0cd", run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInTheReplyToQuit_StillSucceeds()
    {
        // Decided (BL-1117): as with an oversized one, an unreadable reply to QUIT changes nothing.
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            "220 hi\r\n331 Password required\r\n230 Logged in\r\n257 \"/\"\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 1\r\n150 Opening\r\n226 Transfer complete\r\n221 B\0ye\r\n",
            "x");

        StringAssert.EndsWith(run.Sent, "QUIT\r\n", StringComparison.Ordinal);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    private static TransferContext WithEvents(TransferContext context, ITransferEvents events) =>
        new() { Url = context.Url, Output = context.Output, Events = events };
}
