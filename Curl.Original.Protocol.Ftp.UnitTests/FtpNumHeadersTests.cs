using System.Net;
using System.Text;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins the header count <c>%{num_headers}</c> reads from an <c>ftp://</c> transfer's report
/// (BL-5403): <see cref="TransferReport.PseudoHeaders" /> plus <see cref="TransferReport.ResponseHeaders" />.
/// </summary>
[TestClass]
public sealed class FtpNumHeadersTests
{
    private const string FileUrl = "ftp://127.0.0.1:18321/f.txt";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private static int NumHeaders(TransferReport? report) => report!.ResponseHeaders.Count + report.PseudoHeaders.Count;

    private static TransferContext Head(TransferContext c) =>
        new() { Url = c.Url, Output = c.Output, NoBody = true, RemoteTime = true, HeaderOutput = c.Output };

    [TestMethod]
    public async Task ExecuteAsync_HeadOfAFileWithSizeAndMdtm_CountsThreeHeaders()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl, LoggedIn + "213 20200101000000\r\n200 Type set\r\n213 10\r\n350 Restarting at 0\r\n221 Bye\r\n", adjust: Head);

        Assert.AreEqual(3, NumHeaders(run.Report));
        StringAssert.Contains(run.OutputText, "Content-Length: 10\r\n");
    }

    /// <summary>
    /// STD-223 (BL-6020): measured 2026-11-26 with <c>Record-CurlExchange.ps1 -Ftp</c>, curl 8.22.0
    /// prints <c>0</c> for a download, an upload and a listing when no reply holds a colon.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PlainDownloadWithNoColonInAnyReply_CountsNoHeaders()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl,
            LoggedIn + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 7\r\n150 Opening\r\n226 Transfer complete\r\n221 Bye\r\n",
            "hello\r\n");

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(0, NumHeaders(run.Report));
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingWithNoColonInAnyReply_CountsNoHeaders()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/",
            LoggedIn + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n150 Opening\r\n226 Transfer complete\r\n221 Bye\r\n",
            "hello\r\n");

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(0, NumHeaders(run.Report));
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainDownloadWithOneReplyHoldingAColon_CountsOneHeader()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl,
            LoggedIn + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 7\r\n150 Opening\r\n226 done: ok\r\n221 Bye\r\n",
            "hello\r\n");

        Assert.AreEqual(1, NumHeaders(run.Report));
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainDownloadWithColonsInReplies_CountsEveryReplyLineHoldingAColon()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl,
            "220 hello: ready\r\n331 pass: please\r\n230 login: ok\r\n257 \"/\" is cwd\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 type: x\r\n213 7\r\n150 opening: data\r\n226 done\r\n221 Bye\r\n",
            "hello\r\n");

        Assert.AreEqual(5, NumHeaders(run.Report));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithColonsInReplies_CountsRepliesAndWrittenLines()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl, "220 hello: ready\r\n331 pass: please\r\n230 login: ok\r\n257 \"/\" is cwd\r\n213 20200101000000\r\n200 type: x\r\n213 10\r\n350 rest: 0\r\n221 Bye\r\n", adjust: Head);

        Assert.AreEqual(8, NumHeaders(run.Report));
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithNoSizeNorMdtm_CountsNoMoreThanTheLinesWritten()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl, LoggedIn + "550 no mdtm\r\n200 Type set\r\n550 no size\r\n500 no rest\r\n221 Bye\r\n", adjust: Head);

        Assert.AreEqual(0, NumHeaders(run.Report));
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedLogin_CountsNoHeaders()
    {
        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, "220 hi\r\n530 no\r\n");

        Assert.AreEqual(0, NumHeaders(run.Report));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithNoColonInAnyReply_CountsNoHeaders()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl,
            LoggedIn + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n150 Opening\r\n226 Transfer complete\r\n221 Bye\r\n",
            adjust: c => new TransferContext { Url = c.Url, Output = c.Output, Upload = new MemoryStream([1, 2, 3]) });

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(0, NumHeaders(run.Report));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithColonsInReplies_CountsEveryReplyLineHoldingAColon()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            FileUrl,
            "220 hello: ready\r\n331 pass: please\r\n230 login: ok\r\n257 \"/\" is cwd\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 type: x\r\n150 opening: data\r\n226 done\r\n221 Bye\r\n",
            adjust: c => new TransferContext { Url = c.Url, Output = c.Output, Upload = new MemoryStream([1, 2, 3]) });

        Assert.AreEqual(5, NumHeaders(run.Report));
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedDownloadWithColonsInReplies_CountsTheReplyLinesHoldingAColon()
    {
        FtpRun run = await FtpRun.ExecuteAsync(FileUrl, "220 hi: there\r\n530 no: way\r\n");

        Assert.AreEqual(2, NumHeaders(run.Report));
    }

    /// <summary>
    /// STD-111 (BL-6016): <c>-m</c> passing during the passive data connect cancels the transfer, and the
    /// cancellation carries the report the engine reads. Measured 2026-11-26 with
    /// <c>Record-CurlExchange.ps1 -Ftp</c>: curl 8.22.0 prints <c>[6|227]</c> for these six replies.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_CancelledDuringTheDataConnect_CarriesTheReplyCountAndLastCodeOnTheCancellation()
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(
            "220 hi: x\r\n331 pw: x\r\n230 ok: x\r\n257 \"/\" is: cwd\r\n500 no: refused\r\n227 Entering: (10,255,255,1,10,10)\r\n"));
        var handler = new FtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(control)),
            new CancellingDataConnector(),
            new QueuedListener(),
            new QueuedTlsProvider(),
            new NamedDnsResolver(new Dictionary<string, IPAddress[]>()),
            new NamedNetworkInterfaceLookup(new Dictionary<string, IPAddress[]>()));
        var context = new TransferContext { Url = CurlUrl.Parse(FileUrl), Output = new MemoryStream(), FtpSkipPasvIp = false };

        OperationCanceledException cancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await handler.ExecuteAsync(context));

        var report = (TransferReport)cancelled.Data["Curl.Engine.TransferReport"]!;
        Assert.AreEqual(227, report.ResponseCode);
        Assert.AreEqual(6, NumHeaders(report));
        Assert.AreEqual("/", report.FtpEntryPath);
    }

    /// <summary>A data connector whose dial ends as a cancelled one does.</summary>
    private sealed class CancellingDataConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new OperationCanceledException();
    }
}
