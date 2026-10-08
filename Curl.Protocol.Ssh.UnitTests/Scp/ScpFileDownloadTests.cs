using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Protocol.Ssh.Sftp;
using Curl.Testing;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Pins <see cref="ScpFileDownload" /> against an in-memory peer: the <c>exec</c> request
/// and the acknowledgements byte for byte, the bytes written and the outcome of each case
/// measured 2026-09-29 with curl 8.21.0 (libssh2 1.11.1, Schannel build) against OpenSSH
/// 10.2, once running its own <c>scp</c> and once a scripted one that logged what curl
/// sent (BL-574, ADR-0225).
/// </summary>
[TestClass]
public sealed partial class ScpFileDownloadTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string FailedToReceive = "Failed to recv file";

    private const string InvalidResponse = "Invalid response from SCP server";

    private static readonly byte[] HelloScp = "hello sftp\n"u8.ToArray();

    private static readonly byte[] Hello = "hello"u8.ToArray();

    [TestMethod]
    public async Task DownloadAsync_ElevenByteFile_RunsScpWithExecAcknowledgesThreeTimesAndWritesTheBytesAsMeasured()
    {
        Outcome outcome = await DownloadAsync(ScpServerScript.Sending(HelloScp), "/home/u/files/hello.txt");

        Diagnostics.AssertResult(TransferResult.Success(11), outcome.Result);
        Assert.AreEqual(TransferResult.Success(11), outcome.Result);
        Diagnostics.AssertBytes("output", HelloScp, outcome.Output);
        CollectionAssert.AreEqual(HelloScp, outcome.Output);
        Diagnostics.AssertProgress(new[] { (11L, (long?)11) }, outcome.Progress);
        CollectionAssert.AreEqual(new[] { (11L, (long?)11) }, outcome.Progress);
        List<byte[]> written = SftpServerScript.SshPayloads(outcome.Written);
        byte[] serverChannel = UInt32(SftpServerScript.ServerChannel);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelOpen], Name("session"), UInt32(0), UInt32(2097152), UInt32(32768)), written[0]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelRequest], serverChannel, Name("exec"), [1], Name("scp -pf '/home/u/files/hello.txt'")), written[1]);
        Diagnostics.AssertBytes("channel bytes", new byte[] { 0, 0, 0 }, outcome.ChannelBytes);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0 }, outcome.ChannelBytes, "the wakeup, then one acknowledgement per line and none after the bytes, as measured");
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelEof], serverChannel), written[^2]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelClose], serverChannel), written[^1]);
    }

    [TestMethod]
    public async Task DownloadAsync_EmptyFile_SucceedsWithNoBytesAsMeasured()
    {
        Outcome outcome = await DownloadAsync(ScpServerScript.Sending([]), "/f/empty.txt");

        Diagnostics.AssertResult(TransferResult.Success(0), outcome.Result);
        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        Diagnostics.AssertBytes("output", [], outcome.Output);
        Assert.IsEmpty(outcome.Output);
        Diagnostics.AssertProgress([], outcome.Progress);
        Assert.IsEmpty(outcome.Progress);
        Diagnostics.AssertBytes("channel bytes", new byte[] { 0, 0, 0 }, outcome.ChannelBytes);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0 }, outcome.ChannelBytes);
    }

    [TestMethod]
    [DataRow("/~/bl574home.txt", "scp -pf 'bl574home.txt'", DisplayName = "home directory, as measured")]
    [DataRow("/%7E/bl574home.txt", "scp -pf 'bl574home.txt'", DisplayName = "escaped tilde, as measured")]
    [DataRow("/f/a%20b.txt", "scp -pf '/f/a b.txt'", DisplayName = "escaped space, as measured")]
    [DataRow("/x/it%27s%21''here", "scp -pf '/x/it'\"'\"'s'\\!\"''\"'here'", DisplayName = "apostrophes and an exclamation mark, as measured")]
    public async Task DownloadAsync_UrlPath_SendsTheCommandCurlSentAsMeasured(string urlPath, string command)
    {
        Outcome outcome = await DownloadAsync(ScpServerScript.Sending(Hello), urlPath);

        Diagnostics.AssertResult(TransferResult.Success(5), outcome.Result);
        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(command), ExecCommand(outcome));
    }

    [TestMethod]
    [DataRow("\u0001scp: /f/missing.txt: No such file or directory\n", DisplayName = "missing file, as measured")]
    [DataRow("\u0001scp: /f/noread.txt: Permission denied\n", DisplayName = "unreadable file, as measured")]
    [DataRow("\u0001scp: /f/dir: not a regular file\n", DisplayName = "directory, as measured")]
    [DataRow("\u0002fatal thing\n", DisplayName = "fatal error line, as measured")]
    [DataRow("\u0001\n", DisplayName = "empty error line, as measured")]
    [DataRow("C0644 5 noT\nhello\0", DisplayName = "no T line, as measured")]
    [DataRow("Xgarbage\n", DisplayName = "junk, as measured")]
    [DataRow("C0644 5 f", DisplayName = "C line without a line end, as measured")]
    public async Task DownloadAsync_FirstLineIsNotATimesLine_FailsWithExit78AndOneAcknowledgementAsMeasured(string output)
    {
        Outcome outcome = await DownloadAsync(ScpServerScript.Started().Output(output).Ended(), "/f");

        AssertFailure(outcome, FailedToReceive);
        Diagnostics.AssertBytes("channel bytes", new byte[] { 0 }, outcome.ChannelBytes);
        CollectionAssert.AreEqual(new byte[] { 0 }, outcome.ChannelBytes);
    }

    [TestMethod]
    [DataRow("\u0001scp: nope\n", InvalidResponse, DisplayName = "error line after the T line, as measured")]
    [DataRow("D0755 0 d\n", InvalidResponse, DisplayName = "directory line, as measured")]
    [DataRow("C06x4 5 f\n", InvalidResponse + ", invalid mode", DisplayName = "mode not octal, as measured")]
    [DataRow("Cabc x f\n", InvalidResponse + ", invalid mode", DisplayName = "mode and size not numbers, as measured")]
    [DataRow("C0644 5x f\n", InvalidResponse + ", invalid size", DisplayName = "size with a letter, as measured")]
    [DataRow("C0644 5\n", InvalidResponse + ", too short or malformed", DisplayName = "no name, as measured")]
    [DataRow("C0644  5 f\n", InvalidResponse + ", too short or malformed", DisplayName = "two spaces, as measured")]
    [DataRow("C0644\t5 f\n", "Invalid data in SCP response", DisplayName = "tab, as measured")]
    [DataRow(" 0644 5 f\n", InvalidResponse, DisplayName = "space before the mode")]
    [DataRow("C 0644 5 f\n", InvalidResponse + ", malformed mode", DisplayName = "empty mode")]
    [DataRow("C1\r\r\r\r\n", InvalidResponse + ", too short", DisplayName = "too short once its line ends go")]
    [DataRow("C0644 5 f", "Unexpected channel close", DisplayName = "C line cut short")]
    [DataRow("C0 5\n", "Unexpected channel close", DisplayName = "line feed before seven bytes, as measured")]
    public async Task DownloadAsync_FileLineRefused_FailsWithExit78AndLibssh2sMessage(string fileLine, string message)
    {
        Outcome outcome = await DownloadAsync(ScpServerScript.Started().Output(ScpServerScript.TimesLine + fileLine).Ended(), "/f");

        AssertFailure(outcome, message);
        Diagnostics.AssertBytes("channel bytes", new byte[] { 0, 0 }, outcome.ChannelBytes);
        CollectionAssert.AreEqual(new byte[] { 0, 0 }, outcome.ChannelBytes);
    }

    [TestMethod]
    public async Task DownloadAsync_FileLineLongerThanLibssh2sBuffer_FailsAsUnterminatedAsMeasured()
    {
        string fileLine = "C0644 5 " + new string('a', 300) + "\n";

        Outcome outcome = await DownloadAsync(ScpServerScript.Started().Output(ScpServerScript.TimesLine + fileLine).Ended(), "/f");

        AssertFailure(outcome, "Unterminated response from SCP server");
    }

    [TestMethod]
    [DataRow("Tabc\n", "Invalid data in SCP response", DisplayName = "letters, as measured")]
    [DataRow("T 1 0 2 0\n", InvalidResponse + ", malformed mtime", DisplayName = "empty mtime")]
    [DataRow("T12345678\n", InvalidResponse + ", malformed mtime", DisplayName = "no space")]
    [DataRow("T1234567 \n", InvalidResponse + ", malformed mtime.usec", DisplayName = "nothing after mtime")]
    [DataRow("T1234 0 5\n", InvalidResponse + ", too short or malformed", DisplayName = "atime not ended by a space")]
    [DataRow("T1\r\r\r\r\r\r\n", InvalidResponse + ", too short", DisplayName = "too short once its line ends go")]
    [DataRow("", "Unexpected channel close", DisplayName = "nothing, as measured")]
    public async Task DownloadAsync_TimesLineRefused_FailsWithExit78AndLibssh2sMessage(string timesLine, string message)
    {
        Outcome outcome = await DownloadAsync(ScpServerScript.Started().Output(timesLine).Ended(), "/f");

        AssertFailure(outcome, message);
        Diagnostics.AssertBytes("channel bytes", new byte[] { 0 }, outcome.ChannelBytes);
        CollectionAssert.AreEqual(new byte[] { 0 }, outcome.ChannelBytes);
    }

    [TestMethod]
    public async Task DownloadAsync_TimesLineLongerThanLibssh2sBuffer_FailsAsUnterminated()
    {
        Outcome outcome = await DownloadAsync(ScpServerScript.Started().Output("T" + new string('1', 300)).Ended(), "/f");

        AssertFailure(outcome, "Unterminated response from SCP server");
    }

    [TestMethod]
    [DataRow("C107777 5 f\n", DisplayName = "mode with file type bits, as measured")]
    [DataRow("C644 5 f\n", DisplayName = "mode without its leading zero, as measured")]
    [DataRow("C0644 +5 f\n", DisplayName = "size with a plus sign, as measured")]
    [DataRow("C0644 05 f\n", DisplayName = "size with a leading zero, as measured")]
    [DataRow("C0644 5 f\r\n", DisplayName = "carriage return, as measured")]
    [DataRow("C0644 5 \u00e9\n", DisplayName = "name with a byte above 127")]
    public async Task DownloadAsync_FileLineLibssh2Accepts_DownloadsTheFileAsMeasured(string fileLine)
    {
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + fileLine).Output([.. Hello, 0]).Ended();

        Outcome outcome = await DownloadAsync(script, "/f");

        Diagnostics.AssertResult(TransferResult.Success(5), outcome.Result);
        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
        Diagnostics.AssertBytes("output", Hello, outcome.Output);
        CollectionAssert.AreEqual(Hello, outcome.Output);
    }

    [TestMethod]
    public async Task DownloadAsync_TimesLineWithALineFeedBeforeNineBytes_ReadsOnIntoTheNextLine()
    {
        ScpServerScript script = ScpServerScript.Started().Output("T1 0 1\n0 \n").Output("C0644 5 f\nhello\0").Ended();

        Outcome outcome = await DownloadAsync(script, "/f");

        Diagnostics.AssertResult(TransferResult.Success(5), outcome.Result);
        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
    }

    [TestMethod]
    public async Task DownloadAsync_FileShorterThanItsSize_EndsWithExit18AndTheBytesSoFarAsMeasured()
    {
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + "C0644 10 short\n").Output(Hello).Ended();

        Outcome outcome = await DownloadAsync(script, "/x/short");

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 5 bytes missing", 5), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "end of response with 5 bytes missing", 5), outcome.Result);
        Diagnostics.AssertBytes("output", Hello, outcome.Output);
        CollectionAssert.AreEqual(Hello, outcome.Output);
        Diagnostics.AssertProgress(new[] { (5L, (long?)10) }, outcome.Progress);
        CollectionAssert.AreEqual(new[] { (5L, (long?)10) }, outcome.Progress);
    }

    [TestMethod]
    public async Task DownloadAsync_NegativeSize_ReadsNothingAndEndsWithExit18AsMeasured()
    {
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + "C0644 -5 f\nhello\0").Ended();

        Outcome outcome = await DownloadAsync(script, "/x/negsize");

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed with -5 bytes remaining to read", 0), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed with -5 bytes remaining to read", 0), outcome.Result);
        Diagnostics.AssertBytes("output", [], outcome.Output);
        Assert.IsEmpty(outcome.Output);
    }

    [TestMethod]
    public async Task DownloadAsync_SizeMinusOne_ReadsToTheEndOfTheChannelAsMeasured()
    {
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + "C0644 -1 f\n").Output("hello\0tail").Ended();

        Outcome outcome = await DownloadAsync(script, "/x/minus1");

        Diagnostics.AssertResult(TransferResult.Success(10), outcome.Result);
        Assert.AreEqual(TransferResult.Success(10), outcome.Result);
        Diagnostics.AssertBytes("output", "hello\0tail"u8.ToArray(), outcome.Output);
        CollectionAssert.AreEqual("hello\0tail"u8.ToArray(), outcome.Output);
        Diagnostics.AssertProgress(new[] { (10L, (long?)null) }, outcome.Progress);
        CollectionAssert.AreEqual(new[] { (10L, (long?)null) }, outcome.Progress);
    }

    [TestMethod]
    public async Task DownloadAsync_BytesAfterTheSize_AreNotWrittenAsMeasured()
    {
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + "C0644 5 f\n").Output("helloEXTRA\0\u0001after\n").Ended();

        Outcome outcome = await DownloadAsync(script, "/x/extra");

        Diagnostics.AssertResult(TransferResult.Success(5), outcome.Result);
        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
        Diagnostics.AssertBytes("output", Hello, outcome.Output);
        CollectionAssert.AreEqual(Hello, outcome.Output);
    }

    [TestMethod]
    public async Task DownloadAsync_StandardErrorBetweenTheBytes_IsIgnoredAsMeasured()
    {
        ScpServerScript script = ScpServerScript.Started()
            .Ssh(Join([SshConnectionMessageNumber.ChannelExtendedData], UInt32(0), UInt32(1), Name("warn\n")))
            .Output(ScpServerScript.TimesLine + "C0644 5 f\nhel")
            .Ssh(Join([SshConnectionMessageNumber.ChannelExtendedData], UInt32(0), UInt32(1), Name("more\n")))
            .Output("lo\0")
            .Ended();

        Outcome outcome = await DownloadAsync(script, "/x/stderr");

        Diagnostics.AssertResult(TransferResult.Success(5), outcome.Result);
        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
        Diagnostics.AssertBytes("output", Hello, outcome.Output);
        CollectionAssert.AreEqual(Hello, outcome.Output);
        Diagnostics.AssertProgress(new[] { (3L, (long?)5), (5L, (long?)5) }, outcome.Progress);
        CollectionAssert.AreEqual(new[] { (3L, (long?)5), (5L, (long?)5) }, outcome.Progress);
    }

    [TestMethod]
    public async Task DownloadAsync_FileLargerThanOneChannelWindow_ReadsItAllAndGrowsTheWindowAsMeasured()
    {
        const int Size = 3_000_000;
        byte[] content = [.. Enumerable.Range(0, Size).Select(index => (byte)(index % 251))];
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + $"C0644 {Size} big.bin\n");
        foreach (byte[] chunk in content.Chunk(32768))
        {
            script.Output(chunk);
        }

        Outcome outcome = await DownloadAsync(script.Output([0]).Ended(), "/f/big.bin");

        Diagnostics.AssertResult(TransferResult.Success(Size), outcome.Result);
        Assert.AreEqual(TransferResult.Success(Size), outcome.Result);
        Diagnostics.AssertBytes("output", content, outcome.Output);
        CollectionAssert.AreEqual(content, outcome.Output);
        Assert.AreEqual((Size, (long?)Size), outcome.Progress[^1]);
        List<byte[]> adjustments = [.. SftpServerScript.SshPayloads(outcome.Written).Where(payload => payload[0] == SshConnectionMessageNumber.ChannelWindowAdjust)];
        Assert.HasCount(5, adjustments);
    }

    [TestMethod]
    public async Task DownloadAsync_ConnectionBreaksDuringTheBytes_EndsWithExit79AndTheBytesSoFarAsMeasured()
    {
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + "C0644 10 f\n").Output(Hello);

        Outcome outcome = await DownloadAsync(script, "/x/killdata");

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 5), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.Ssh, "Error in the SSH layer", 5), outcome.Result);
        Diagnostics.AssertBytes("output", Hello, outcome.Output);
        CollectionAssert.AreEqual(Hello, outcome.Output);
    }

    [TestMethod]
    public async Task DownloadAsync_ConnectionBreaksDuringTheHeader_FailsWithExit79AsMeasured()
    {
        ScpServerScript script = ScpServerScript.Started().Output("T1790702112 0 17");

        SshTransferException failure = await DownloadFailsAsync(script);

        Diagnostics.Assert("exit code", CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Diagnostics.AssertText("message", "Failed reading SCP response", failure.Message);
        Assert.AreEqual("Failed reading SCP response", failure.Message);
    }

    [TestMethod]
    [DataRow(1u, "Channel open failure (administratively prohibited)")]
    [DataRow(2u, "Channel open failure (connect failed)", DisplayName = "connect failed, as measured with MaxSessions 0")]
    [DataRow(3u, "Channel open failure (unknown channel type)")]
    [DataRow(4u, "Channel open failure (resource shortage)")]
    [DataRow(5u, "Channel open failure")]
    public async Task DownloadAsync_ChannelRefused_FailsWithExit79AndLibssh2sReasonText(uint reasonCode, string message)
    {
        ScpServerScript script = new ScpServerScript().Ssh(Join([SshConnectionMessageNumber.ChannelOpenFailure], UInt32(0), UInt32(reasonCode), Name("refused"), Name(string.Empty)));

        SshTransferException failure = await DownloadFailsAsync(script);

        Diagnostics.Assert("exit code", CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Diagnostics.AssertText("message", message, failure.Message);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public async Task DownloadAsync_ExecRefused_FailsWithExit79AndLibssh2sMessage()
    {
        ScpServerScript script = new ScpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelFailure, .. UInt32(0)]);

        SshTransferException failure = await DownloadFailsAsync(script);

        Diagnostics.Assert("exit code", CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Diagnostics.AssertText("message", "Unable to complete request for channel-process-startup", failure.Message);
        Assert.AreEqual("Unable to complete request for channel-process-startup", failure.Message);
    }

    [TestMethod]
    [DataRow(false, "Unexpected error", DisplayName = "before the channel opens")]
    [DataRow(true, "Failed waiting for channel success", DisplayName = "before the exec request is answered")]
    public async Task DownloadAsync_ConnectionClosesBeforeScpStarts_FailsWithExit79AndLibssh2sMessageAsMeasured(bool opened, string message)
    {
        ScpServerScript script = opened ? new ScpServerScript().Confirm() : new ScpServerScript();

        SshTransferException failure = await DownloadFailsAsync(script);

        Diagnostics.Assert("exit code", CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Diagnostics.AssertText("message", message, failure.Message);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public async Task DownloadAsync_ServerNeverClosesTheChannel_StillReportsTheDownload()
    {
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + "C0644 5 f\nhello\0");

        Outcome outcome = await DownloadAsync(script, "/f");

        Diagnostics.AssertResult(TransferResult.Success(5), outcome.Result);
        Assert.AreEqual(TransferResult.Success(5), outcome.Result);
    }

    private void AssertFailure(Outcome outcome, string message)
    {
        Diagnostics.AssertText("message", new SshTransferException(CurlExitCode.RemoteFileNotFound, message).Message, outcome.Failure?.Message ?? "(none)");
        Assert.AreEqual(new SshTransferException(CurlExitCode.RemoteFileNotFound, message).Message, outcome.Failure?.Message, "message");
    }

    private static byte[] ExecCommand(Outcome outcome)
    {
        byte[] request = SftpServerScript.SshPayloads(outcome.Written)[1];
        return request[(1 + 4 + 4 + 4 + 1 + 4)..];
    }

    private async Task<SshTransferException> DownloadFailsAsync(ScpServerScript script)
    {
        Diagnostics.ArrangeTransfer("/f", script.Bytes);
        SshTransferException failure;
        using (Diagnostics.Phase("download"))
        {
            failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
                async () => await new ScpFileDownload(SftpSessionTests.Transport(new ScriptedConnection(script.Bytes)))
                    .DownloadAsync("/f", new MemoryStream(), new RecordingProgress(), CancellationToken.None));
        }

        SshAuthenticationDiagnostics.ActFailure(Diagnostics, failure);
        return failure;
    }

    private async Task<Outcome> DownloadAsync(ScpServerScript script, string urlPath, long? maxFileSize = null)
    {
        Diagnostics.ArrangeTransfer(urlPath, script.Bytes);
        Diagnostics.Arrange("max file size", maxFileSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        Outcome outcome;
        using (Diagnostics.Phase("download"))
        {
            outcome = await RunDownloadAsync(script, urlPath, maxFileSize);
        }

        Diagnostics.ActTransfer(outcome.Result, outcome.Output, outcome.Progress);
        if (outcome.Failure is not null)
        {
            SshAuthenticationDiagnostics.ActFailure(Diagnostics, outcome.Failure);
        }

        Diagnostics.ActBytes("channel bytes", outcome.ChannelBytes);
        Diagnostics.ActSshMessages(outcome.Written);
        return outcome;
    }

    private static async Task<Outcome> RunDownloadAsync(ScpServerScript script, string urlPath, long? maxFileSize)
    {
        ScriptedConnection connection = new(script.Bytes);
        MemoryStream output = new();
        RecordingProgress progress = new();
        try
        {
            TransferResult result = await new ScpFileDownload(SftpSessionTests.Transport(connection))
                .DownloadAsync(urlPath, output, progress, CancellationToken.None, maxFileSize);
            return new Outcome(result, null, output.ToArray(), progress.Reports, connection.Written);
        }
        catch (SshTransferException failure)
        {
            Assert.AreEqual(CurlExitCode.RemoteFileNotFound, failure.ExitCode);
            return new Outcome(null, failure, output.ToArray(), progress.Reports, connection.Written);
        }
    }

    private sealed record Outcome(TransferResult? Result, SshTransferException? Failure, byte[] Output, List<(long, long?)> Progress, byte[] Written)
    {
        internal byte[] ChannelBytes => ScpServerScript.ChannelBytes(Written);
    }

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
