using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpFileDownload" /> against an in-memory peer: the SFTP requests byte
/// for byte, the bytes written and the outcome of each case measured 2026-09-29 with curl
/// 8.21.0 (libssh2 1.11.1, Schannel build) against OpenSSH 10.2, once with its own
/// <c>sftp-server</c> and once with a scripted SFTP subsystem that logged every request
/// (BL-569, ADR-0220).
/// </summary>
[TestClass]
public sealed partial class SftpFileDownloadTests
{
    private const UnixFileMode Mode0644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private static readonly byte[] Hello = "abcde"u8.ToArray();

    [TestMethod]
    public async Task DownloadAsync_FiveByteFile_SendsCurlsRequestsAndWritesTheBytesAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Data(3, Hello).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/x/file.txt");

        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
        CollectionAssert.AreEqual(Hello, outcome.Output);
        CollectionAssert.AreEqual(new[] { (5L, (long?)5) }, outcome.Progress);
        AssertRequests(
            outcome,
            [SftpPacketType.Init, 0, 0, 0, 3],
            Join([SftpPacketType.RealPath], UInt32(0), Name(".")),
            SftpServerScript.OpenRequest("/x/file.txt"),
            SftpServerScript.StatRequest("/x/file.txt"),
            SftpServerScript.ReadRequest(3, 0, 20),
            SftpServerScript.CloseRequest(4));
    }

    [TestMethod]
    public async Task DownloadAsync_EmptyFile_ReadsAheadAsForAnUnknownSizeAndSucceedsAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(0).Status(3, SftpStatusCode.EndOfFile).Status(17, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/size/0/f");

        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        Assert.IsEmpty(outcome.Output);
        Assert.IsEmpty(outcome.Progress);
        byte[][] expectedReads = [.. Enumerable.Range(0, 14).Select(index => SftpServerScript.ReadRequest((uint)(3 + index), (ulong)(index * 30000), 30000))];
        AssertRequests(outcome, 4, [.. expectedReads, SftpServerScript.CloseRequest(17)]);
    }

    [TestMethod]
    public async Task DownloadAsync_FileLargerThanOneChannelWindow_ReadsItAllAndGrowsTheWindow()
    {
        const int Size = 3_000_000;
        byte[] content = [.. Enumerable.Range(0, Size).Select(index => (byte)(index % 251))];
        SftpServerScript script = SftpServerScript.Started().Opened(Size);
        for (int read = 0; read < 100; read++)
        {
            script.Data((uint)(3 + read), content[(read * 30000)..((read + 1) * 30000)]);
        }

        ScriptedConnection connection = new(script.Bytes);
        MemoryStream output = new();
        RecordingProgress progress = new();

        TransferResult result = await new SftpFileDownload(SftpSessionTests.Transport(connection))
            .DownloadAsync("/big", Mode0644, output, progress, CancellationToken.None);

        Assert.AreEqual(TransferResult.Success(Size), result);
        CollectionAssert.AreEqual(content, output.ToArray());
        Assert.HasCount(100, progress.Reports);
        Assert.AreEqual((Size, (long?)Size), progress.Reports[^1]);
        List<byte[]> requests = SftpServerScript.SftpRequests(connection.Written);
        List<byte[]> reads = [.. requests.Where(request => request[0] == SftpPacketType.Read)];
        for (int read = 0; read < reads.Count; read++)
        {
            CollectionAssert.AreEqual(SftpServerScript.ReadRequest((uint)(3 + read), (ulong)(read * 30000), 30000), reads[read]);
        }

        CollectionAssert.AreEqual(SftpServerScript.CloseRequest((uint)(3 + reads.Count)), requests[^1]);
        List<byte[]> adjustments = [.. SftpServerScript.SshPayloads(connection.Written).Where(payload => payload[0] == SshConnectionMessageNumber.ChannelWindowAdjust)];
        Assert.HasCount(5, adjustments, "3 MB through a 2 MiB window restored at three quarters, as OpenSSH logged five adjustments");
    }

    [TestMethod]
    public async Task DownloadAsync_ShorterThanItsStatSize_EndsWithExit18AsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(10).Data(3, Hello).Status(4, SftpStatusCode.EndOfFile).Status(5, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/bigsize/f");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 5 bytes missing", 5), outcome.Result);
        CollectionAssert.AreEqual(Hello, outcome.Output);
        AssertRequests(outcome, 4, SftpServerScript.ReadRequest(3, 0, 40), SftpServerScript.ReadRequest(4, 5, 20), SftpServerScript.CloseRequest(5));
    }

    [TestMethod]
    public async Task DownloadAsync_EndOfFileAtTheFirstRead_EndsWithExit18AsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Status(3, SftpStatusCode.EndOfFile).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/readfail/1");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 5 bytes missing", 0), outcome.Result);
    }

    [TestMethod]
    [DataRow(2u, DisplayName = "no such file, as measured")]
    [DataRow(3u, DisplayName = "permission denied, as measured")]
    [DataRow(4u, DisplayName = "failure, as measured")]
    public async Task DownloadAsync_ReadFails_EndsWithExit79AndStillClosesAsMeasured(uint status)
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Status(3, status).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/readfail/x");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 0), outcome.Result);
        AssertRequests(outcome, 4, SftpServerScript.ReadRequest(3, 0, 20), SftpServerScript.CloseRequest(4));
    }

    [TestMethod]
    public async Task DownloadAsync_UnknownSizeAndAShortRead_DropsTheReadsAheadAndGoesOnFromWhereItEnded()
    {
        SftpServerScript script = SftpServerScript.Started()
            .HomeDirectory()
            .Handle()
            .Status(2, 2)
            .Data(3, Hello)
            .Status(4, SftpStatusCode.EndOfFile)
            .Status(17, SftpStatusCode.EndOfFile)
            .Status(31, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/statfail/f");

        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
        CollectionAssert.AreEqual(new[] { (5L, (long?)null) }, outcome.Progress);
        List<byte[]> requests = Requests(outcome);
        CollectionAssert.AreEqual(SftpServerScript.ReadRequest(17, 5, 30000), requests[18]);
        CollectionAssert.AreEqual(SftpServerScript.CloseRequest(31), requests[^1]);
    }

    [TestMethod]
    [DataRow("0000000000", DisplayName = "no size flag")]
    [DataRow("00000001000000", DisplayName = "size cut short")]
    [DataRow("00000001FFFFFFFFFFFFFFFF", DisplayName = "size beyond a long")]
    public async Task DownloadAsync_StatGivesNoUsableSize_ReadsUntilTheEnd(string attributesHex)
    {
        SftpServerScript script = SftpServerScript.Started()
            .HomeDirectory()
            .Handle()
            .Sftp([SftpPacketType.Attributes, .. UInt32(2), .. Convert.FromHexString(attributesHex)])
            .Data(3, Hello)
            .Status(17, SftpStatusCode.EndOfFile)
            .Status(31, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/f");

        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
    }

    [TestMethod]
    [DataRow(2u, CurlExitCode.RemoteFileNotFound, "No such file or directory", DisplayName = "missing file, as measured")]
    [DataRow(3u, CurlExitCode.RemoteAccessDenied, "Permission denied", DisplayName = "no read permission, as measured")]
    [DataRow(4u, CurlExitCode.Ssh, "Operation failed", DisplayName = "failure")]
    [DataRow(11u, CurlExitCode.RemoteFileExists, "File already exists", DisplayName = "file exists")]
    [DataRow(14u, CurlExitCode.RemoteDiskFull, "Disk full", DisplayName = "disk full")]
    [DataRow(18u, CurlExitCode.QuoteError, "Directory not empty", DisplayName = "directory not empty")]
    [DataRow(99u, CurlExitCode.Ssh, "Unknown error in libssh2", DisplayName = "unknown code")]
    public async Task DownloadAsync_OpenFails_ThrowsTheStatussExitCodeAndMessageAsMeasured(uint status, CurlExitCode exitCode, string description)
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Status(1, status);
        ScriptedConnection connection = new(script.Bytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Download(connection, "/missing"));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual("Could not open remote file for reading: " + description, failure.Message);
        Assert.HasCount(3, SftpServerScript.SftpRequests(connection.Written), "no close without a handle");
    }

    [TestMethod]
    public async Task DownloadAsync_OpenAnsweredWithOk_WaitsForTheHandleAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started()
            .HomeDirectory()
            .Status(1, SftpStatusCode.Ok)
            .Handle()
            .Size(5)
            .Data(3, Hello)
            .Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/f");

        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
    }

    [TestMethod]
    [DataRow(2u, CurlExitCode.RemoteFileNotFound, "Remote file not found", DisplayName = "no such file, as measured")]
    [DataRow(3u, CurlExitCode.RemoteAccessDenied, "Access denied to remote resource", DisplayName = "permission denied, as measured")]
    [DataRow(4u, CurlExitCode.Ssh, "Error in the SSH layer", DisplayName = "failure, as measured")]
    [DataRow(15u, CurlExitCode.RemoteDiskFull, "Disk full or allocation exceeded", DisplayName = "quota exceeded")]
    [DataRow(11u, CurlExitCode.RemoteFileExists, "Remote file already exists", DisplayName = "file exists")]
    [DataRow(18u, CurlExitCode.QuoteError, "Quote command returned error", DisplayName = "directory not empty")]
    public async Task DownloadAsync_RealPathFails_ThrowsTheExitCodesOwnTextAsMeasured(uint status, CurlExitCode exitCode, string message)
    {
        SftpServerScript script = SftpServerScript.Started().Status(0, status);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Download(new ScriptedConnection(script.Bytes), "/~/f"));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    [DataRow("/~/f", "/home/fake/f", DisplayName = "home file, as measured")]
    [DataRow("/~/dir/f", "/home/fake/dir/f", DisplayName = "home subdirectory, as measured")]
    [DataRow("/a%20b.txt", "/a b.txt", DisplayName = "escaped space, as measured")]
    [DataRow("/x%2Fy", "/x/y", DisplayName = "escaped slash, as measured")]
    [DataRow("/~file", "/~file", DisplayName = "tilde without a slash")]
    public async Task DownloadAsync_UrlPath_OpensThePathCurlSends(string urlPath, string expected)
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Data(3, Hello).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, urlPath);

        List<byte[]> requests = Requests(outcome);
        CollectionAssert.AreEqual(SftpServerScript.OpenRequest(expected), requests[2]);
        CollectionAssert.AreEqual(SftpServerScript.StatRequest(expected), requests[3]);
    }

    [TestMethod]
    public async Task DownloadAsync_CreateFileMode_SendsItAsTheOpensPermissions()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Data(3, Hello).Status(4, SftpStatusCode.Ok);
        ScriptedConnection connection = new(script.Bytes);

        await new SftpFileDownload(SftpSessionTests.Transport(connection))
            .DownloadAsync("/f", UnixFileMode.UserRead | UnixFileMode.UserWrite, new MemoryStream(), new RecordingProgress(), CancellationToken.None);

        CollectionAssert.AreEqual(
            Join([SftpPacketType.Open], UInt32(1), Name("/f"), UInt32(1), UInt32(4), UInt32(0x8180)),
            SftpServerScript.SftpRequests(connection.Written)[2]);
    }

    [TestMethod]
    public async Task DownloadAsync_ConnectionEndsAtTheOpen_SucceedsWithNothingWrittenAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory();

        TransferResult result = await Download(new ScriptedConnection(script.Bytes), "/f");

        Assert.AreEqual(TransferResult.Success(0), result);
    }

    [TestMethod]
    public async Task DownloadAsync_ConnectionEndsAtTheStat_ThrowsExit79()
    {
        SftpServerScript script = SftpServerScript.Started().HomeDirectory().Handle();

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Download(new ScriptedConnection(script.Bytes), "/f"));

        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual("Error in the SSH layer", failure.Message);
    }

    [TestMethod]
    public async Task DownloadAsync_RealPathAnsweredWithAnotherType_ThrowsExit79()
    {
        SftpServerScript script = SftpServerScript.Started().Handle(0);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Download(new ScriptedConnection(script.Bytes), "/f"));

        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
    }

    [TestMethod]
    public async Task DownloadAsync_PacketLongerThanLibssh2Accepts_ThrowsExit79()
    {
        SftpServerScript script = SftpServerScript.Started().ChannelData([.. UInt32(256 * 1024 + 1), SftpPacketType.Name]);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await Download(new ScriptedConnection(script.Bytes), "/f"));

        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
    }

    [TestMethod]
    public async Task DownloadAsync_ConnectionEndsDuringTheCopy_EndsWithExit79AndTheBytesSoFar()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(10).Data(3, Hello);

        Outcome outcome = await DownloadAsync(script, "/f");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 5), outcome.Result);
        CollectionAssert.AreEqual(Hello, outcome.Output);
    }

    [TestMethod]
    public async Task DownloadAsync_ReadAnsweredWithAnotherType_EndsWithExit79()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Handle(3).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/f");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 0), outcome.Result);
    }

    [TestMethod]
    public async Task DownloadAsync_PacketTooShortToCarryANumber_SkipsIt()
    {
        SftpServerScript script = SftpServerScript.Started().Sftp(SftpPacketType.Name, 0).Opened(5).Data(3, Hello).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, "/f");

        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
    }

    [TestMethod]
    public async Task DownloadAsync_CloseNeverAnswered_StillSucceeds()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(5).Data(3, Hello);

        Outcome outcome = await DownloadAsync(script, "/f");

        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
    }

    private static ValueTask<TransferResult> Download(ScriptedConnection connection, string urlPath) =>
        new SftpFileDownload(SftpSessionTests.Transport(connection))
            .DownloadAsync(urlPath, Mode0644, new MemoryStream(), new RecordingProgress(), CancellationToken.None);

    private static async Task<Outcome> DownloadAsync(SftpServerScript script, string urlPath)
    {
        ScriptedConnection connection = new(script.Bytes);
        MemoryStream output = new();
        RecordingProgress progress = new();
        TransferResult result = await new SftpFileDownload(SftpSessionTests.Transport(connection))
            .DownloadAsync(urlPath, Mode0644, output, progress, CancellationToken.None);
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
