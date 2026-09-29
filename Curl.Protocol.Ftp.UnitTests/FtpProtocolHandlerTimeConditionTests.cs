using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>-z</c> and <c>-R</c> on <c>ftp://</c> against curl 8.21.0: when <c>MDTM</c> is
/// sent, what an unmet condition sends and returns, the time <c>-R</c> reports and the
/// <c>-v</c> lines. Every case was recorded from real curl on 2026-09-29 with
/// <c>Record-CurlExchange.ps1 -Ftp -FtpData hello</c> against
/// <c>ftp://127.0.0.1:port/dir/f.txt</c>, whose <c>MDTM</c> is answered
/// <c>213 20260927123456</c> (BL-637), and is replayed here with the recorder's replies.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerTimeConditionTests
{
    private const string Url = "ftp://127.0.0.1:52137/dir/f.txt";

    /// <summary>The recording server's replies from the greeting through <c>CWD dir</c>.</summary>
    private const string InDirectory = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n250 OK\r\n";

    private const string Modified = "213 20260927123456\r\n";

    /// <summary>Its replies from <c>EPSV</c> through <c>QUIT</c> for the five-byte file.</summary>
    private const string Retrieved = "229 Entering Extended Passive Mode (|||59770|)\r\n200 Type set\r\n213 5\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n" + Bye;

    private const string Bye = "221 Bye\r\n";

    private const string InDirectorySent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD dir\r\n";

    private const string MdtmSent = InDirectorySent + "MDTM f.txt\r\n";

    private const string RetrieveSent = "EPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n";

    private const string Hello = "hello";

    /// <summary>The time <c>213 20260927123456</c> names.</summary>
    private static readonly DateTimeOffset ModifiedUtc = new(2026, 9, 27, 12, 34, 56, TimeSpan.Zero);

    private static readonly DateTimeOffset Older = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Newer = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ExecuteAsync_IfModifiedSinceAnOlderDate_SendsMdtmThenTransfers()
    {
        // curl -z 20200101
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(InDirectory + Modified + Retrieved, c => With(c, IfModifiedSince(Older), events: events));

        Assert.AreEqual(MdtmSent + RetrieveSent, run.Sent);
        Assert.AreEqual(Hello, run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "-z 20300101")]
    [DataRow(true, DisplayName = "-z 'Sun, 27 Sep 2026 12:34:56 GMT', the MDTM time itself")]
    public async Task ExecuteAsync_IfModifiedSinceANewerOrEqualDate_QuitsAfterMdtmWithNoBody(bool equal)
    {
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(InDirectory + Modified + Bye, c => With(c, IfModifiedSince(equal ? ModifiedUtc : Newer), events: events));

        Assert.AreEqual(MdtmSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual("", run.OutputText);
        Assert.AreEqual(TransferResult.TimeConditionNotMet(), run.Result);
        Assert.IsTrue(run.Result.TimeConditionUnmet);
        Assert.AreEqual(213, run.Report!.ResponseCode);
        Assert.HasCount(1, run.Connector.Targets);
        CollectionAssert.AreEqual(new[] { "The requested document is not new enough" }, events.Info);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "-z -20300101")]
    [DataRow(true, DisplayName = "-z '-Sun, 27 Sep 2026 12:34:56 GMT', the MDTM time itself")]
    public async Task ExecuteAsync_IfUnmodifiedSinceANewerOrEqualDate_Transfers(bool equal)
    {
        FtpRun run = await RunAsync(InDirectory + Modified + Retrieved, c => With(c, IfUnmodifiedSince(equal ? ModifiedUtc : Newer)));

        Assert.AreEqual(MdtmSent + RetrieveSent, run.Sent);
        Assert.AreEqual(Hello, run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_IfUnmodifiedSinceAnOlderDate_QuitsAfterMdtmWithNoBody()
    {
        // curl -z -20200101
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(InDirectory + Modified + Bye, c => With(c, IfUnmodifiedSince(Older), events: events));

        Assert.AreEqual(MdtmSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.TimeConditionNotMet(), run.Result);
        CollectionAssert.AreEqual(new[] { "The requested document is not old enough" }, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_RemoteTime_SendsMdtmAndReportsItsTime()
    {
        // curl -R -o out: out's last-write time became 2026-09-27T12:34:56Z.
        FtpRun run = await RunAsync(InDirectory + Modified + Retrieved, c => With(c, remoteTime: true));

        Assert.AreEqual(MdtmSent + RetrieveSent, run.Sent);
        Assert.AreEqual(Hello, run.OutputText);
        Assert.AreEqual(TransferResult.Success(5, ModifiedUtc), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RemoteTimeWithFractionalSeconds_ReportsTheWholeSeconds()
    {
        // curl -R -o out, MDTM answered 213 20260927123456.789: out stamped 12:34:56.
        FtpRun run = await RunAsync(InDirectory + "213 20260927123456.789\r\n" + Retrieved, c => With(c, remoteTime: true));

        Assert.AreEqual(TransferResult.Success(5, ModifiedUtc), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_IfModifiedSinceASecondBeforeAFractionalTime_Transfers()
    {
        // curl -z 'Sun, 27 Sep 2026 12:34:55 GMT', MDTM answered 213 20260927123456.789.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(InDirectory + "213 20260927123456.789\r\n" + Retrieved, c => With(c, IfModifiedSince(ModifiedUtc.AddSeconds(-1)), events: events));

        Assert.AreEqual(MdtmSent + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_RemoteTimeWithAnUnmetCondition_ReportsTheTimeWithNoBody()
    {
        // curl -R -z 20300101 -o out: no out is created.
        FtpRun run = await RunAsync(InDirectory + Modified + Bye, c => With(c, IfModifiedSince(Newer), remoteTime: true));

        Assert.AreEqual(MdtmSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.TimeConditionNotMet(ModifiedUtc), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RemoteTimeWithMdtmRefused_TransfersWithNoTime()
    {
        // curl -R -o out, MDTM answered 550: exit 0, out keeps the time it was written.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(InDirectory + "550 No such file\r\n" + Retrieved, c => With(c, remoteTime: true, events: events));

        Assert.AreEqual(MdtmSent + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        CollectionAssert.AreEqual(new[] { "MDTM failed: file does not exist or permission problem, continuing" }, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_RemoteTimeWhenTheTransferFails_ReportsNoTime()
    {
        FtpRun run = await RunAsync(
            InDirectory + Modified + "229 Entering Extended Passive Mode (|||59770|)\r\n200 Type set\r\n550 No such file\r\n" + Bye,
            c => With(c, remoteTime: true));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "The file does not exist"), run.Result);
    }

    [TestMethod]
    [DataRow("550 No such file", "MDTM failed: file does not exist or permission problem, continuing", DisplayName = "MDTM refused")]
    [DataRow("500 Unknown command", "unsupported MDTM reply format", DisplayName = "MDTM not understood")]
    [DataRow("213 garbage", null, DisplayName = "MDTM without a timestamp")]
    [DataRow("213 2026092712345", null, DisplayName = "MDTM with a thirteen-digit timestamp")]
    [DataRow("213 20261327123456", null, DisplayName = "MDTM with month 13")]
    [DataRow("213 19700101000000", null, DisplayName = "MDTM at the Unix epoch")]
    [DataRow("213 19690101000000", null, DisplayName = "MDTM before the Unix epoch")]
    public async Task ExecuteAsync_ConditionWithoutAUsableTime_SkipsTheComparisonAndTransfers(string mdtmReply, string? replyLine)
    {
        // curl -z 20300101: every one of these transfers, exit 0.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(InDirectory + mdtmReply + "\r\n" + Retrieved, c => With(c, IfModifiedSince(Newer), events: events));

        Assert.AreEqual(MdtmSent + RetrieveSent, run.Sent);
        Assert.AreEqual(Hello, run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        string[] expected = replyLine is null ? ["Skipping time comparison"] : [replyLine, "Skipping time comparison"];
        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConditionAtTheUnixEpoch_SkipsTheComparisonAndTransfers()
    {
        // curl -z 19700101
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(InDirectory + Modified + Retrieved, c => With(c, IfModifiedSince(DateTimeOffset.UnixEpoch), events: events));

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        CollectionAssert.AreEqual(new[] { "Skipping time comparison" }, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConditionOnADirectoryListing_SendsNoMdtm()
    {
        // curl -z 20300101 ftp://127.0.0.1:port/dir/
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:52137/dir/",
            InDirectory + "229 Entering Extended Passive Mode (|||55619|)\r\n200 Type set\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n" + Bye,
            Hello,
            c => With(c, IfModifiedSince(Newer), remoteTime: true));

        Assert.AreEqual(InDirectorySent + "EPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Hello, run.OutputText);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnmetConditionOnAListOnlyFileUrl_QuitsAfterMdtm()
    {
        // curl -l -z 20300101
        FtpRun run = await RunAsync(InDirectory + Modified + Bye, c => With(c, IfModifiedSince(Newer), listOnly: true));

        Assert.AreEqual(MdtmSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.TimeConditionNotMet(), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithAnUnmetCondition_WritesLastModifiedThenQuits()
    {
        // curl -I -z 20300101
        FtpRun run = await RunAsync(InDirectory + Modified + Bye, c => With(c, IfModifiedSince(Newer), head: true));

        Assert.AreEqual(MdtmSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual("Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\n", run.OutputText);
        Assert.AreEqual(TransferResult.TimeConditionNotMet(), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadWithAMetCondition_SendsMdtmOnce()
    {
        // curl -I -z 20200101
        FtpRun run = await RunAsync(
            InDirectory + Modified + "200 Type set\r\n213 5\r\n350 Restarting at 0\r\n" + Bye,
            c => With(c, IfModifiedSince(Older), head: true));

        Assert.AreEqual(MdtmSent + "TYPE I\r\nSIZE f.txt\r\nREST 0\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\nContent-Length: 5\r\nAccept-ranges: bytes\r\n", run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithAnUnmetCondition_QuitsAfterMdtmWithoutStoring()
    {
        // curl -T up.txt -z 20300101
        FtpRun run = await RunAsync(InDirectory + Modified + Bye, c => With(c, IfModifiedSince(Newer), upload: "up"));

        Assert.AreEqual(MdtmSent + "QUIT\r\n", run.Sent);
        Assert.IsEmpty(run.Data.Sent);
        Assert.AreEqual(TransferResult.TimeConditionNotMet(), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithAMetCondition_StoresAfterMdtm()
    {
        // curl -T up.txt -z 20200101
        FtpRun run = await RunAsync(
            InDirectory + Modified + "229 Entering Extended Passive Mode (|||50479|)\r\n200 Type set\r\n150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n" + Bye,
            c => With(c, IfModifiedSince(Older), upload: "up"));

        Assert.AreEqual(MdtmSent + "EPSV\r\nTYPE I\r\nSTOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("up", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(2), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnmetConditionWithQuotes_SendsOnlyTheAfterLoginAndPostTransferQuotes()
    {
        // curl -Q NOOP -Q +NOOP -Q -NOOP -z 20300101: +NOOP is never sent.
        FtpRun run = await RunAsync(
            "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n200 OK\r\n250 OK\r\n" + Modified + "200 OK\r\n" + Bye,
            c => With(c, IfModifiedSince(Newer), quotes: ["NOOP", "+NOOP", "-NOOP"]));

        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nNOOP\r\nCWD dir\r\nMDTM f.txt\r\nNOOP\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.TimeConditionNotMet(), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnmetConditionWithARefusedPostTransferQuote_FailsWithExit21()
    {
        // curl -Q -NOOP -z 20300101, NOOP answered 500: exit 21 after QUIT.
        FtpRun run = await RunAsync(InDirectory + Modified + "500 no\r\n" + Bye, c => With(c, IfModifiedSince(Newer), quotes: ["-NOOP"]));

        Assert.AreEqual(MdtmSent + "NOOP\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, "QUOT string not accepted: NOOP"), run.Result);
    }

    private static TimeCondition IfModifiedSince(DateTimeOffset value) => new(value, TimeConditionKind.IfModifiedSince);

    private static TimeCondition IfUnmodifiedSince(DateTimeOffset value) => new(value, TimeConditionKind.IfUnmodifiedSince);

    private static TransferContext With(
        TransferContext context,
        TimeCondition? condition = null,
        bool remoteTime = false,
        RecordingTransferEvents? events = null,
        bool listOnly = false,
        bool head = false,
        string? upload = null,
        string[]? quotes = null) =>
        new()
        {
            Url = context.Url,
            Output = context.Output,
            TimeCondition = condition,
            RemoteTime = remoteTime,
            Events = events ?? (ITransferEvents)NoTransferEvents.Instance,
            ListOnly = listOnly,
            NoBody = head,
            HeaderOutput = head ? context.Output : null,
            Upload = upload is null ? null : new MemoryStream(Encoding.Latin1.GetBytes(upload)),
            QuoteCommands = quotes ?? [],
        };

    private static Task<FtpRun> RunAsync(string replies, Func<TransferContext, TransferContext> adjust) =>
        FtpRun.ExecuteAsync(Url, replies, Hello, adjust);
}
