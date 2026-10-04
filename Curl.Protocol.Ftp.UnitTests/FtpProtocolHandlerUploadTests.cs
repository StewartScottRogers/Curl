using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>-T</c> over <c>ftp://</c> against curl 8.21.0: the commands sent on the control
/// connection, the bytes written to the data connection, and the exit code and message of
/// each outcome. Every case was recorded from real curl on 2026-09-27 with
/// <c>Record-CurlExchange.ps1 -Ftp</c> uploading the twelve bytes <c>hello world\n</c>
/// (BL-439, ADR-0323's BL-439 addendum) and is replayed here with the recorder's replies.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerUploadTests
{
    private const string Url = "ftp://127.0.0.1:18439/f.txt";

    private const string Upload = "hello world\n";

    /// <summary>The recording server's replies from the greeting through <c>PWD</c>.</summary>
    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string DirectoryChanged = "250 OK\r\n";

    /// <summary>Its replies to <c>EPSV</c> and <c>TYPE I</c>.</summary>
    private const string Passive = "229 Entering Extended Passive Mode (|||57697|)\r\n200 Type set\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string LogInSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    /// <summary>What curl sent from <c>EPSV</c> through <c>TYPE I</c>.</summary>
    private const string PassiveSent = "EPSV\r\nTYPE I\r\n";

    [TestMethod]
    public async Task ExecuteAsync_UploadToAFileUrl_ChangesDirectoriesThenStoresTheUpload()
    {
        // curl -T up.txt ftp://127.0.0.1:18439/dir/sub/file.txt
        var progress = new RecordingProgress();
        FtpRun run = await RunAsync(
            "ftp://127.0.0.1:18439/dir/sub/file.txt",
            LoggedIn + DirectoryChanged + DirectoryChanged + Passive + Opened + Complete + Bye,
            Seekable(Upload),
            progress: progress);

        Assert.AreEqual(LogInSent + "CWD dir\r\nCWD sub\r\n" + PassiveSent + "STOR file.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
        Assert.IsTrue(run.Data.IsDisposed);
        Assert.AreEqual(TransferResult.Success(12), run.Result);
        Assert.AreEqual(string.Empty, run.OutputText);
        Assert.IsTrue(progress.Started);
        CollectionAssert.AreEqual(new List<(long, long?)> { (12, 12) }, progress.Uploaded);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadToADirectoryUrlWithTheFileNameAppended_StoresUnderThatName()
    {
        // curl -T up.txt ftp://127.0.0.1:18439/dir/ - curl appends the file name before the
        // handler sees the URL, so the handler receives .../dir/up.txt.
        FtpRun run = await RunAsync(
            "ftp://127.0.0.1:18439/dir/up.txt",
            LoggedIn + DirectoryChanged + Passive + Opened + Complete + Bye,
            Seekable(Upload));

        Assert.AreEqual(LogInSent + "CWD dir\r\n" + PassiveSent + "STOR up.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(12), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyUpload_StoresNothingAndSucceeds()
    {
        // curl -T empty.txt ftp://127.0.0.1:18439/dir/e.txt
        FtpRun run = await RunAsync(
            "ftp://127.0.0.1:18439/dir/e.txt",
            LoggedIn + DirectoryChanged + Passive + Opened + Complete + Bye,
            Seekable(string.Empty));

        Assert.AreEqual(LogInSent + "CWD dir\r\n" + PassiveSent + "STOR e.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(0, run.Data.Sent.Length);
        Assert.IsTrue(run.Data.IsDisposed);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadToAUrlWithoutAFileName_FailsWithExit3BeforeCwdAndWithoutQuit()
    {
        // curl -T - ftp://127.0.0.1:18439/dir/ - standard input has no name to append.
        FtpRun run = await RunAsync("ftp://127.0.0.1:18439/dir/", LoggedIn, new ForwardOnlyStream(Encoding.Latin1.GetBytes("abc")));

        Assert.AreEqual(LogInSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, "Uploading to a URL without a filename"), run.Result);
        Assert.HasCount(1, run.Connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_StorRefusedWith553_FailsWithExit25AfterQuit()
    {
        // curl -T up.txt ftp://127.0.0.1:18439/dir/f.txt, STOR answered 553.
        FtpRun run = await RunAsync(
            "ftp://127.0.0.1:18439/dir/f.txt",
            LoggedIn + DirectoryChanged + Passive + "553 Not allowed\r\n" + Bye,
            Seekable(Upload));

        Assert.AreEqual(LogInSent + "CWD dir\r\n" + PassiveSent + "STOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(0, run.Data.Sent.Length);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UploadFailed, "Failed FTP upload: 553"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StorRefusedWith550_FailsWithExit25AfterQuit()
    {
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + "550 No\r\n" + Bye, Seekable(Upload));

        Assert.AreEqual(LogInSent + PassiveSent + "STOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UploadFailed, "Failed FTP upload: 550"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CwdRefusedDuringUpload_FailsWithExit9AfterQuitAndSendsNoMkd()
    {
        // curl -T up.txt ftp://127.0.0.1:18439/dir/f.txt, CWD answered 550: no MKD without
        // --ftp-create-dirs.
        FtpRun run = await RunAsync(
            "ftp://127.0.0.1:18439/dir/f.txt",
            LoggedIn + "550 No such dir\r\n" + Bye,
            Seekable(Upload));

        Assert.AreEqual(LogInSent + "CWD dir\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Server denied you to change to the given directory"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EndOfUploadAnswered451_FailsWithExit18AfterQuit()
    {
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Opened + "451 Local error\r\n" + Bye, Seekable(Upload));

        Assert.AreEqual(LogInSent + PassiveSent + "STOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(CurlExitCode.PartialFile, run.Result.ExitCode);
        Assert.AreEqual("server did not report OK, got 451", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_EndOfUploadAnswered250_Succeeds()
    {
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Opened + "250 ok\r\n" + Bye, Seekable(Upload));

        Assert.AreEqual(LogInSent + PassiveSent + "STOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(12), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtAnOffset_SkipsThoseBytesAndSendsAppe()
    {
        // curl -C 5 -T up.txt: no SIZE; APPE with the seven bytes past the offset.
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Opened + Complete + Bye, Seekable(Upload), resumeFrom: 5);

        Assert.AreEqual(LogInSent + PassiveSent + "APPE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(" world\n", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(7), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtZero_SendsStor()
    {
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Opened + Complete + Bye, Seekable(Upload), resumeFrom: 0);

        Assert.AreEqual(LogInSent + PassiveSent + "STOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(12), run.Result);
    }

    [TestMethod]
    [DataRow(12L, DisplayName = "-C 12, the file's length")]
    [DataRow(20L, DisplayName = "-C 20, past the file's end")]
    public async Task ExecuteAsync_ContinueAtOrPastTheEnd_SendsOnlyQuitAndSucceeds(long offset)
    {
        // curl -C 12 -T up.txt and -C 20 -T up.txt: "File already completely uploaded", exit 0.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Bye, Seekable(Upload), resumeFrom: offset, events: events);

        Assert.AreEqual(LogInSent + PassiveSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(0, run.Data.Sent.Length);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.Contains("File already completely uploaded", events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_EndOfUploadAnswered552_FailsWithExit70AfterQuit()
    {
        // curl 8.21.0 ftp_done: 552 is "Exceeded storage allocation", CURLE_REMOTE_DISK_FULL.
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Opened + "552 Quota exceeded\r\n" + Bye, Seekable(Upload));

        Assert.AreEqual(LogInSent + PassiveSent + "STOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(CurlExitCode.RemoteDiskFull, run.Result.ExitCode);
        Assert.AreEqual("Exceeded storage allocation", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueAtAnOffsetIntoAnEmptyUpload_AppendsNothing()
    {
        // curl -C 5 -T empty.txt: APPE with no bytes, exit 0.
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Opened + Complete + Bye, Seekable(string.Empty), resumeFrom: 5);

        Assert.AreEqual(LogInSent + PassiveSent + "APPE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(0, run.Data.Sent.Length);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow(5L, DisplayName = "-C 5 -T -")]
    [DataRow(20L, DisplayName = "-C 20 -T -")]
    public async Task ExecuteAsync_ContinueAtAnOffsetFromStandardInput_AppendsTheWholeInput(long offset)
    {
        // curl -C 5 -T - and -C 20 -T - with hello world\n on standard input: nothing skipped.
        var progress = new RecordingProgress();
        FtpRun run = await RunAsync(
            Url,
            LoggedIn + Passive + Opened + Complete + Bye,
            new ForwardOnlyStream(Encoding.Latin1.GetBytes(Upload)),
            resumeFrom: offset,
            progress: progress);

        Assert.AreEqual(LogInSent + PassiveSent + "APPE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(12), run.Result);
        CollectionAssert.AreEqual(new List<(long, long?)> { (12, null) }, progress.Uploaded);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueFromTheServersSize_SendsSizeThenAppendsThePartPastIt()
    {
        // curl -C - -T up.txt, SIZE answered 213 3: APPE with the nine bytes past it.
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + "213 3\r\n" + Opened + Complete + Bye, Seekable(Upload), fromUnknownOffset: true);

        Assert.AreEqual(LogInSent + PassiveSent + "SIZE f.txt\r\nAPPE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("lo world\n", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(9), run.Result);
    }

    [TestMethod]
    [DataRow("550 no file", DisplayName = "SIZE refused")]
    [DataRow("213 0", DisplayName = "SIZE 0")]
    public async Task ExecuteAsync_ContinueFromTheServersSizeWithNothingThere_SendsSizeThenStor(string sizeReply)
    {
        // curl -C - -T up.txt, SIZE answered 550 or 213 0: STOR with the whole file.
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + sizeReply + "\r\n" + Opened + Complete + Bye, Seekable(Upload), fromUnknownOffset: true);

        Assert.AreEqual(LogInSent + PassiveSent + "SIZE f.txt\r\nSTOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(Upload, Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(12), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinueFromTheServersSizeCoveringTheFile_SendsOnlyQuit()
    {
        // curl -C - -T up.txt, SIZE answered 213 12 (and 213 20 alike): no upload, exit 0.
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + "213 12\r\n" + Bye, Seekable(Upload), resumeFrom: 3, fromUnknownOffset: true);

        Assert.AreEqual(LogInSent + PassiveSent + "SIZE f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnectionRefusesTheUpload_FailsWithExit55()
    {
        var data = new ScriptedConnection { WritesBeforeFailure = 0 };
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Passive + Opened + Complete + Bye)),
            data,
            c => new TransferContext { Url = c.Url, Output = c.Output, Upload = Seekable(Upload) });

        Assert.AreEqual(LogInSent + PassiveSent + "STOR f.txt\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer"), run.Result);
        Assert.IsTrue(data.IsDisposed);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_DataConnectionResetMidUpload_FailsWithExit55ConnectionWasResetCountingTheBytesSent()
    {
        var data = ResettingDataConnection();
        FtpRun run = await RunResetMidUploadAsync(data);

        Assert.AreEqual(16384, data.Sent.Length);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Send failure: Connection was reset", 16384), run.Result);
        Assert.IsTrue(data.IsDisposed);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_DataConnectionResetMidUploadOffWindows_FailsWithExit55AndTheErrorsOwnMessageCountingTheBytesSent()
    {
        var data = ResettingDataConnection();
        FtpRun run = await RunResetMidUploadAsync(data);

        string words = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset).Message;
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Send failure: " + words, 16384), run.Result);
        Assert.IsTrue(data.IsDisposed);
    }

    // The first 16384-byte chunk is sent; the second write is reset.
    private static ScriptedConnection ResettingDataConnection() => new()
    {
        WritesBeforeFailure = 1,
        WriteFailure = new IOException("reset", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset)),
    };

    private static Task<FtpRun> RunResetMidUploadAsync(ScriptedConnection data)
    {
        string upload = new('x', 16384 + 12);
        return FtpRun.ExecuteAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Passive + Opened + Complete + Bye)),
            data,
            c => new TransferContext { Url = c.Url, Output = c.Output, Upload = Seekable(upload) });
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadSourceFailsToRead_EndsTheUploadAsItsEnd()
    {
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Opened + Complete + Bye, new ReadFailingStream());

        Assert.AreEqual(LogInSent + PassiveSent + "STOR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(0, run.Data.Sent.Length);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadLargerThanOneRead_SendsEveryByte()
    {
        string large = new('x', 40000);
        var progress = new RecordingProgress();
        FtpRun run = await RunAsync(Url, LoggedIn + Passive + Opened + Complete + Bye, Seekable(large), progress: progress);

        Assert.AreEqual(large, Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(40000), run.Result);
        Assert.AreEqual((40000L, (long?)40000), progress.Uploaded[^1]);
    }

    private static MemoryStream Seekable(string content) => new(Encoding.Latin1.GetBytes(content));

    private static Task<FtpRun> RunAsync(
        string url,
        string replies,
        Stream upload,
        long? resumeFrom = null,
        bool fromUnknownOffset = false,
        RecordingProgress? progress = null,
        RecordingTransferEvents? events = null) =>
        FtpRun.ExecuteAsync(
            url,
            replies,
            adjust: c => new TransferContext
            {
                Url = c.Url,
                Output = c.Output,
                Upload = upload,
                ResumeFrom = resumeFrom,
                ResumeUploadFromUnknownOffset = fromUnknownOffset,
                Progress = (ITransferProgress?)progress ?? NoTransferProgress.Instance,
                Events = (ITransferEvents?)events ?? NoTransferEvents.Instance,
            });
}
