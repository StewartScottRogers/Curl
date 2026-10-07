using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins curl 8.21.0's <c>-v</c> line <c>Uploaded unaligned file size (N out of M bytes)</c>,
/// which <c>ftp_done_check_partial</c> writes when an upload of a known size ends, on a failure
/// that leaves the control connection usable, before its bytes all went out (BL-1395).
/// Measured 2026-10-03 with <c>Record-CurlExchange.ps1 -Ftp</c> uploading a 92-byte file.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerUnalignedUploadTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Passive = "229 Entering Extended Passive Mode (|||57697|)\r\n200 Type set\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Unaligned = "Uploaded unaligned file size (0 out of 92 bytes)";

    private const string LeftIntact = "Connection #0 to host 127.0.0.1:18439 left intact";

    [TestMethod]
    public async Task ExecuteAsync_CwdRefusedForAKnownSizeUpload_ReportsTheUnalignedSizeBeforeLeftIntact()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -sv -T win.ini ftp://127.0.0.1:P/d/f, CWD answered 550: exit 9.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(diagnostics, "ftp://127.0.0.1:18439/d/f", LoggedIn + "550 No such dir\r\n" + Bye, Known(), events);
        diagnostics.ActRun(run);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Server denied you to change to the given directory"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Server denied you to change to the given directory"), run.Result);
        diagnostics.Diff("lines", string.Join('\n', new[] { Unaligned, LeftIntact }), string.Join('\n', events.Info.TakeLast(2).ToArray()));
        CollectionAssert.AreEqual(new[] { Unaligned, LeftIntact }, events.Info.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StorRefusedWith553ForAKnownSizeUpload_ReportsTheUnalignedSizeAfterTheDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -sv -T win.ini ftp://127.0.0.1:P/f, STOR answered 553: exit 25.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(diagnostics, "ftp://127.0.0.1:18439/f", LoggedIn + Passive + "553 Not allowed\r\n" + Bye, Known(), events);
        diagnostics.ActRun(run);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.UploadFailed, "Failed FTP upload: 553"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UploadFailed, "Failed FTP upload: 553"), run.Result);
        diagnostics.Diff("lines", string.Join('\n', new[] { "Remembering we are in directory \"\"", Unaligned, LeftIntact }), string.Join('\n', events.Info.TakeLast(3).ToArray()));
        CollectionAssert.AreEqual(new[] { "Remembering we are in directory \"\"", Unaligned, LeftIntact }, events.Info.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StorRefusedWith553UnderCrlf_ReportsTheUnalignedSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -sv --crlf -T win.ini ftp://127.0.0.1:P/f, STOR answered 553: exit 25.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(diagnostics, "ftp://127.0.0.1:18439/f", LoggedIn + Passive + "553 Not allowed\r\n" + Bye, Known(), events, convertLineEndings: true);
        diagnostics.ActRun(run);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.UploadFailed, "Failed FTP upload: 553"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UploadFailed, "Failed FTP upload: 553"), run.Result);
        diagnostics.Diff("lines", string.Join('\n', new[] { Unaligned, LeftIntact }), string.Join('\n', events.Info.TakeLast(2).ToArray()));
        CollectionAssert.AreEqual(new[] { Unaligned, LeftIntact }, events.Info.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StorRefusedWith553ForStandardInput_ReportsNoUnalignedSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -sv -T - ftp://127.0.0.1:P/f, STOR answered 553: the size is unknown.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(diagnostics, "ftp://127.0.0.1:18439/f", LoggedIn + Passive + "553 Not allowed\r\n" + Bye, new ForwardOnlyStream(new byte[92]), events);
        diagnostics.ActRun(run);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.UploadFailed, "Failed FTP upload: 553"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UploadFailed, "Failed FTP upload: 553"), run.Result);
        diagnostics.Diff("lines", string.Join('\n', new[] { "Remembering we are in directory \"\"", LeftIntact }), string.Join('\n', events.Info.TakeLast(2).ToArray()));
        CollectionAssert.AreEqual(new[] { "Remembering we are in directory \"\"", LeftIntact }, events.Info.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessfulKnownSizeUpload_ReportsNoUnalignedSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(diagnostics,
            "ftp://127.0.0.1:18439/f",
            LoggedIn + Passive + "150 Opening\r\n226 Transfer complete\r\n" + Bye,
            Known(),
            events);
        diagnostics.ActRun(run);

        diagnostics.Assert("result", TransferResult.Success(92), run.Result);
        Assert.AreEqual(TransferResult.Success(92), run.Result);
        diagnostics.Assert("info holds the line", false, events.Info.Contains(Unaligned));
        Assert.DoesNotContain(Unaligned, events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessfulCrlfUploadSendingMoreThanItsSize_ReportsNoUnalignedSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // --crlf turns each \n into \r\n: more bytes than the size go out, which is not short.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(diagnostics,
            "ftp://127.0.0.1:18439/f",
            LoggedIn + Passive + "150 Opening\r\n226 Transfer complete\r\n" + Bye,
            new MemoryStream("a\nb\n"u8.ToArray()),
            events,
            convertLineEndings: true);
        diagnostics.ActRun(run);

        diagnostics.Assert("result", TransferResult.Success(6), run.Result);
        Assert.AreEqual(TransferResult.Success(6), run.Result);
        diagnostics.Assert("matching info lines", 0, events.Info.Count(line => line.StartsWith("Uploaded unaligned", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Uploaded unaligned", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginRefusedForAKnownSizeUpload_ReportsNoUnalignedSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // exit 67 is a failure ftp_done does not map to OK.
        var events = new RecordingTransferEvents();
        FtpRun run = await RunAsync(diagnostics, "ftp://127.0.0.1:18439/f", "220 Recorder ready\r\n530 No\r\n", Known(), events);
        diagnostics.ActRun(run);

        diagnostics.Assert("result", CurlExitCode.LoginDenied, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        diagnostics.Assert("matching info lines", 0, events.Info.Count(line => line.StartsWith("Uploaded unaligned", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Uploaded unaligned", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWithCwdRefused_ReportsNoUnalignedSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var events = new RecordingTransferEvents();
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18439/d/f");
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18439/d/f",
            LoggedIn + "550 No such dir\r\n" + Bye,
            adjust: c => new TransferContext { Url = c.Url, Output = c.Output, Events = events });
        diagnostics.ActRun(run);

        diagnostics.Assert("result", CurlExitCode.RemoteAccessDenied, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, run.Result.ExitCode);
        diagnostics.Assert("matching info lines", 0, events.Info.Count(line => line.StartsWith("Uploaded unaligned", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Uploaded unaligned", StringComparison.Ordinal)));
    }

    /// <summary>A 92-byte source of known size, as <c>-T</c> names a file.</summary>
    private static MemoryStream Known() => new(new byte[92]);

    private static Task<FtpRun> RunAsync(TestDiagnostics diagnostics, string url, string replies, Stream upload, RecordingTransferEvents events, bool convertLineEndings = false)
    {
        diagnostics.ArrangeFtp(url, replies);
        diagnostics.Arrange("convert line endings", convertLineEndings);

        return FtpRun.ExecuteAsync(
            url,
            replies,
            adjust: c => new TransferContext
            {
                Url = c.Url,
                Output = c.Output,
                Upload = upload,
                Events = events,
                ConvertLineEndings = convertLineEndings,
            });
    }
}
