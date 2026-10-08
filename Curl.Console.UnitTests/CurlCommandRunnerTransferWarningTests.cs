using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins where <see cref="TransferDispatch.WarningLinesBeforeEachTransfer" /> go against
/// curl 8.21.0 (mingw, Schannel), measured on Windows on 2026-09-26 with
/// <c>--no-progress-meter --capath .</c>: on standard error once per URL, before that URL's
/// own lines, never under <c>-s</c> or <c>-sS</c>, and not at all for a URL whose <c>-D</c>
/// file cannot be opened. No test opens a socket.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerTransferWarningTests
{
    // The Schannel build's two --capath lines at the default 79 columns (ADR-0009), the first
    // with its trailing space.
    private const string CaPathWarnings =
        "Warning: ignoring setting the CA path for the proxy, not supported by libcurl \r\n"
        + "Warning: with Schannel\r\n";

    // The one warning the Schannel provider reports, unwrapped; the runner wraps it.
    private const string CaPathWarningLine =
        "Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel";

    private static readonly string[] CaPathWarningLines = [CaPathWarningLine];

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static string ExpectedWarnings => CaPathWarnings.Replace("\r\n", Environment.NewLine, StringComparison.Ordinal);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_CaPathWarningsAndTwoFailingUrls_PrintsTheWarningsBeforeEachUrlsErrorLine()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.Failing(
            "http", CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.1 port 1");

        Diagnostics.Arrange("handler behaviour", "http fails with exit 7, Failed to connect to 127.0.0.1 port 1");

        int exitCode = await RunAsync(["--capath", ".", "http://127.0.0.1:1/", "http://127.0.0.1:1/"], http);

        Diagnostics.Assert("exit code", 7, exitCode);
        Assert.AreEqual(7, exitCode);
        string errorLine = "curl: (7) Failed to connect to 127.0.0.1 port 1" + Environment.NewLine;
        Diagnostics.Diff(
            "stderr",
            Normalized(ExpectedWarnings + errorLine + ExpectedWarnings + errorLine),
            Normalized(StandardErrorText));
        Assert.AreEqual(ExpectedWarnings + errorLine + ExpectedWarnings + errorLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningAt79Columns_PrintsTheTwoMeasuredLines()
    {
        int exitCode = await RunAsync(
            ["--capath", ".", "file:///Windows/win.ini"], 79, RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: ignoring setting the CA path for the proxy, not supported by libcurl \n"
            + "Warning: with Schannel\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "Warning: ignoring setting the CA path for the proxy, not supported by libcurl " + Environment.NewLine
            + "Warning: with Schannel" + Environment.NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningAt200Columns_PrintsOneLine()
    {
        int exitCode = await RunAsync(
            ["--capath", ".", "file:///Windows/win.ini"], 200, RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", CaPathWarningLine + "\n", Normalized(StandardErrorText));
        Assert.AreEqual(CaPathWarningLine + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarnings_ArePrintedBeforeTheHandlerRuns()
    {
        RecordingProtocolHandler file = new("file", _ =>
        {
            Diagnostics.Diff("stderr when the handler runs", Normalized(ExpectedWarnings), Normalized(StandardErrorText));
            Assert.AreEqual(ExpectedWarnings, StandardErrorText);

            return ValueTask.FromResult(TransferResult.Success(0));
        });
        Diagnostics.Arrange("handler behaviour", "file checks stderr already holds the warnings, then succeeds with 0 bytes");

        int exitCode = await RunAsync(["--capath", ".", "file:///Windows/win.ini"], file);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("handler call count", 1, file.Contexts.Count);
        Assert.HasCount(1, file.Contexts);
        Diagnostics.Diff("stderr", Normalized(ExpectedWarnings), Normalized(StandardErrorText));
        Assert.AreEqual(ExpectedWarnings, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningsAndAMalformedUrl_PrintsTheWarningsBeforeTheMalformedUrlLine()
    {
        int exitCode = await RunAsync(["--capath", ".", "dict://exa mple.com/d:x"]);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff(
            "stderr",
            Normalized(ExpectedWarnings) + "curl: (3) URL rejected: Malformed input to a URL function\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            ExpectedWarnings + "curl: (3) URL rejected: Malformed input to a URL function" + Environment.NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningsAndAnUnopenableOutputFile_PrintsTheWarningsBeforeTheOpenWarning()
    {
        fileSystem.UnwritablePaths.Add("out.txt");

        RecordingProtocolHandler empty = new("file", _ => ValueTask.FromResult(TransferResult.Success(0)));
        Diagnostics.Arrange("handler behaviour", "file succeeds with 0 bytes");

        await RunAsync(["--capath", ".", "-o", "out.txt", "file:///empty"], empty);

        Diagnostics.Assert(
            "stderr starts with the warnings then the open warning",
            true,
            StandardErrorText.StartsWith(ExpectedWarnings + "Warning: Failed to open the file out.txt", StringComparison.Ordinal));
        StringAssert.StartsWith(StandardErrorText, ExpectedWarnings + "Warning: Failed to open the file out.txt");
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningsAndAnUnopenableHeaderFile_PrintsOnlyTheHeaderFileLines()
    {
        fileSystem.UnwritablePaths.Add("hd.txt");

        int exitCode = await RunAsync(
            ["--capath", ".", "-D", "hd.txt", "file:///Windows/win.ini"],
            RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: Failed to open hd.txt\n" + "curl: (23) " + CurlCommandRunner.WriteReceivedDataFailedMessage + "\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: Failed to open hd.txt" + Environment.NewLine
            + "curl: (23) " + CurlCommandRunner.WriteReceivedDataFailedMessage + Environment.NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_Tls13CipherWarningsAt79ColumnsAndTwoUrls_PrintsBothUnwrappedBeforeEachUrl()
    {
        // Measured, curl 8.21.0 Schannel (BL-1034): each line is 72 or 79 bytes, so neither wraps.
        string[] lines =
        [
            "Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel",
            "Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel",
        ];
        string warnings = lines[0] + Environment.NewLine + lines[1] + Environment.NewLine;
        IProtocolHandler[] handlers = [RecordingProtocolHandler.WritingPath("file")];
        Diagnostics.Arrange("terminal columns", 79);

        int exitCode = await RunWithDiagnosticsAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(handlers), lines),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                79),
            ["-o", "a", "-o", "b", "file:///Windows/win.ini", "file:///Windows/win.ini"],
            lines,
            handlers);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", Normalized(warnings + warnings), Normalized(StandardErrorText));
        Assert.AreEqual(warnings + warnings, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-O")]
    [DataRow("--remote-name-all")]
    public async Task RunAsync_Tls13CipherWarningAndRemoteNameWithoutFileName_PrintsNoRemoteFilenameFirst(string remoteName)
    {
        // Measured, curl 8.21.0 Schannel (BL-1362, AF-0027): --tls13-ciphers x -O http://127.0.0.1:PORT/
        // names the output file before it sets the transfer's options.
        string[] lines = ["Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel"];

        IProtocolHandler[] handlers = [RecordingProtocolHandler.WritingPath("http")];
        Diagnostics.Arrange("terminal columns", 79);

        int exitCode = await RunWithDiagnosticsAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(handlers), lines),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                79),
            [remoteName, "http://127.0.0.1:1/"],
            lines,
            handlers);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "stderr",
            "Warning: No remote filename, uses \"curl_response\"\n" + lines[0] + "\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "Warning: No remote filename, uses \"curl_response\"" + Environment.NewLine + lines[0] + Environment.NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameOnAUrlThatDoesNotParse_PrintsNoRemoteFilenameWarning()
    {
        int exitCode = await RunAsync(["-O", "http://exa mple.com/"], RecordingProtocolHandler.WritingPath("http"));

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Assert(
            "stderr contains No remote filename",
            false,
            StandardErrorText.Contains("No remote filename", StringComparison.Ordinal));
        Assert.IsFalse(StandardErrorText.Contains("No remote filename", StringComparison.Ordinal), StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("-sS")]
    public async Task RunAsync_SilentAnywhereWithCaPathWarnings_PrintsNoWarning(string silent)
    {
        int exitCode = await RunAsync(
            ["--capath", ".", "file:///Windows/win.ini", silent], RecordingProtocolHandler.WritingPath("file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Normalized(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers) =>
        RunAsync(arguments, TerminalColumns.Default, handlers);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, int terminalColumns, params IProtocolHandler[] handlers)
    {
        Diagnostics.Arrange("terminal columns", terminalColumns);

        return await RunWithDiagnosticsAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(handlers), CaPathWarningLines),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                terminalColumns),
            arguments,
            CaPathWarningLines,
            handlers);
    }

    private async Task<int> RunWithDiagnosticsAsync(
        CurlCommandRunner runner,
        IReadOnlyList<string> arguments,
        string[] warningLines,
        IProtocolHandler[] handlers)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("warning lines before each transfer", string.Join(" | ", warningLines));
        Diagnostics.Arrange(
            "handler schemes",
            string.Join(", ", handlers.Select(handler => string.Join('/', handler.SupportedSchemes))));
        Diagnostics.Arrange("unwritable paths", string.Join(", ", fileSystem.UnwritablePaths));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Act("stderr", Normalized(StandardErrorText));
        return exitCode;
    }
}
