using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Sftp;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Pins <see cref="ScpFileUpload" /> against an in-memory peer: the <c>exec</c> request, the
/// <c>C</c> line, the bytes and the channel's close byte for byte, and the outcome of each
/// case measured 2026-09-29 with curl 8.21.0 (libssh2 1.11.1, Schannel build) against
/// OpenSSH 10.2, once running its own <c>scp -t</c> and once a scripted one (BL-577,
/// ADR-0258).
/// </summary>
[TestClass]
public sealed class ScpFileUploadTests
{
    private const string InvalidAcknowledgement = "Invalid ACK response from remote";

    private const string UnexpectedChannelClose = "Unexpected channel close";

    private const string FailedToSend = "failed to send file";

    private const UnixFileMode Mode0644 = TransferContext.DefaultCreateFileMode;

    private static readonly byte[] HelloScp = "hello scp\n"u8.ToArray();

    [TestMethod]
    public async Task UploadAsync_NewFile_RunsScpTSendsTheFileLineAndTheBytesWithNoZeroByteAsMeasured()
    {
        Outcome outcome = await UploadAsync(ScpServerScript.Receiving(), "/home/u/files/new.txt", new MemoryStream(HelloScp));

        Assert.AreEqual(TransferResult.Success(10) with { Report = new TransferReport { UploadSize = 10 } }, outcome.Result);
        CollectionAssert.AreEqual(new[] { (10L, (long?)10) }, outcome.Progress);
        List<byte[]> written = SftpServerScript.SshPayloads(outcome.Written);
        byte[] serverChannel = UInt32(SftpServerScript.ServerChannel);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelOpen], Name("session"), UInt32(0), UInt32(2097152), UInt32(32768)), written[0]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelRequest], serverChannel, Name("exec"), [1], Name("scp -t '/home/u/files/new.txt'")), written[1]);
        CollectionAssert.AreEqual(Join("C0644 10 new.txt\n"u8.ToArray(), HelloScp), outcome.ChannelBytes, "no T line before and no zero byte after, as measured");
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelEof], serverChannel), written[^2]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelClose], serverChannel), written[^1]);
    }

    [TestMethod]
    [DataRow(0b110_000_000, "C0600", DisplayName = "0600, as measured")]
    [DataRow(0b000_000_001, "C01", DisplayName = "1, as measured")]
    [DataRow(0, "C0644", DisplayName = "0 leaves curl's default, as measured")]
    [DataRow(0b111_111_111, "C0777", DisplayName = "0777, as measured")]
    public async Task UploadAsync_CreateFileMode_IsTheFileLinesModeAsMeasured(int mode, string expected)
    {
        Outcome outcome = await UploadAsync(ScpServerScript.Receiving(), "/f/m.txt", new MemoryStream(HelloScp), (UnixFileMode)mode);

        Assert.AreEqual(CurlExitCode.Ok, outcome.Result.ExitCode);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes($"{expected} 10 m.txt\n"), outcome.ChannelBytes[..^HelloScp.Length]);
    }

    [TestMethod]
    [DataRow("/~/bl577/files/tilde.txt", "scp -t 'bl577/files/tilde.txt'", "tilde.txt", DisplayName = "home directory, as measured")]
    [DataRow("/x/it%27s%21x.txt", "scp -t '/x/it'\"'\"'s'\\!'x.txt'", "it's!x.txt", DisplayName = "apostrophe and exclamation mark, as measured")]
    [DataRow("/~/f", "scp -t 'f'", "f", DisplayName = "a path with no slash names itself")]
    public async Task UploadAsync_UrlPath_SendsTheCommandAndNameCurlSentAsMeasured(string urlPath, string command, string name)
    {
        Outcome outcome = await UploadAsync(ScpServerScript.Receiving(), urlPath, new MemoryStream(HelloScp));

        Assert.AreEqual(CurlExitCode.Ok, outcome.Result.ExitCode);
        byte[] request = SftpServerScript.SshPayloads(outcome.Written)[1];
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(command), request[(1 + 4 + 4 + 4 + 1 + 4)..]);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes($"C0644 10 {name}\n"), outcome.ChannelBytes[..^HelloScp.Length]);
    }

    [TestMethod]
    public async Task UploadAsync_EmptyFile_SendsASizeOfZeroAndSucceedsAsMeasured()
    {
        Outcome outcome = await UploadAsync(ScpServerScript.Receiving(), "/f/empty.txt", new MemoryStream());

        Assert.AreEqual(TransferResult.Success(0) with { Report = new TransferReport() }, outcome.Result);
        CollectionAssert.AreEqual("C0644 0 empty.txt\n"u8.ToArray(), outcome.ChannelBytes);
        Assert.IsEmpty(outcome.Progress);
    }

    [TestMethod]
    public async Task UploadAsync_SourceLargerThanOneBlock_SendsItIn64KiBBlocksAsMeasured()
    {
        byte[] content = [.. Enumerable.Range(0, 100000).Select(index => (byte)(index % 251))];

        Outcome outcome = await UploadAsync(ScpServerScript.Receiving(), "/f/big.bin", new MemoryStream(content));

        Assert.AreEqual(100000, outcome.Result.BytesTransferred);
        CollectionAssert.AreEqual(Join("C0644 100000 big.bin\n"u8.ToArray(), content), outcome.ChannelBytes);
        CollectionAssert.AreEqual(new[] { (65536L, (long?)100000), (100000L, (long?)100000) }, outcome.Progress);
    }

    [TestMethod]
    public async Task UploadAsync_SourceAlreadyPartlyRead_SendsWhatRemains()
    {
        MemoryStream source = new(HelloScp) { Position = 6 };

        Outcome outcome = await UploadAsync(ScpServerScript.Receiving(), "/f/rest", source);

        Assert.AreEqual(4, outcome.Result.BytesTransferred);
        CollectionAssert.AreEqual("C0644 4 rest\nscp\n"u8.ToArray(), outcome.ChannelBytes);
    }

    [TestMethod]
    public async Task UploadAsync_SourceFailsToRead_EndsAsItsEndDoes()
    {
        Outcome outcome = await UploadAsync(ScpServerScript.Receiving(), "/f/x", new FailingStream(HelloScp));

        Assert.AreEqual(TransferResult.Success(0) with { Report = new TransferReport() }, outcome.Result);
    }

    [TestMethod]
    public async Task UploadAsync_SourceOfUnknownSize_FailsWithExit25BeforeOpeningAChannelAsMeasured()
    {
        ScriptedConnection connection = new(ScpServerScript.Receiving().Bytes);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await new ScpFileUpload(SftpSessionTests.Transport(connection), NoTransferEvents.Instance)
                .UploadAsync("/f/stdin.txt", Mode0644, new UnseekableStream(HelloScp), new RecordingProgress(), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.UploadFailed, failure.ExitCode);
        Assert.AreEqual("SCP requires a known file size for upload", failure.Message);
        Assert.IsEmpty(connection.Written);
    }

    [TestMethod]
    [DataRow("\u0001scp: nope\n", DisplayName = "error line, as measured")]
    [DataRow("\u0002fatal\n", DisplayName = "fatal line, as measured")]
    [DataRow("X", DisplayName = "junk, as measured")]
    public async Task UploadAsync_StartNotAcknowledged_FailsWithExit25ClosesTheChannelAtOnceAndSendsNoFileLineAsMeasured(string answer)
    {
        (SshTransferException failure, byte[] written) = await UploadFailsAsync(ScpServerScript.Started().Output(answer).Ended());

        AssertFailure(failure, InvalidAcknowledgement);
        Assert.IsEmpty(ScpServerScript.ChannelBytes(written));
        AssertClosedAtOnce(written);
    }

    [TestMethod]
    [DataRow("\u0001scp: /f/nodir/x.txt: No such file or directory\n", DisplayName = "missing directory, as measured")]
    [DataRow("\u0001scp: /f/readonly.txt: Permission denied\n", DisplayName = "read-only file, as measured")]
    [DataRow("\u0001scp: protocol error: bad mode\n", DisplayName = "mode refused, as measured")]
    [DataRow("\u0002fatal\n", DisplayName = "fatal line, as measured")]
    [DataRow("\u0001", DisplayName = "bare error byte, as measured")]
    [DataRow("Xjunk\n", DisplayName = "junk, as measured")]
    public async Task UploadAsync_FileLineRefused_FailsWithExit25AndSendsNoBytesAsMeasured(string answer)
    {
        (SshTransferException failure, byte[] written) = await UploadFailsAsync(ScpServerScript.Started().Output([0]).Output(answer).Ended());

        AssertFailure(failure, FailedToSend);
        CollectionAssert.AreEqual("C0644 10 f\n"u8.ToArray(), ScpServerScript.ChannelBytes(written));
        AssertClosedAtOnce(written);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "before the first acknowledgement, as measured")]
    [DataRow(true, DisplayName = "before the second acknowledgement, as measured")]
    public async Task UploadAsync_ChannelEndsBeforeAnAcknowledgement_FailsWithExit25AsMeasured(bool started)
    {
        ScpServerScript script = started ? ScpServerScript.Started().Output([0]) : ScpServerScript.Started();

        (SshTransferException failure, _) = await UploadFailsAsync(script.Ended());

        AssertFailure(failure, UnexpectedChannelClose);
    }

    [TestMethod]
    [DataRow(false, "SCP failure", DisplayName = "waiting for the first acknowledgement, as measured")]
    [DataRow(true, InvalidAcknowledgement, DisplayName = "waiting for the second acknowledgement, as measured")]
    public async Task UploadAsync_ConnectionBreaksBeforeAnAcknowledgement_FailsWithExit25AndLibssh2sMessage(bool started, string message)
    {
        ScpServerScript script = started ? ScpServerScript.Started().Output([0]) : ScpServerScript.Started();

        (SshTransferException failure, _) = await UploadFailsAsync(script);

        AssertFailure(failure, message);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "before the channel opens")]
    [DataRow(true, DisplayName = "before the exec request is answered")]
    public async Task UploadAsync_ConnectionBreaksBeforeScpStarts_FailsWithExit25(bool opened)
    {
        ScpServerScript script = opened ? new ScpServerScript().Confirm() : new ScpServerScript();

        (SshTransferException failure, _) = await UploadFailsAsync(script);

        AssertFailure(failure, "SCP failure");
    }

    [TestMethod]
    [DataRow(1u, "Channel open failure (administratively prohibited)")]
    [DataRow(2u, "Channel open failure (connect failed)", DisplayName = "connect failed, as measured with MaxSessions 0")]
    [DataRow(3u, "Channel open failure (unknown channel type)")]
    [DataRow(4u, "Channel open failure (resource shortage)")]
    [DataRow(5u, "Channel open failure")]
    public async Task UploadAsync_ChannelRefused_FailsWithExit25AndLibssh2sReasonText(uint reasonCode, string message)
    {
        ScpServerScript script = new ScpServerScript().Ssh(Join([SshConnectionMessageNumber.ChannelOpenFailure], UInt32(0), UInt32(reasonCode), Name("refused"), Name(string.Empty)));

        (SshTransferException failure, byte[] written) = await UploadFailsAsync(script);

        AssertFailure(failure, message);
        Assert.HasCount(1, SftpServerScript.SshPayloads(written), "a refused channel is not closed");
    }

    [TestMethod]
    public async Task UploadAsync_ExecRefused_FailsWithExit25AndClosesTheChannelAtOnce()
    {
        ScpServerScript script = new ScpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelFailure, .. UInt32(0)]).Ended();

        (SshTransferException failure, byte[] written) = await UploadFailsAsync(script);

        AssertFailure(failure, "Unable to complete request for channel-process-startup");
        AssertClosedAtOnce(written);
    }

    [TestMethod]
    public async Task UploadAsync_ConnectionBreaksDuringTheBytes_EndsWithExit79AndTheBytesSentSoFarAsMeasured()
    {
        byte[] content = new byte[70000];
        byte[] fileLine = "C0644 70000 f\n"u8.ToArray();
        ScpServerScript script = new ScpServerScript().Confirm((uint)(fileLine.Length + 65536)).Ssh([SshConnectionMessageNumber.ChannelSuccess, .. UInt32(0)]).Output([0]).Output([0]);

        Outcome outcome = await UploadAsync(script, "/f", new MemoryStream(content));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 65536) with { Report = new TransferReport { UploadSize = 65536 } }, outcome.Result);
    }

    [TestMethod]
    [DataRow("", DisplayName = "the server never closes, as measured")]
    [DataRow("\u0001scp: late\n", DisplayName = "an error line after the bytes, as measured")]
    public async Task UploadAsync_AnythingTheServerSaysAfterTheBytes_LeavesTheUploadSucceededAsMeasured(string after)
    {
        ScpServerScript script = ScpServerScript.Started().Output([0]).Output([0]);
        if (after.Length > 0)
        {
            script.Output(after).Ended();
        }

        Outcome outcome = await UploadAsync(script, "/f", new MemoryStream(HelloScp));

        Assert.AreEqual(TransferResult.Success(10) with { Report = new TransferReport { UploadSize = 10 } }, outcome.Result);
    }

    [TestMethod]
    [DataRow("/f/new.txt", 0b110_100_100, 10L, "C0644 10 new.txt\n")]
    [DataRow("f", 0b110_000_000, 0L, "C0600 0 f\n")]
    public void FileLine_BuildsLibssh2sLine(string path, int mode, long size, string expected) =>
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(expected), ScpFileUpload.FileLine((UnixFileMode)mode, size, Encoding.ASCII.GetBytes(path)));

    private static void AssertFailure(SshTransferException failure, string message)
    {
        Assert.AreEqual(CurlExitCode.UploadFailed, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    // Measured: after a failure libssh2 sends EOF and CLOSE together, then waits.
    private static void AssertClosedAtOnce(byte[] written)
    {
        List<byte[]> payloads = SftpServerScript.SshPayloads(written);
        byte[] serverChannel = UInt32(SftpServerScript.ServerChannel);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelEof], serverChannel), payloads[^2]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelClose], serverChannel), payloads[^1]);
    }

    private static async Task<(SshTransferException Failure, byte[] Written)> UploadFailsAsync(ScpServerScript script)
    {
        ScriptedConnection connection = new(script.Bytes);
        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await new ScpFileUpload(SftpSessionTests.Transport(connection), NoTransferEvents.Instance)
                .UploadAsync("/x/f", Mode0644, new MemoryStream(HelloScp), new RecordingProgress(), CancellationToken.None));
        return (failure, connection.Written);
    }

    private static async Task<Outcome> UploadAsync(ScpServerScript script, string urlPath, Stream source, UnixFileMode mode = Mode0644)
    {
        ScriptedConnection connection = new(script.Bytes);
        RecordingProgress progress = new();
        TransferResult result = await new ScpFileUpload(SftpSessionTests.Transport(connection), NoTransferEvents.Instance)
            .UploadAsync(urlPath, mode, source, progress, CancellationToken.None);
        return new Outcome(result, progress.Reports, connection.Written);
    }

    private sealed record Outcome(TransferResult Result, List<(long, long?)> Progress, byte[] Written)
    {
        internal byte[] ChannelBytes => ScpServerScript.ChannelBytes(Written);
    }

    private sealed class FailingStream(byte[] bytes) : MemoryStream(bytes)
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
