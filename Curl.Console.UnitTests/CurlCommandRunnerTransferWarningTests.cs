using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    private static string ExpectedWarnings => CaPathWarnings.Replace("\r\n", Environment.NewLine, StringComparison.Ordinal);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_CaPathWarningsAndTwoFailingUrls_PrintsTheWarningsBeforeEachUrlsErrorLine()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.Failing(
            "http", CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.1 port 1");

        int exitCode = await RunAsync(["--capath", ".", "http://127.0.0.1:1/", "http://127.0.0.1:1/"], http);

        Assert.AreEqual(7, exitCode);
        string errorLine = "curl: (7) Failed to connect to 127.0.0.1 port 1" + Environment.NewLine;
        Assert.AreEqual(ExpectedWarnings + errorLine + ExpectedWarnings + errorLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningAt79Columns_PrintsTheTwoMeasuredLines()
    {
        int exitCode = await RunAsync(
            ["--capath", ".", "file:///C:/Windows/win.ini"], 79, RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "Warning: ignoring setting the CA path for the proxy, not supported by libcurl " + Environment.NewLine
            + "Warning: with Schannel" + Environment.NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningAt200Columns_PrintsOneLine()
    {
        int exitCode = await RunAsync(
            ["--capath", ".", "file:///C:/Windows/win.ini"], 200, RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(CaPathWarningLine + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarnings_ArePrintedBeforeTheHandlerRuns()
    {
        RecordingProtocolHandler file = new("file", _ =>
        {
            Assert.AreEqual(ExpectedWarnings, StandardErrorText);

            return ValueTask.FromResult(TransferResult.Success(0));
        });

        int exitCode = await RunAsync(["--capath", ".", "file:///C:/Windows/win.ini"], file);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, file.Contexts);
        Assert.AreEqual(ExpectedWarnings, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningsAndAMalformedUrl_PrintsTheWarningsBeforeTheMalformedUrlLine()
    {
        int exitCode = await RunAsync(["--capath", ".", "dict://exa mple.com/d:x"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(
            ExpectedWarnings + "curl: (3) URL rejected: Malformed input to a URL function" + Environment.NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningsAndAnUnopenableOutputFile_PrintsTheWarningsBeforeTheOpenWarning()
    {
        fileSystem.UnwritablePaths.Add("out.txt");

        RecordingProtocolHandler empty = new("file", _ => ValueTask.FromResult(TransferResult.Success(0)));

        await RunAsync(["--capath", ".", "-o", "out.txt", "file:///empty"], empty);

        StringAssert.StartsWith(StandardErrorText, ExpectedWarnings + "Warning: Failed to open the file out.txt");
    }

    [TestMethod]
    public async Task RunAsync_CaPathWarningsAndAnUnopenableHeaderFile_PrintsOnlyTheHeaderFileLines()
    {
        fileSystem.UnwritablePaths.Add("hd.txt");

        int exitCode = await RunAsync(
            ["--capath", ".", "-D", "hd.txt", "file:///C:/Windows/win.ini"],
            RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual(
            "curl: Failed to open hd.txt" + Environment.NewLine
            + "curl: (23) " + CurlCommandRunner.WriteReceivedDataFailedMessage + Environment.NewLine,
            StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("-sS")]
    public async Task RunAsync_SilentAnywhereWithCaPathWarnings_PrintsNoWarning(string silent)
    {
        int exitCode = await RunAsync(
            ["--capath", ".", "file:///C:/Windows/win.ini", silent], RecordingProtocolHandler.WritingPath("file"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers) =>
        RunAsync(arguments, TerminalColumns.Default, handlers);

    private Task<int> RunAsync(IReadOnlyList<string> arguments, int terminalColumns, params IProtocolHandler[] handlers) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(handlers), CaPathWarningLines),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                terminalColumns)
            .RunAsync(arguments);
}
