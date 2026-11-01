using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins wildcard downloads (BL-2478, ADR-0615) against upstream curl 8.22.0's
/// <c>wc_statemach</c> in <c>lib/ftp.c</c> and upstream tests 574 and 576: the directory is
/// listed with <c>LIST</c>, each match is handed to the chunk-begin hook and downloaded on the
/// same connection with no <c>CWD</c> and no <c>SIZE</c>, and the chunk-end hook follows each.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerWildcardTests
{
    private const string Url = "ftp://127.0.0.1:47380/d/*.txt";

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string LogInSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    private const string DirectoryChanged = "250 OK\r\n";

    private const string Passive = "229 Entering Extended Passive Mode (|||49783|)\r\n";

    private const string TypeSet = "200 Type set\r\n";

    private const string Opened = "150 Opening data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>The replies through the listing: <c>CWD d</c>, <c>EPSV</c>, <c>TYPE A</c> and <c>LIST</c>.</summary>
    private const string Listed = LoggedIn + DirectoryChanged + Passive + TypeSet + Opened + Complete;

    private const string ListedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE A\r\nLIST\r\n";

    /// <summary>A directory named like a match, three matching files and one that does not match.</summary>
    private const string Listing =
        "drwxr-xr-x 2 u g 4096 Jan 11 10:00 sub.txt\r\n"
        + "-rw-r--r-- 1 u g 3 Jan 11 10:00 a.txt\r\n"
        + "-rw-r--r-- 1 u g 2 Jan 11 10:00 b.txt\r\n"
        + "-rw-r--r-- 1 u g 4 Jan 11 10:00 c.txt\r\n"
        + "-rw-r--r-- 1 u g 9 Jan 11 10:00 notes.md\r\n";

    private static readonly string[] MatchCalls = ["match sub.txt", "match a.txt", "match b.txt", "match c.txt", "match notes.md"];

    [TestMethod]
    public async Task ExecuteAsync_ThreeFilesOneSkipped_DownloadsTheOtherTwoOnOneConnection()
    {
        var hooks = new RecordingWildcardHooks { BeginAnswer = entry => entry.FileName == "b.txt" ? FtpChunkBeginAnswer.Skip : FtpChunkBeginAnswer.Transfer };

        WildcardRun run = await RunAsync(
            hooks,
            Listed + Passive + TypeSet + Opened + Complete + Passive + Opened + Complete + Bye,
            [Listing, "abc", "wxyz"]);

        Assert.AreEqual(ListedSent + "EPSV\r\nTYPE I\r\nRETR a.txt\r\nEPSV\r\nRETR c.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("abcwxyz", run.OutputText);
        Assert.AreEqual(TransferResult.Success(4), run.Result);
        CollectionAssert.AreEqual(
            (string[])[.. MatchCalls, "begin sub.txt 4", "end", "begin a.txt 3", "end", "begin b.txt 2", "end", "begin c.txt 1", "end"],
            hooks.Calls);
        Assert.Contains("Wildcard - Parsing started", run.Events.Info);
        Assert.Contains("Wildcard - START of \"a.txt\"", run.Events.Info);
        Assert.Contains("Wildcard - \"b.txt\" skipped by user", run.Events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_BeginHookFails_EndsWithExit88AndNoQuit()
    {
        var hooks = new RecordingWildcardHooks { BeginAnswer = _ => FtpChunkBeginAnswer.Fail };

        WildcardRun run = await RunAsync(hooks, Listed + Bye, [Listing]);

        Assert.AreEqual(ListedSent, run.Sent);
        Assert.AreEqual(string.Empty, run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.ChunkFailed, "Chunk callback failed", Listing.Length), run.Result);
        CollectionAssert.AreEqual((string[])[.. MatchCalls, "begin sub.txt 4"], hooks.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_EndHookFails_IsIgnoredAsCurlIgnoresIt()
    {
        var hooks = new RecordingWildcardHooks { EndAnswer = FtpChunkEndAnswer.Fail };

        WildcardRun run = await RunAsync(
            hooks,
            Listed + Passive + TypeSet + Opened + Complete + Passive + Opened + Complete + Passive + Opened + Complete + Bye,
            [Listing, "abc", "xy", "wxyz"]);

        Assert.AreEqual(ListedSent + "EPSV\r\nTYPE I\r\nRETR a.txt\r\nEPSV\r\nRETR b.txt\r\nEPSV\r\nRETR c.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("abcxywxyz", run.OutputText);
        Assert.AreEqual(TransferResult.Success(4), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_MatchHookFails_LeavesEveryEntryOutAndEndsWithExit78()
    {
        var hooks = new RecordingWildcardHooks { MatchAnswer = FtpNameMatchAnswer.Fail };

        WildcardRun run = await RunAsync(hooks, Listed + Bye, [Listing]);

        Assert.AreEqual(ListedSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "Remote file not found", Listing.Length), run.Result);
        CollectionAssert.AreEqual(MatchCalls, hooks.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoEntryMatches_EndsWithExit78AfterQuit()
    {
        WildcardRun run = await RunAsync(FtpDownloadEveryMatch.Instance, Listed + Bye, [Listing], "ftp://127.0.0.1:47380/d/*.zip");

        Assert.AreEqual(ListedSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(string.Empty, run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "Remote file not found", Listing.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DefaultHooks_DownloadEveryMatchingFile()
    {
        WildcardRun run = await RunAsync(
            FtpDownloadEveryMatch.Instance,
            Listed + Passive + TypeSet + Opened + Complete + Bye,
            [Listing, "nine byte"],
            "ftp://127.0.0.1:47380/d/n*");

        Assert.AreEqual(ListedSent + "EPSV\r\nTYPE I\r\nRETR notes.md\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("nine byte", run.OutputText);
        Assert.AreEqual(TransferResult.Success(9), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_MalformedListing_EndsWithExit87AndNoQuit()
    {
        const string Malformed = "x bogus\r\n";

        WildcardRun run = await RunAsync(new RecordingWildcardHooks(), Listed + Bye, [Malformed]);

        Assert.AreEqual(ListedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpBadFileList, "Failed to parse FTP file list", Malformed.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingEntryPastCurlsBuffer_EndsWithExit27AndNoQuit()
    {
        string oversized = "-rw-r--r-- 1 u g 3 Jan 11 10:00 " + new string('n', 10000) + "\r\n";

        WildcardRun run = await RunAsync(new RecordingWildcardHooks(), Listed + Bye, [oversized]);

        Assert.AreEqual(ListedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OutOfMemory, "Out of memory", oversized.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_MatchRefused_EndsWithExit78AfterQuitAndStillCallsTheEndHook()
    {
        var hooks = new RecordingWildcardHooks();

        WildcardRun run = await RunAsync(hooks, Listed + Passive + TypeSet + "550 No such file\r\n" + Bye, [Listing, string.Empty]);

        Assert.AreEqual(ListedSent + "EPSV\r\nTYPE I\r\nRETR a.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        CollectionAssert.AreEqual((string[])[.. MatchCalls, "begin sub.txt 4", "end", "begin a.txt 3", "end"], hooks.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryRefused_EndsAsWithoutWildcards()
    {
        WildcardRun run = await RunAsync(new RecordingWildcardHooks(), LoggedIn + "550 No such directory\r\n" + Bye, []);

        Assert.AreEqual(LogInSent + "CWD d\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostQuoteRefusedAfterTheListing_EndsWithExit21AfterQuit()
    {
        WildcardRun run = await RunAsync(
            new RecordingWildcardHooks(),
            Listed + "500 Unknown\r\n" + Bye,
            [Listing],
            quotes: ["-BOGUS"]);

        Assert.AreEqual(ListedSent + "BOGUS\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.QuoteError, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCwdMethod_WalksTheDirectoryAsMultiCwd()
    {
        WildcardRun run = await RunAsync(
            new RecordingWildcardHooks(),
            Listed + Bye,
            [Listing],
            "ftp://127.0.0.1:47380/d/*.zip",
            method: FtpFileMethod.NoCwd);

        Assert.AreEqual(ListedSent + "QUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryUrl_ListsAsWithoutWildcards()
    {
        var hooks = new RecordingWildcardHooks();

        WildcardRun run = await RunAsync(hooks, Listed + Bye, [Listing], "ftp://127.0.0.1:47380/d/");

        Assert.AreEqual(ListedSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(FtpAsciiOutput.Of(Listing), run.OutputText);
        Assert.IsEmpty(hooks.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_StoresAsWithoutWildcards()
    {
        var hooks = new RecordingWildcardHooks();

        WildcardRun run = await RunAsync(
            hooks,
            LoggedIn + DirectoryChanged + Passive + TypeSet + Opened + Complete + Bye,
            [string.Empty],
            "ftp://127.0.0.1:47380/d/up.txt",
            upload: new MemoryStream(Encoding.Latin1.GetBytes("hi")));

        Assert.AreEqual(LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nSTOR up.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(2), run.Result);
        Assert.IsEmpty(hooks.Calls);
    }

    [TestMethod]
    public void DownloadEveryMatch_EachHook_AnswersAsLibcurlWithNoCallbacksSet()
    {
        var entry = new FtpFileListEntry { FileName = "a.txt", FileType = FtpFileType.File, TimeText = "Jan 11 10:00" };

        Assert.AreEqual(FtpNameMatchAnswer.Match, FtpDownloadEveryMatch.Instance.MatchName("*.txt", "a.txt"));
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpDownloadEveryMatch.Instance.MatchName("*.txt", "a.md"));
        Assert.AreEqual(FtpChunkBeginAnswer.Transfer, FtpDownloadEveryMatch.Instance.BeginChunk(entry, 1));
        Assert.AreEqual(FtpChunkEndAnswer.CarryOn, FtpDownloadEveryMatch.Instance.EndChunk());
    }

    private static async Task<WildcardRun> RunAsync(
        IFtpWildcardHooks hooks,
        string replies,
        string[] dataConnections,
        string url = Url,
        string[]? quotes = null,
        FtpFileMethod method = FtpFileMethod.MultiCwd,
        Stream? upload = null)
    {
        var control = ScriptedConnection.FromReplies(replies);
        ConnectResult[] connects =
        [
            ConnectResult.Connected(control),
            .. dataConnections.Select(data => ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(data)))),
        ];
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Events = events,
            QuoteCommands = quotes ?? [],
            FtpFileMethod = method,
            Upload = upload,
        };
        var handler = new FtpProtocolHandler(new QueuedConnector(connects)) { WildcardHooks = hooks };

        TransferResult result = await handler.ExecuteAsync(context);

        return new WildcardRun(result with { Report = null }, control, (MemoryStream)context.Output, events);
    }

    private sealed record WildcardRun(TransferResult Result, ScriptedConnection Control, MemoryStream Output, RecordingTransferEvents Events)
    {
        public string Sent => Encoding.Latin1.GetString(Control.Sent);

        public string OutputText => Encoding.Latin1.GetString(Output.ToArray());
    }
}
