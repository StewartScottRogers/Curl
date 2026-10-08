using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpFileUpload" /> against an in-memory peer: the SFTP requests byte for
/// byte - open flags and attributes, <c>MKDIR</c>s, write offsets and sizes - and the
/// outcome of each case measured 2026-09-29 with curl 8.21.0 (libssh2 1.11.1, Schannel
/// build) against OpenSSH 10.2, once with its own <c>sftp-server</c> logging every request
/// and once with a scripted SFTP subsystem (BL-571, ADR-0244).
/// </summary>
[TestClass]
public sealed class SftpFileUploadTests
{
    private const UnixFileMode Mode0644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    // WRITE|CREAT|TRUNC, WRITE alone and WRITE|APPEND|CREAT, as sftp-server logged them.
    private const uint NewFile = 0x1A;

    private const uint Resumed = 0x02;

    private const uint Appended = 0x0E;

    private static readonly byte[] Content = "hello upload\n"u8.ToArray();

    private static readonly SftpUploadOptions Plain = new(0, false, false, false, Mode0644);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task UploadAsync_NewFile_OpensToCreateAndTruncateWritesAndClosesAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/x/new.txt", Plain, new MemoryStream(Content));

        AssertSuccess(outcome, 13);
        CollectionAssert.AreEqual(new[] { (13L, (long?)13) }, outcome.Progress);
        AssertRequests(
            outcome,
            [SftpPacketType.Init, 0, 0, 0, 3],
            Join([SftpPacketType.RealPath], UInt32(0), Name(".")),
            SftpServerScript.OpenRequest("/x/new.txt", NewFile, 1),
            SftpServerScript.WriteRequest(2, 0, Content),
            SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task UploadAsync_ThreeMegabytes_SendsEach64KiBBlockAsWritesOf30000BytesAsMeasured()
    {
        const int Size = 3_000_000;
        byte[] content = [.. Enumerable.Range(0, Size).Select(index => (byte)(index % 251))];
        SftpServerScript script = SftpServerScript.Started(window: 8_000_000).HomeDirectory().Handle();
        for (uint id = 2; id <= 139; id++)
        {
            script.Status(id, SftpStatusCode.Ok);
        }

        Outcome outcome = await UploadAsync(script, "/big.bin", Plain, new MemoryStream(content));

        AssertSuccess(outcome, Size);
        Assert.HasCount(137, outcome.Progress);
        Assert.AreEqual((Size, (long?)Size), outcome.Progress[^1]);
        List<byte[]> writes = [.. Requests(outcome).Where(request => request[0] == SftpPacketType.Write)];
        List<(int Offset, int Length)> expected = [];
        for (int block = 0; block < Size; block += SftpFileUpload.ReadBufferSize)
        {
            int blockLength = Math.Min(SftpFileUpload.ReadBufferSize, Size - block);
            for (int start = 0; start < blockLength; start += SftpFileUpload.WriteChunkSize)
            {
                expected.Add((block + start, Math.Min(SftpFileUpload.WriteChunkSize, blockLength - start)));
            }
        }

        Assert.HasCount(expected.Count, writes);
        Assert.AreEqual((65536, 30000), expected[3], "the second block starts at 65536, as sftp-server logged");
        Assert.AreEqual((60000, 5536), expected[2], "as sftp-server logged");
        for (int index = 0; index < writes.Count; index++)
        {
            (int offset, int length) = expected[index];
            CollectionAssert.AreEqual(SftpServerScript.WriteRequest((uint)(2 + index), (ulong)offset, content[offset..(offset + length)]), writes[index], $"write {index}");
        }
    }

    [TestMethod]
    public async Task UploadAsync_ResumeFromAnOffset_OpensForWritingAloneAndSkipsThatMuchOfTheSourceAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/e.txt", Plain with { ResumeFrom = 5 }, new MemoryStream(Content));

        AssertSuccess(outcome, 8);
        CollectionAssert.AreEqual(new[] { (8L, (long?)8) }, outcome.Progress);
        AssertRequests(outcome, 2, SftpServerScript.OpenRequest("/e.txt", Resumed, 1), SftpServerScript.WriteRequest(2, 5, Content[5..]), SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task UploadAsync_ResumeFromRemoteSize_StatsThenResumesAtThatSizeAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Size(5, 1).Handle(2).Status(3, SftpStatusCode.Ok).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/e.txt", Plain with { ResumeFromRemoteSize = true }, new MemoryStream(Content));

        AssertSuccess(outcome, 8);
        AssertRequests(
            outcome,
            2,
            SftpServerScript.StatRequest("/e.txt", 1),
            SftpServerScript.OpenRequest("/e.txt", Resumed, 2),
            SftpServerScript.WriteRequest(3, 5, Content[5..]),
            SftpServerScript.CloseRequest(4));
    }

    [TestMethod]
    [DataRow("status", DisplayName = "no such file, as measured")]
    [DataRow("no size", DisplayName = "no size attribute, as measured")]
    [DataRow("zero", DisplayName = "size 0, as measured")]
    public async Task UploadAsync_ResumeFromRemoteSizeWithNoSize_UploadsTheWholeFileAsNewAsMeasured(string answer)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory();
        _ = answer switch
        {
            "status" => script.Status(1, 2),
            "no size" => script.Sftp([SftpPacketType.Attributes, .. UInt32(1), .. UInt32(0)]),
            _ => script.Size(0, 1),
        };
        script.Handle(2).Status(3, SftpStatusCode.Ok).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/m.txt", Plain with { ResumeFromRemoteSize = true }, new MemoryStream(Content));

        AssertSuccess(outcome, 13);
        AssertRequests(outcome, 3, SftpServerScript.OpenRequest("/m.txt", NewFile, 2), SftpServerScript.WriteRequest(3, 0, Content), SftpServerScript.CloseRequest(4));
    }

    // OpenSSH's sftp-server never reports a size of 2^63 or more, so this is pinned from
    // curl 8.21.0's sftp_upload_init (lib/vssh/libssh2.c at curl-8_21_0), not measured.
    [TestMethod]
    public async Task UploadAsync_ResumeFromRemoteSizeWithItsTopBitSet_ThrowsExit36BadFileSizeAndWritesNothing()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Size(0x8000000000000000uL, 1);
        ScriptedConnection connection = new(script.Bytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Upload(connection, "/e.txt", Plain with { ResumeFromRemoteSize = true }));
        Diagnostics.ActFailure(failure);

        Diagnostics.Assert("exit code", CurlExitCode.BadDownloadResume, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.BadDownloadResume, failure.ExitCode);
        Diagnostics.Diff("message", "Bad file size (-9223372036854775808)", failure.Message);
        Assert.AreEqual("Bad file size (-9223372036854775808)", failure.Message);
        List<byte[]> requests = SftpServerScript.SftpRequests(connection.Written);
        CollectionAssert.AreEqual(SftpServerScript.StatRequest("/e.txt", 1), requests[^1]);
    }

    [TestMethod]
    [DataRow(13UL, DisplayName = "remote as long, as measured")]
    [DataRow(20UL, DisplayName = "remote longer, as measured")]
    public async Task UploadAsync_ResumeFromRemoteSizeCoveringTheSource_OpensAndClosesWithNoWriteAsMeasured(ulong remoteSize)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Size(remoteSize, 1).Handle(2).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/e.txt", Plain with { ResumeFromRemoteSize = true }, new MemoryStream(Content));

        AssertSuccess(outcome, 0);
        Assert.IsEmpty(outcome.Progress);
        AssertRequests(outcome, 3, SftpServerScript.OpenRequest("/e.txt", Resumed, 2), SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task UploadAsync_ResumeFromBeyondTheSource_OpensAndClosesWithNoWriteAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/e.txt", Plain with { ResumeFrom = 20 }, new MemoryStream(Content));

        AssertSuccess(outcome, 0);
        AssertRequests(outcome, 2, SftpServerScript.OpenRequest("/e.txt", Resumed, 1), SftpServerScript.CloseRequest(2));
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "-a, as measured")]
    [DataRow(5L, DisplayName = "-a with -C 5, as measured")]
    public async Task UploadAsync_Append_OpensToAppendAndSendsTheWholeSourceFromZeroAsMeasured(long resumeFrom)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/e.txt", Plain with { Append = true, ResumeFrom = resumeFrom }, new MemoryStream(Content));

        AssertSuccess(outcome, 13);
        AssertRequests(outcome, 2, SftpServerScript.OpenRequest("/e.txt", Appended, 1), SftpServerScript.WriteRequest(2, 0, Content), SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task UploadAsync_AppendWithResumeFromRemoteSize_StillStatsThenAppendsTheWholeSourceAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Size(5, 1).Handle(2).Status(3, SftpStatusCode.Ok).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/e.txt", Plain with { Append = true, ResumeFromRemoteSize = true }, new MemoryStream(Content));

        AssertSuccess(outcome, 13);
        AssertRequests(
            outcome,
            2,
            SftpServerScript.StatRequest("/e.txt", 1),
            SftpServerScript.OpenRequest("/e.txt", Appended, 2),
            SftpServerScript.WriteRequest(3, 0, Content),
            SftpServerScript.CloseRequest(4));
    }

    [TestMethod]
    public async Task UploadAsync_StandardInputWithAnOffset_SendsItWholeFromTheOffsetAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/s.txt", Plain with { ResumeFrom = 5 }, new UnseekableStream(Content));

        AssertSuccess(outcome, 13);
        CollectionAssert.AreEqual(new[] { (13L, (long?)null) }, outcome.Progress);
        AssertRequests(outcome, 2, SftpServerScript.OpenRequest("/s.txt", Resumed, 1), SftpServerScript.WriteRequest(2, 5, Content), SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task UploadAsync_CreateFileMode_SendsItAsTheOpensPermissionsAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/n.txt", Plain with { CreateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite }, new MemoryStream(Content));

        Diagnostics.Diff("SSH_FXP_OPEN", SftpServerScript.OpenRequest("/n.txt", NewFile, 1, 0x180), Requests(outcome)[2]);
        CollectionAssert.AreEqual(SftpServerScript.OpenRequest("/n.txt", NewFile, 1, 0x180), Requests(outcome)[2]);
    }

    [TestMethod]
    public async Task UploadAsync_CreateFileModeZero_SendsCurlsDefault0644AsTheOpensPermissionsAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/n.txt", Plain with { CreateFileMode = 0 }, new MemoryStream(Content));

        Diagnostics.Diff("SSH_FXP_OPEN", SftpServerScript.OpenRequest("/n.txt", NewFile, 1, 0x1A4), Requests(outcome)[2]);
        CollectionAssert.AreEqual(SftpServerScript.OpenRequest("/n.txt", NewFile, 1, 0x1A4), Requests(outcome)[2]);
    }

    [TestMethod]
    [DataRow("/~/up/x.txt", "/home/fake/up/x.txt", DisplayName = "home path, as measured")]
    [DataRow("/a%20b.txt", "/a b.txt", DisplayName = "escaped space")]
    public async Task UploadAsync_UrlPath_OpensThePathCurlSends(string urlPath, string expected)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, urlPath, Plain, new MemoryStream(Content));

        Diagnostics.Diff("SSH_FXP_OPEN", SftpServerScript.OpenRequest(expected, NewFile, 1), Requests(outcome)[2]);
        CollectionAssert.AreEqual(SftpServerScript.OpenRequest(expected, NewFile, 1), Requests(outcome)[2]);
    }

    [TestMethod]
    [DataRow(2u, 4u, DisplayName = "no such file, then MKDIR failures, as measured")]
    [DataRow(4u, 0u, DisplayName = "failure, then MKDIRs made, as measured")]
    [DataRow(10u, 3u, DisplayName = "no such path, then MKDIRs denied, as measured")]
    [DataRow(2u, 11u, DisplayName = "no such file, then directories that exist, as measured")]
    public async Task UploadAsync_CreateDirectories_MakesEveryDirectoryOfThePathThenOpensAgainAsMeasured(uint openStatus, uint makeDirectoryStatus)
    {
        SftpServerScript script = SftpServerScript.Started()
            .HomeDirectory()
            .Status(1, openStatus)
            .Status(2, makeDirectoryStatus)
            .Status(3, makeDirectoryStatus)
            .Handle(4)
            .Status(5, SftpStatusCode.Ok)
            .Status(6, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/d1/d2/n.txt", Plain with { CreateDirectories = true }, new MemoryStream(Content));

        AssertSuccess(outcome, 13);
        AssertRequests(
            outcome,
            2,
            SftpServerScript.OpenRequest("/d1/d2/n.txt", NewFile, 1),
            SftpServerScript.MakeDirectoryRequest("/d1", 2),
            SftpServerScript.MakeDirectoryRequest("/d1/d2", 3),
            SftpServerScript.OpenRequest("/d1/d2/n.txt", NewFile, 4),
            SftpServerScript.WriteRequest(5, 0, Content),
            SftpServerScript.CloseRequest(6));
    }

    [TestMethod]
    [DataRow(2u, CurlExitCode.RemoteFileNotFound, "Remote file not found", DisplayName = "no such file, as measured")]
    [DataRow(1u, CurlExitCode.Ssh, "Error in the SSH layer", DisplayName = "end of file, as measured")]
    [DataRow(5u, CurlExitCode.Ssh, "Error in the SSH layer", DisplayName = "bad message, as measured")]
    public async Task UploadAsync_MakeDirectoryFailsOtherwise_ThrowsTheExitCodesOwnTextAsMeasured(uint status, CurlExitCode exitCode, string message)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, 2).Status(2, status);
        ScriptedConnection connection = new(script.Bytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Upload(connection, "/d1/d2/n.txt", Plain with { CreateDirectories = true }));
        Diagnostics.ActFailure(failure);

        Diagnostics.Assert("exit code", exitCode, failure.ExitCode);
        Assert.AreEqual(exitCode, failure.ExitCode);
        Diagnostics.Diff("message", message, failure.Message);
        Assert.AreEqual(message, failure.Message);
        Assert.HasCount(4, SftpServerScript.SftpRequests(connection.Written), "no second MKDIR, no open");
    }

    [TestMethod]
    [DataRow(2u, CurlExitCode.RemoteFileNotFound, "Creating the dir/file failed: No such file or directory", DisplayName = "no such file, as measured")]
    [DataRow(3u, CurlExitCode.RemoteAccessDenied, "Creating the dir/file failed: Permission denied", DisplayName = "permission denied")]
    public async Task UploadAsync_SecondOpenFails_ThrowsCreatingTheDirOrFileFailedAsMeasured(uint status, CurlExitCode exitCode, string message)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, 2).Status(2, 4).Status(3, status);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Upload(new ScriptedConnection(script.Bytes), "/d1/n.txt", Plain with { CreateDirectories = true }));
        Diagnostics.ActFailure(failure);

        Diagnostics.Assert("exit code", exitCode, failure.ExitCode);
        Assert.AreEqual(exitCode, failure.ExitCode);
        Diagnostics.Diff("message", message, failure.Message);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public async Task UploadAsync_CreateDirectoriesForAFileAtTheRoot_MakesNoDirectoryAndOpensAgain()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, 2).Status(2, 2);
        ScriptedConnection connection = new(script.Bytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Upload(connection, "/x.txt", Plain with { CreateDirectories = true }));
        Diagnostics.ActFailure(failure);

        Diagnostics.Diff("message", "Creating the dir/file failed: No such file or directory", failure.Message);
        Assert.AreEqual("Creating the dir/file failed: No such file or directory", failure.Message);
        List<byte[]> requests = SftpServerScript.SftpRequests(connection.Written);
        CollectionAssert.AreEqual(SftpServerScript.OpenRequest("/x.txt", NewFile, 2), requests[^1]);
    }

    [TestMethod]
    [DataRow(1u, CurlExitCode.Ssh, "Upload failed: Unknown error in libssh2 (1/-31)", false, DisplayName = "end of file, as measured")]
    [DataRow(2u, CurlExitCode.RemoteFileNotFound, "Upload failed: No such file or directory (2/-31)", false, DisplayName = "missing directory, as measured")]
    [DataRow(3u, CurlExitCode.RemoteAccessDenied, "Upload failed: Permission denied (3/-31)", false, DisplayName = "read-only file, as measured")]
    [DataRow(4u, CurlExitCode.Ssh, "Upload failed: Operation failed (4/-31)", false, DisplayName = "failure, as measured")]
    [DataRow(11u, CurlExitCode.RemoteFileExists, "Upload failed: File already exists (11/-31)", false, DisplayName = "file exists, as measured")]
    [DataRow(3u, CurlExitCode.RemoteAccessDenied, "Upload failed: Permission denied (3/-31)", true, DisplayName = "permission denied makes no directory, as measured")]
    public async Task UploadAsync_OpenFails_ThrowsUploadFailedAsMeasured(uint status, CurlExitCode exitCode, string message, bool createDirectories)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, status);
        ScriptedConnection connection = new(script.Bytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Upload(connection, "/d/x.txt", Plain with { CreateDirectories = createDirectories }));
        Diagnostics.ActFailure(failure);

        Diagnostics.Assert("exit code", exitCode, failure.ExitCode);
        Assert.AreEqual(exitCode, failure.ExitCode);
        Diagnostics.Diff("message", message, failure.Message);
        Assert.AreEqual(message, failure.Message);
        Assert.HasCount(3, SftpServerScript.SftpRequests(connection.Written), "no MKDIR, no close without a handle");
    }

    [TestMethod]
    public async Task UploadAsync_WriteRefused_EndsWithExit79AndStillClosesAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, 3).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/writefail/3", Plain, new MemoryStream(Content));

        AssertFailure(outcome, 0);
        AssertRequests(outcome, 3, SftpServerScript.WriteRequest(2, 0, Content), SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task UploadAsync_SecondWriteRefused_ReportsTheBytesAcknowledgedBeforeItAsMeasured()
    {
        byte[] content = new byte[SftpFileUpload.ReadBufferSize];
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle()
            .Status(2, SftpStatusCode.Ok).Status(3, 4).Status(4, 4).Status(5, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/writefail/second/4", Plain, new MemoryStream(content));

        AssertFailure(outcome, 30000);
        CollectionAssert.AreEqual(new[] { (30000L, (long?)65536) }, outcome.Progress);
        CollectionAssert.AreEqual(SftpServerScript.CloseRequest(5), Requests(outcome)[^1]);
    }

    [TestMethod]
    public async Task UploadAsync_ConnectionEndsDuringTheCopy_EndsWithExit79AsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle();

        Outcome outcome = await UploadAsync(script, "/killwrite/x", Plain, new MemoryStream(Content));

        AssertFailure(outcome, 0);
    }

    [TestMethod]
    public async Task UploadAsync_WriteAnsweredWithAnotherType_EndsWithExit79()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Data(2, Content).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/writedata/x", Plain, new MemoryStream(Content));

        AssertFailure(outcome, 0);
    }

    [TestMethod]
    public async Task UploadAsync_EmptySource_OpensAndClosesWithNoWriteAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/d/empty", Plain, new MemoryStream());

        AssertSuccess(outcome, 0);
        Assert.IsEmpty(outcome.Progress);
        AssertRequests(outcome, 2, SftpServerScript.OpenRequest("/d/empty", NewFile, 1), SftpServerScript.CloseRequest(2));
    }

    [TestMethod]
    public async Task UploadAsync_SourceFailsToRead_EndsTheUploadAsItsEndDoes()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle().Status(2, SftpStatusCode.Ok);

        Outcome outcome = await UploadAsync(script, "/f", Plain, new FailingStream());

        AssertSuccess(outcome, 0);
        CollectionAssert.AreEqual(SftpServerScript.CloseRequest(2), Requests(outcome)[^1]);
    }

    [TestMethod]
    public async Task UploadAsync_ConnectionEndsBeforeTheOpenIsAnswered_ThrowsExit79()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory();

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Upload(new ScriptedConnection(script.Bytes), "/f", Plain));
        Diagnostics.ActFailure(failure);

        Diagnostics.Assert("exit code", CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Diagnostics.Diff("message", "Error in the SSH layer", failure.Message);
        Assert.AreEqual("Error in the SSH layer", failure.Message);
    }

    [TestMethod]
    public async Task UploadAsync_CreateDirectories_ReportsEachDirectoryBeforeItsMakeDirectoryIsSent()
    {
        SftpServerScript script = SftpServerScript.Started()
            .HomeDirectory()
            .Status(1, 2)
            .Status(2, SftpStatusCode.Ok)
            .Status(3, SftpStatusCode.Ok)
            .Handle(4)
            .Status(5, SftpStatusCode.Ok)
            .Status(6, SftpStatusCode.Ok);
        Diagnostics.ArrangeScript(script);
        ScriptedConnection connection = new(script.Bytes);
        RequestCountingTransferEvents events = new(connection);
        SftpUploadOptions options = new(0, false, false, true, 0);
        ArrangeUpload("/a/b/c.txt", options);

        TransferResult result = await new SftpFileUpload(SftpSessionTests.Transport(connection), events)
            .UploadAsync("/a/b/c.txt", options, new MemoryStream("x"u8.ToArray()), NoTransferProgress.Instance, CancellationToken.None);
        Diagnostics.ActResult(result);
        Diagnostics.Act("transfer event lines", string.Join(" | ", events.Lines));
        Diagnostics.ActRequests(connection.Written);

        Diagnostics.Assert("success", true, result.IsSuccess);
        Diagnostics.Diff(
            "transfer event lines",
            "3: SFTP: creating directory '/a' | 4: SFTP: creating directory '/a/b' | 7: upload completely sent off: 1 bytes",
            string.Join(" | ", events.Lines));
        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);

        // INIT, REALPATH and the failed OPEN come before the first line; its MKDIR before the second.
        CollectionAssert.AreEqual(
            new[] { "3: SFTP: creating directory '/a'", "4: SFTP: creating directory '/a/b'", "7: upload completely sent off: 1 bytes" },
            events.Lines);
        List<byte[]> requests = SftpServerScript.SftpRequests(connection.Written);
        CollectionAssert.AreEqual(SftpServerScript.MakeDirectoryRequest("/a", 2), requests[3]);
        CollectionAssert.AreEqual(SftpServerScript.MakeDirectoryRequest("/a/b", 3), requests[4]);
    }

    private ValueTask<TransferResult> Upload(ScriptedConnection connection, string urlPath, SftpUploadOptions options)
    {
        ArrangeUpload(urlPath, options);
        Diagnostics.Bytes("source", Content);
        return new SftpFileUpload(SftpSessionTests.Transport(connection), NoTransferEvents.Instance)
            .UploadAsync(urlPath, options, new MemoryStream(Content), new RecordingProgress(), CancellationToken.None);
    }

    private async Task<Outcome> UploadAsync(SftpServerScript script, string urlPath, SftpUploadOptions options, Stream source)
    {
        ArrangeUpload(urlPath, options);
        Diagnostics.ArrangeScript(script);
        ScriptedConnection connection = new(script.Bytes);
        RecordingProgress progress = new();
        TransferResult result;
        using (Diagnostics.Phase("upload"))
        {
            result = await new SftpFileUpload(SftpSessionTests.Transport(connection), NoTransferEvents.Instance)
                .UploadAsync(urlPath, options, source, progress, CancellationToken.None);
        }

        Diagnostics.ActResult(result);
        Diagnostics.Act("progress reports", string.Join(", ", progress.Reports));
        Diagnostics.ActRequests(connection.Written);
        return new Outcome(result, progress.Reports, connection.Written);
    }

    private void ArrangeUpload(string urlPath, SftpUploadOptions options)
    {
        Diagnostics.Arrange("URL path", urlPath);
        Diagnostics.Arrange("upload options", options);
    }

    private void AssertSuccess(Outcome outcome, long uploaded)
    {
        Diagnostics.Assert("exit code", CurlExitCode.Ok, outcome.Result.ExitCode);
        Diagnostics.Assert("bytes uploaded", uploaded, outcome.Result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, outcome.Result.ExitCode);
        Assert.AreEqual(uploaded, outcome.Result.BytesTransferred);
        Assert.AreEqual(uploaded, outcome.Result.Report!.UploadSize, "%{size_upload}");
    }

    private void AssertFailure(Outcome outcome, long uploaded)
    {
        Diagnostics.Assert("exit code", CurlExitCode.Ssh, outcome.Result.ExitCode);
        Diagnostics.Assert("%{size_upload}", uploaded, outcome.Result.Report?.UploadSize);
        Assert.AreEqual(CurlExitCode.Ssh, outcome.Result.ExitCode);
        Assert.AreEqual("Error in the SSH layer", outcome.Result.ErrorMessage);
        Assert.AreEqual(uploaded, outcome.Result.Report!.UploadSize, "%{size_upload}, as measured");
    }

    private static List<byte[]> Requests(Outcome outcome) => SftpServerScript.SftpRequests(outcome.Written);

    private void AssertRequests(Outcome outcome, params byte[][] expected) => AssertRequests(outcome, 0, expected);

    private void AssertRequests(Outcome outcome, int skipped, params byte[][] expected)
    {
        List<byte[]> requests = Requests(outcome);
        Diagnostics.Assert("request count", skipped + expected.Length, requests.Count);
        Assert.HasCount(skipped + expected.Length, requests);
        for (int index = 0; index < expected.Length; index++)
        {
            Diagnostics.Diff($"request {skipped + index}", expected[index], requests[skipped + index]);
            CollectionAssert.AreEqual(expected[index], requests[skipped + index], $"request {skipped + index}");
        }
    }

    private sealed record Outcome(TransferResult Result, List<(long, long?)> Progress, byte[] Written);

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("The source failed.");
    }

    private sealed class RecordingProgress : ITransferProgress
    {
        internal List<(long, long?)> Reports { get; } = [];

        public void ReportTransferStarted()
        {
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
        {
        }

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Reports.Add((bytesSoFar, expectedTotal));
    }
}
