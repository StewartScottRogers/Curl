using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpDirectoryListing" /> against an in-memory peer: the SFTP requests
/// byte for byte, the bytes written and the outcome of each case measured 2026-09-29 with
/// curl 8.21.0 (libssh2 1.11.1, Schannel build) against OpenSSH 10.2, once with its own
/// <c>sftp-server</c> and once with a scripted SFTP subsystem that logged every request
/// (BL-570, ADR-0241).
/// </summary>
[TestClass]
public sealed class SftpDirectoryListingTests
{
    private const uint Directory = 0x41ED;

    private const uint SymbolicLink = 0xA1FF;

    // What OpenSSH's sftp-server sent for a directory holding a file, a subdirectory, a
    // symbolic link and a broken one, in its order, and the lines curl printed for them.
    private static readonly byte[][] OpenSshEntries =
    [
        SftpServerScript.Entry("sub", "drwxr-xr-x    2 stewart_rogers stewart_rogers     4096 Jan  1  2026 sub", Directory),
        SftpServerScript.Entry("a.txt", "-rw-r--r--    1 stewart_rogers stewart_rogers        6 Jan  1  2026 a.txt"),
        SftpServerScript.Entry("..", "drwxr-xr-x    4 stewart_rogers stewart_rogers     4096 Sep 29 12:51 ..", Directory),
        SftpServerScript.Entry("broken", "lrwxrwxrwx    1 stewart_rogers stewart_rogers       19 Sep 29 12:50 broken", SymbolicLink),
        SftpServerScript.Entry(".", "drwxr-xr-x    3 stewart_rogers stewart_rogers     4096 Sep 29 12:50 .", Directory),
        SftpServerScript.Entry("link", "lrwxrwxrwx    1 stewart_rogers stewart_rogers        5 Sep 29 12:50 link", SymbolicLink),
        SftpServerScript.Entry("b.bin", "-rw-r--r--    1 stewart_rogers stewart_rogers        3 Jan  1  2026 b.bin"),
    ];

    private static readonly string OpenSshListing =
        "drwxr-xr-x    2 stewart_rogers stewart_rogers     4096 Jan  1  2026 sub\n" +
        "-rw-r--r--    1 stewart_rogers stewart_rogers        6 Jan  1  2026 a.txt\n" +
        "drwxr-xr-x    4 stewart_rogers stewart_rogers     4096 Sep 29 12:51 ..\n" +
        "lrwxrwxrwx    1 stewart_rogers stewart_rogers       19 Sep 29 12:50 broken -> /nonexistent/target\n" +
        "drwxr-xr-x    3 stewart_rogers stewart_rogers     4096 Sep 29 12:50 .\n" +
        "lrwxrwxrwx    1 stewart_rogers stewart_rogers        5 Sep 29 12:50 link -> a.txt\n" +
        "-rw-r--r--    1 stewart_rogers stewart_rogers        3 Jan  1  2026 b.bin\n";

    private static readonly byte[] FileEntry = SftpServerScript.Entry("f", "LONG-f");

    private static readonly byte[] LinkEntry = SftpServerScript.Entry("l", "LONG-l", SymbolicLink);

    [TestMethod]
    public async Task ListAsync_OpenSshDirectory_WritesEachLongNameAndFollowsTheLinksAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(2, OpenSshEntries)
            .Names(3, Name("/nonexistent/target"))
            .Names(4, Name("a.txt"))
            .Status(5, SftpStatusCode.EndOfFile)
            .Status(6, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/home/stewart_rogers/bl570/list/");

        Assert.AreEqual(OpenSshListing, Encoding.UTF8.GetString(outcome.Output));
        Assert.AreEqual(TransferResult.Success(541), outcome.Result, "-w '%{size_download}' printed 541, as measured");
        Assert.AreEqual((541L, (long?)null), outcome.Progress[^1]);
        AssertRequests(
            outcome,
            [SftpPacketType.Init, 0, 0, 0, 3],
            Join([SftpPacketType.RealPath], UInt32(0), Name(".")),
            SftpServerScript.OpenDirectoryRequest("/home/stewart_rogers/bl570/list/"),
            SftpServerScript.ReadDirectoryRequest(2),
            SftpServerScript.ReadLinkRequest("/home/stewart_rogers/bl570/list/broken", 3),
            SftpServerScript.ReadLinkRequest("/home/stewart_rogers/bl570/list/link", 4),
            SftpServerScript.ReadDirectoryRequest(5),
            SftpServerScript.CloseRequest(6));
    }

    [TestMethod]
    public async Task ListAsync_ListOnly_WritesOnlyTheNamesWithoutReadingLinksAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(2, OpenSshEntries)
            .Status(3, SftpStatusCode.EndOfFile)
            .Status(4, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/", listOnly: true);

        Assert.AreEqual("sub\na.txt\n..\nbroken\n.\nlink\nb.bin\n", Encoding.UTF8.GetString(outcome.Output));
        Assert.AreEqual(TransferResult.Success(33), outcome.Result);
        AssertRequests(outcome, 3, SftpServerScript.ReadDirectoryRequest(2), SftpServerScript.ReadDirectoryRequest(3), SftpServerScript.CloseRequest(4));
    }

    [TestMethod]
    [DataRow(false, "drwxr-xr-x    4 u u     4096 Sep 29 12:51 ..\ndrwxr-xr-x    2 u u     4096 Sep 29 12:50 .\n", DisplayName = "long names, as measured")]
    [DataRow(true, "..\n.\n", DisplayName = "-l, as measured")]
    public async Task ListAsync_EmptyDirectory_WritesItsDotEntriesAsMeasured(bool listOnly, string expected)
    {
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(
                2,
                SftpServerScript.Entry("..", "drwxr-xr-x    4 u u     4096 Sep 29 12:51 ..", Directory),
                SftpServerScript.Entry(".", "drwxr-xr-x    2 u u     4096 Sep 29 12:50 .", Directory))
            .Status(3, SftpStatusCode.EndOfFile)
            .Status(4, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/emptydir/", listOnly);

        Assert.AreEqual(expected, Encoding.UTF8.GetString(outcome.Output));
    }

    [TestMethod]
    public async Task ListAsync_NamesOverSeveralAnswers_SendsAReadDirectoryForEach()
    {
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(2, FileEntry)
            .Names(3, SftpServerScript.Entry("g", "LONG-g"))
            .Status(4, SftpStatusCode.EndOfFile)
            .Status(5, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/");

        Assert.AreEqual("LONG-f\nLONG-g\n", Encoding.UTF8.GetString(outcome.Output));
        AssertRequests(outcome, 3, SftpServerScript.ReadDirectoryRequest(2), SftpServerScript.ReadDirectoryRequest(3), SftpServerScript.ReadDirectoryRequest(4), SftpServerScript.CloseRequest(5));
    }

    [TestMethod]
    public async Task ListAsync_AnswerOfNoNames_EndsTheListingAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().OpenedDirectory().Names(2).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/zeronames/");

        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        Assert.IsEmpty(outcome.Output);
        AssertRequests(outcome, 3, SftpServerScript.ReadDirectoryRequest(2), SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task ListAsync_HomeDirectoryPath_OpensAndReadsLinksUnderTheHomeDirectoryAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(2, FileEntry, LinkEntry)
            .Names(3, Name("tgt"))
            .Status(4, SftpStatusCode.EndOfFile)
            .Status(5, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/~/sub/");

        Assert.AreEqual("LONG-f\nLONG-l -> tgt\n", Encoding.UTF8.GetString(outcome.Output));
        List<byte[]> requests = Requests(outcome);
        CollectionAssert.AreEqual(SftpServerScript.OpenDirectoryRequest("/home/fake/sub/"), requests[2]);
        CollectionAssert.AreEqual(SftpServerScript.ReadLinkRequest("/home/fake/sub/l", 3), requests[4]);
    }

    [TestMethod]
    public async Task ListAsync_EscapedSlash_OpensTheDecodedDirectoryAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().OpenedDirectory().Status(2, SftpStatusCode.EndOfFile).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/x%2F");

        CollectionAssert.AreEqual(SftpServerScript.OpenDirectoryRequest("/d/x/"), Requests(outcome)[2]);
    }

    [TestMethod]
    public async Task ListAsync_ReadLinkAnsweredWithOk_PrintsTheEntrysOwnNameAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(2, FileEntry, LinkEntry)
            .Status(3, SftpStatusCode.Ok)
            .Status(4, SftpStatusCode.EndOfFile)
            .Status(5, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/readlinkok/");

        Assert.AreEqual("LONG-f\nLONG-l -> l\n", Encoding.UTF8.GetString(outcome.Output));
        Assert.AreEqual(TransferResult.Success(19), outcome.Result);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "failed status, as measured")]
    [DataRow(true, DisplayName = "no names, as measured")]
    public async Task ListAsync_ReadLinkFails_EndsWithExit27AfterTheLinesSoFarAndStillClosesAsMeasured(bool answerNoNames)
    {
        SftpServerScript script = SftpServerScript.Started().OpenedDirectory().Names(2, FileEntry, LinkEntry);
        script = answerNoNames ? script.Names(3) : script.Status(3, 2);
        script.Status(4, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/readlinkfail/");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OutOfMemory, "Out of memory", 7), outcome.Result);
        Assert.AreEqual("LONG-f\n", Encoding.UTF8.GetString(outcome.Output));
        AssertRequests(outcome, 3, SftpServerScript.ReadDirectoryRequest(2), SftpServerScript.ReadLinkRequest("/d/readlinkfail/l", 3), SftpServerScript.CloseRequest(4));
    }

    [TestMethod]
    [DataRow(3u, CurlExitCode.RemoteAccessDenied, "Permission denied", DisplayName = "permission denied, as measured")]
    [DataRow(2u, CurlExitCode.RemoteFileNotFound, "No such file or directory", DisplayName = "no such file")]
    [DataRow(4u, CurlExitCode.Ssh, "Operation failed", DisplayName = "failure")]
    public async Task ListAsync_ReadDirectoryFails_EndsWithTheStatussExitCodeAfterTheLinesSoFarAsMeasured(uint status, CurlExitCode exitCode, string description)
    {
        SftpServerScript script = SftpServerScript.Started().OpenedDirectory().Names(2, FileEntry).Status(3, status).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/readdirfail/");

        Assert.AreEqual(TransferResult.Failure(exitCode, $"Could not open remote file for reading: {description} :: -31", 7), outcome.Result);
        Assert.AreEqual("LONG-f\n", Encoding.UTF8.GetString(outcome.Output));
        AssertRequests(outcome, 3, SftpServerScript.ReadDirectoryRequest(2), SftpServerScript.ReadDirectoryRequest(3), SftpServerScript.CloseRequest(4));
    }

    [TestMethod]
    [DataRow(1u, CurlExitCode.Ssh, "Unknown error in libssh2", DisplayName = "end of file, as measured")]
    [DataRow(2u, CurlExitCode.RemoteFileNotFound, "No such file or directory", DisplayName = "missing directory, as measured")]
    [DataRow(3u, CurlExitCode.RemoteAccessDenied, "Permission denied", DisplayName = "permission denied, as measured")]
    [DataRow(4u, CurlExitCode.Ssh, "Operation failed", DisplayName = "failure, as measured")]
    public async Task ListAsync_OpenDirectoryFails_ThrowsTheStatussExitCodeWithoutAClose(uint status, CurlExitCode exitCode, string description)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, status);
        ScriptedConnection connection = new(script.Bytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await List(connection, "/d/opendir/"));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual("Could not open directory for reading: " + description, failure.Message);
        Assert.HasCount(3, SftpServerScript.SftpRequests(connection.Written), "no close without a handle, as measured");
    }

    [TestMethod]
    public async Task ListAsync_OpenDirectoryAnsweredWithOk_WaitsForTheHandleAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started()
            .HomeDirectory()
            .Status(1, SftpStatusCode.Ok)
            .Handle()
            .Names(2, FileEntry)
            .Status(3, SftpStatusCode.EndOfFile)
            .Status(4, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/opendirok/");

        Assert.AreEqual("LONG-f\n", Encoding.UTF8.GetString(outcome.Output));
    }

    [TestMethod]
    public async Task ListAsync_EntryWithoutPermissionsOrLongName_WritesAnEmptyLineAndReadsNoLinkAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(2, Join(Name("x"), Name("LONG-x"), UInt32(0)), Join(Name("y"), Name(string.Empty), UInt32(1), UInt32(0), UInt32(7)))
            .Status(3, SftpStatusCode.EndOfFile)
            .Status(4, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/noperms/");

        Assert.AreEqual("LONG-x\n\n", Encoding.UTF8.GetString(outcome.Output));
        Assert.HasCount(6, Requests(outcome));
    }

    [TestMethod]
    public async Task ListAsync_EntryWithEveryAttribute_FindsTheLinkAmongThemAsMeasured()
    {
        byte[] everyAttribute = Join(
            Name("a"),
            Name("LONG-a"),
            UInt32(0x8000000F),
            UInt32(0),
            UInt32(9),
            UInt32(1),
            UInt32(2),
            UInt32(SymbolicLink),
            UInt32(3),
            UInt32(4),
            UInt32(1),
            Name("name@example"),
            Name("data"));
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(2, everyAttribute, FileEntry)
            .Names(3, Name("tgt"))
            .Status(4, SftpStatusCode.EndOfFile)
            .Status(5, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/allattrs/");

        Assert.AreEqual("LONG-a -> tgt\nLONG-f\n", Encoding.UTF8.GetString(outcome.Output));
    }

    [TestMethod]
    public async Task ListAsync_NulInsideANameOrTarget_PrintsUpToItAsCurlsCStringsDo()
    {
        byte[] link = Join(String("l\0x"u8.ToArray()), String("LONG-l\0hidden"u8.ToArray()), UInt32(4), UInt32(SymbolicLink));
        SftpServerScript script = SftpServerScript.Started()
            .OpenedDirectory()
            .Names(2, link)
            .Sftp(Join([SftpPacketType.Name], UInt32(3), UInt32(1), String("tgt\0more"u8.ToArray())))
            .Status(4, SftpStatusCode.EndOfFile)
            .Status(5, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/");

        Assert.AreEqual("LONG-l -> tgt\n", Encoding.UTF8.GetString(outcome.Output));
        CollectionAssert.AreEqual(SftpServerScript.ReadLinkRequest("/d/l", 3), Requests(outcome)[4]);
    }

    [TestMethod]
    public async Task ListAsync_NoBody_StopsBeforeOpeningTheDirectoryAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory();
        ScriptedConnection connection = new(script.Bytes);
        MemoryStream output = new();

        TransferResult result = await new SftpDirectoryListing(SftpSessionTests.Transport(connection))
            .ListAsync("/d/", listOnly: false, noBody: true, output, new RecordingProgress(), CancellationToken.None);

        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.AreEqual(0, output.Length);
        Assert.HasCount(2, SftpServerScript.SftpRequests(connection.Written), "INIT and REALPATH only");
    }

    [TestMethod]
    public async Task ListAsync_ConnectionEndsDuringTheListing_EndsWithExit79AndTheBytesSoFar()
    {
        SftpServerScript script = SftpServerScript.Started().OpenedDirectory().Names(2, FileEntry);

        Outcome outcome = await ListAsync(script, "/d/");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 7), outcome.Result);
        Assert.AreEqual("LONG-f\n", Encoding.UTF8.GetString(outcome.Output));
    }

    [TestMethod]
    public async Task ListAsync_ReadDirectoryAnsweredWithAnotherType_EndsWithExit79()
    {
        SftpServerScript script = SftpServerScript.Started().OpenedDirectory().Handle(2).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 0), outcome.Result);
    }

    [TestMethod]
    public async Task ListAsync_ReadLinkAnsweredWithAnotherType_EndsWithExit79()
    {
        SftpServerScript script = SftpServerScript.Started().OpenedDirectory().Names(2, LinkEntry).Handle(3).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await ListAsync(script, "/d/");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 0), outcome.Result);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "before REALPATH is answered")]
    [DataRow(true, DisplayName = "before OPENDIR is answered")]
    public async Task ListAsync_ConnectionEndsBeforeTheListing_ThrowsExit79(bool homeDirectoryAnswered)
    {
        SftpServerScript script = SftpServerScript.Started();
        script = homeDirectoryAnswered ? script.HomeDirectory() : script;

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await List(new ScriptedConnection(script.Bytes), "/d/"));

        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual("Error in the SSH layer", failure.Message);
    }

    private static ValueTask<TransferResult> List(ScriptedConnection connection, string urlPath) =>
        new SftpDirectoryListing(SftpSessionTests.Transport(connection))
            .ListAsync(urlPath, listOnly: false, noBody: false, new MemoryStream(), new RecordingProgress(), CancellationToken.None);

    private static async Task<Outcome> ListAsync(SftpServerScript script, string urlPath, bool listOnly = false)
    {
        ScriptedConnection connection = new(script.Bytes);
        MemoryStream output = new();
        RecordingProgress progress = new();
        TransferResult result = await new SftpDirectoryListing(SftpSessionTests.Transport(connection))
            .ListAsync(urlPath, listOnly, noBody: false, output, progress, CancellationToken.None);
        return new Outcome(result, output.ToArray(), progress.Reports, connection.Written);
    }

    private static List<byte[]> Requests(Outcome outcome) => SftpServerScript.SftpRequests(outcome.Written);

    private static void AssertRequests(Outcome outcome, params byte[][] expected) => AssertRequests(outcome, 0, expected);

    private static void AssertRequests(Outcome outcome, int skipped, params byte[][] expected)
    {
        List<byte[]> requests = Requests(outcome);
        Assert.HasCount(skipped + expected.Length, requests);
        for (int index = 0; index < expected.Length; index++)
        {
            CollectionAssert.AreEqual(expected[index], requests[skipped + index], $"request {skipped + index}");
        }
    }

    private sealed record Outcome(TransferResult Result, byte[] Output, List<(long, long?)> Progress, byte[] Written);

    private sealed class RecordingProgress : ITransferProgress
    {
        internal List<(long, long?)> Reports { get; } = [];

        public void ReportTransferStarted()
        {
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Reports.Add((bytesSoFar, expectedTotal));

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }
}
